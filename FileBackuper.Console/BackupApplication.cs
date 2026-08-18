using FileBackuper.Core;

namespace FileBackuper.Console;

internal class BackupApplication
{
    public void Run()
    {
        Stat.Reset();
        Stat.Start();

        BackupOptions options = LoggingConfiguration.LoadBackupOptions();
        ConsoleWindow.SetVisibility(options.ShowConsole);
        string stateDirectory = StatePaths.EnsureStateDirectory(options.StateDirectory);

        using CancellationTokenSource cancellationSource = new();
        int exitRequested = 0;
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        System.Console.CancelKeyPress += cancelHandler;
        try
        {
            string destinationDirectory = BackupPaths.ResolveDestinationDirectory(options.DestinationDirectory);
            BackupOptionsValidator.Validate(options, destinationDirectory);
            CloudFileState.Configure(options.CloudFileMode);
            FileCopier.CreateDestinationDirectory(destinationDirectory);
            Stat.ConfigureStatusDirectory(destinationDirectory);
            LoggingConfiguration.Configure(destinationDirectory);
            BackupLog.Info($"Анализ EXIF: {(options.EnableExifAnalysis ? "включён" : "выключен")}");
            BackupLog.Info($"Сканирование кэша браузеров и анализ файлов без расширения: " +
                $"{(options.IncludeBrowserCaches ? "включены" : "выключены")}");
            BackupLog.Flush();

            JobManifestStore manifestStore = new(stateDirectory);
            string rulesDirectory = Path.Combine(AppContext.BaseDirectory, "Rules");
            RegexPatternSet cameraFileNamePatterns = RegexPatternSet.Load(
                Path.Combine(rulesDirectory, "CameraFileNamePatterns.txt"));
            RegexPatternSet videoBlacklistPatterns = RegexPatternSet.Load(
                Path.Combine(rulesDirectory, "VideoBlacklistPatterns.txt"));
            MediaFileAnalysisService mediaFileAnalysisService = new(options.MinFileSizeBytes,
                options.MaxFileSizeBytes, cameraFileNamePatterns, videoBlacklistPatterns,
                enableExifAnalysis: options.EnableExifAnalysis);
            FileSizeGroupService fileSizeGroupService = new(options.FileSizeGroups);
            Stat.ConfigureSizeGroups(options.FileSizeGroups);
            BackupFilePriorityService priorityService = new(fileSizeGroupService);
            BackupFileDiagnosticFormatter diagnosticFormatter = new(fileSizeGroupService,
                includeExifFields: options.EnableExifAnalysis,
                includeSignatureFields: options.IncludeBrowserCaches);
            string instanceId = Guid.NewGuid().ToString("N");
            using BackupJobManager jobManager = new(stateDirectory, destinationDirectory, manifestStore, instanceId,
                skipDirectoryNames: options.SkipDirectoryNames,
                includeBrowserCaches: options.IncludeBrowserCaches,
                minFileSizeBytes: options.MinFileSizeBytes,
                maxFileSizeBytes: options.MaxFileSizeBytes,
                mediaFileAnalysisService: mediaFileAnalysisService,
                priorityService: priorityService,
                diagnosticFormatter: diagnosticFormatter);
            CopyScheduler copyScheduler = new(manifestStore, priorityService);
            CopyPauseState copyPauseState = new();

            using GlobalHotKeys hotKeys = new(
                copyPauseState.Pause,
                copyPauseState.Resume,
                () =>
                {
                    if (Interlocked.Exchange(ref exitRequested, 1) != 0)
                        return;
                    BackupLog.Info("Запрошен выход горячей клавишей Ctrl+Shift+Alt+X.");
                    BackupLog.Flush();
                    cancellationSource.Cancel();
                });
            BackupLog.InfoBlock("Горячие клавиши: пауза Ctrl+Shift+Alt+P; " +
                "продолжить Ctrl+Shift+Alt+R; выход Ctrl+Shift+Alt+X.");
            BackupLog.Flush();

            RunBackup(jobManager, copyScheduler, copyPauseState, destinationDirectory, options,
                cancellationSource.Token);
            if (!jobManager.HasForeignWork)
                manifestStore.DeleteCompleted();
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            Stat.Stop();
            BackupLog.Warning(Volatile.Read(ref exitRequested) != 0
                ? "Работа завершена по горячей клавише выхода."
                : "Операция отменена пользователем.");
            BackupLog.Flush();
        }
        finally
        {
            System.Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static void RunBackup(BackupJobManager jobManager, CopyScheduler copyScheduler,
        CopyPauseState copyPauseState, string destinationDirectory, BackupOptions options,
        CancellationToken cancellationToken)
    {
        BackupLog.Info("Подготовка начальной общей очереди дисков.");

        BackupJobBatch? initialBatch = jobManager
            .DiscoverAndPrepareInitialBatchAsync(cancellationToken)
            .GetAwaiter()
            .GetResult();
        if (initialBatch is not null)
            copyScheduler.Enqueue(initialBatch);

        DateTimeOffset nextDriveCheckUtc = DateTimeOffset.MinValue;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool checkedDrives = false;
            if (options.MonitorNewDrives && DateTimeOffset.UtcNow >= nextDriveCheckUtc)
            {
                DiscoverAdditionalJobs(jobManager, copyScheduler, cancellationToken);
                nextDriveCheckUtc = DateTimeOffset.UtcNow.AddSeconds(options.DrivePollingIntervalSeconds);
                checkedDrives = true;
            }

            bool copiedFile = !copyPauseState.IsPaused &&
                copyScheduler.CopyNextAsync(destinationDirectory, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
            if (copiedFile)
                continue;

            if (!jobManager.HasPreparingJobs)
            {
                if (options.MonitorNewDrives && !checkedDrives)
                {
                    DiscoverAdditionalJobs(jobManager, copyScheduler, cancellationToken);
                    nextDriveCheckUtc = DateTimeOffset.UtcNow.AddSeconds(options.DrivePollingIntervalSeconds);
                    if (copyScheduler.PendingFileCount > 0 || jobManager.HasPreparingJobs)
                        continue;
                }

                if (copyPauseState.IsPaused && copyScheduler.PendingFileCount > 0)
                {
                    DelayWhilePaused(cancellationToken);
                    continue;
                }

                jobManager.LogTotalAnalysisStatistics();
                copyScheduler.LogFinalStatistics();
                TimeSpan duration = Stat.Stop();
                BackupLog.Info($"Все доступные диски обработаны. Время работы: {duration:hh\\:mm\\:ss\\.ff}");
                BackupLog.Flush();
                return;
            }

            Task.Delay(copyPauseState.IsPaused
                    ? TimeSpan.FromMilliseconds(200)
                    : TimeSpan.FromSeconds(options.DrivePollingIntervalSeconds), cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
    }

    private static void DiscoverAdditionalJobs(BackupJobManager jobManager, CopyScheduler copyScheduler,
        CancellationToken cancellationToken)
    {
        jobManager.DiscoverAndStartAdditionalJobs(cancellationToken);
        foreach (BackupJob job in jobManager.ReadyJobs)
            copyScheduler.Enqueue(job);
    }

    private static void DelayWhilePaused(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).GetAwaiter().GetResult();
}
