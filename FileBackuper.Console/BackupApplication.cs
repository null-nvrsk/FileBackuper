using FileBackuper.Core;

namespace FileBackuper.Console;

internal class BackupApplication
{
    public void Run()
    {
        BackupOptions options = LoggingConfiguration.LoadBackupOptions();
        ConsoleWindow.SetVisibility(options.ShowConsole);
        string stateDirectory = StatePaths.EnsureStateDirectory(options.StateDirectory);

        using CancellationTokenSource cancellationSource = new();
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
            FileCopier.CreateDestinationDirectory(destinationDirectory);
            Stat.ConfigureStatusDirectory(destinationDirectory);
            LoggingConfiguration.Configure(destinationDirectory);

            JobManifestStore manifestStore = new(stateDirectory);
            string instanceId = Guid.NewGuid().ToString("N");
            if (options.MonitorNewDrives)
            {
                MonitorDrives(stateDirectory, destinationDirectory, manifestStore, instanceId,
                    options.DrivePollingIntervalSeconds, cancellationSource.Token);
            }
            else
            {
                ProcessAvailableDrives(stateDirectory, destinationDirectory, manifestStore, instanceId,
                    cancellationSource.Token);
            }

            manifestStore.DeleteCompleted();
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            Stat.Stop();
            BackupLog.Warning("Операция отменена пользователем.");
            BackupLog.Flush();
        }
        finally
        {
            System.Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static void MonitorDrives(string stateDirectory, string destinationDirectory,
        JobManifestStore manifestStore, string instanceId, int pollingIntervalSeconds,
        CancellationToken cancellationToken)
    {
        Stat.Start();
        BackupLog.Info($"Мониторинг новых дисков: проверка каждые {pollingIntervalSeconds} сек.");
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (bool processedAnyDrive, bool hasActiveWork) = ProcessAvailableDrives(
                stateDirectory, destinationDirectory, manifestStore, instanceId, cancellationToken);

            if (!processedAnyDrive && !hasActiveWork)
            {
                BackupLog.Info("Все доступные диски обработаны. Завершение работы.");
                return;
            }

            if (processedAnyDrive)
                continue;

            Task.Delay(TimeSpan.FromSeconds(pollingIntervalSeconds), cancellationToken).GetAwaiter().GetResult();
        }
    }

    private static (bool ProcessedAnyDrive, bool HasActiveWork) ProcessAvailableDrives(
        string stateDirectory, string destinationDirectory,
        JobManifestStore manifestStore, string instanceId, CancellationToken cancellationToken)
    {
        bool processedAnyDrive = false;
        bool hasActiveWork = false;
        List<DriveInfo> drives = FileScanner.GetDrivesToScan(destinationDirectory);
        foreach (DriveInfo drive in drives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string volumeId = VolumeIdentity.GetVolumeId(drive);
                JobManifest? existingManifest = manifestStore.Read(volumeId);
                if (existingManifest?.Status == JobStatus.Completed)
                {
                    BackupLog.Info($"Диск {drive.Name} уже обработан.");
                    continue;
                }

                using VolumeLease? lease = VolumeLease.TryAcquire(stateDirectory, volumeId);
                if (lease is null)
                {
                    hasActiveWork = true;
                    BackupLog.Info($"Диск {drive.Name} уже обрабатывается другим процессом.");
                    continue;
                }

                JobManifest manifest = CreateManifest(volumeId, drive.Name, destinationDirectory, instanceId);
                manifestStore.Save(manifest);

                try
                {
                    ProcessDrive(drive, destinationDirectory, manifestStore, ref manifest, cancellationToken);
                    processedAnyDrive = true;
                }
                catch (OperationCanceledException)
                {
                    manifestStore.Save(UpdateManifest(manifest, JobStatus.Cancelled));
                    throw;
                }
                catch (Exception exception)
                {
                    manifestStore.Save(UpdateManifest(manifest, JobStatus.Failed, lastError: exception.Message));
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                hasActiveWork = true;
                BackupLog.Warning($"Не удалось обработать диск {drive.Name}: {exception.Message}");
            }
        }

        return (processedAnyDrive, hasActiveWork);
    }

    private static void ProcessDrive(DriveInfo drive, string destinationDirectory, JobManifestStore manifestStore,
        ref JobManifest manifest, CancellationToken cancellationToken)
    {
        Stat.Reset();
        List<FileInfo> files = ScanFiles(drive, cancellationToken);
        long totalSize = files.Sum(file => file.Length);
        manifest = UpdateManifest(manifest, JobStatus.Sorting, filesFound: files.Count, totalBytes: totalSize);
        manifestStore.Save(manifest);

        files = OrderFiles(files, cancellationToken);
        manifest = UpdateManifest(manifest, JobStatus.Copying);
        manifestStore.Save(manifest);

        CopyFiles(files, destinationDirectory, totalSize, cancellationToken);
        manifestStore.Save(UpdateManifest(manifest, JobStatus.Completed,
            filesCompleted: files.Count, completedBytes: totalSize));
    }

    private static List<FileInfo> ScanFiles(DriveInfo drive, CancellationToken cancellationToken)
    {
        Stat.Start();
        BackupLog.Info($"Начало сканирования диска {drive.Name}");
        List<FileInfo> files = FileScanner.Scan(drive.RootDirectory, cancellationToken);

        TimeSpan scanDuration = Stat.Stop();
        BackupLog.Info($"Время сканирования: {scanDuration:hh\\:mm\\:ss\\.ff}");
        BackupLog.Info($"Найдено файлов: {files.Count:N0}");
        BackupLog.Info($"Общий размер файлов: {files.Sum(file => file.Length):N0} байтов");
        BackupLog.Flush();
        return files;
    }

    private static List<FileInfo> OrderFiles(List<FileInfo> files, CancellationToken cancellationToken)
    {
        Stat.Start();
        BackupLog.Info("Начало сортировки");
        List<FileInfo> orderedFiles = FilePriorityService.OrderByBackupPriority(files, cancellationToken);
        TimeSpan sortDuration = Stat.Stop();
        BackupLog.Info($"Конец сортировки. Время сортировки: {sortDuration:hh\\:mm\\:ss\\.ff}");
        BackupLog.Flush();
        return orderedFiles;
    }

    private static void CopyFiles(IReadOnlyList<FileInfo> files, string destinationDirectory, long totalSize,
        CancellationToken cancellationToken)
    {
        Stat.Start();
        BackupLog.Info("Начало копирования");
        FileCopier.CopyFiles(files, destinationDirectory, cancellationToken);
        TimeSpan copyDuration = Stat.Stop();

        BackupLog.Info($"Время копирования: {copyDuration:hh\\:mm\\:ss\\.ff}");
        double copySpeed = totalSize / copyDuration.TotalSeconds;
        BackupLog.Info($"Скорость: {(copySpeed / 1024 / 1024):F2} Mb/s");
        BackupLog.Info($"          {(copySpeed / 1024 / 1024 / 1024 * 60):F2} Gb/min");
        BackupLog.Flush();
    }

    private static JobManifest CreateManifest(string volumeId, string driveLetter, string destinationDirectory,
        string instanceId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new JobManifest
        {
            VolumeId = volumeId,
            CurrentDriveLetter = driveLetter,
            Status = JobStatus.Scanning,
            OwnerInstanceId = instanceId,
            OwnerProcessId = Environment.ProcessId,
            StartedUtc = now,
            LastHeartbeatUtc = now,
            DestinationDirectory = destinationDirectory
        };
    }

    private static JobManifest UpdateManifest(JobManifest manifest, JobStatus status, long? filesFound = null,
        long? totalBytes = null, long? filesCompleted = null, long? completedBytes = null, string? lastError = null)
    {
        return new JobManifest
        {
            SchemaVersion = manifest.SchemaVersion,
            VolumeId = manifest.VolumeId,
            CurrentDriveLetter = manifest.CurrentDriveLetter,
            Status = status,
            OwnerInstanceId = manifest.OwnerInstanceId,
            OwnerProcessId = manifest.OwnerProcessId,
            StartedUtc = manifest.StartedUtc,
            LastHeartbeatUtc = DateTimeOffset.UtcNow,
            DestinationDirectory = manifest.DestinationDirectory,
            FilesFound = filesFound ?? manifest.FilesFound,
            TotalBytes = totalBytes ?? manifest.TotalBytes,
            FilesCompleted = filesCompleted ?? manifest.FilesCompleted,
            CompletedBytes = completedBytes ?? manifest.CompletedBytes,
            CurrentFile = manifest.CurrentFile,
            LastError = lastError ?? manifest.LastError
        };
    }
}
