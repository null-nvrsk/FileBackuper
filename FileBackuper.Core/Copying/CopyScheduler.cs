namespace FileBackuper.Core;

/// <summary>
/// Owns a single copy stream. Producers may add prepared jobs concurrently,
/// while this scheduler chooses the most important next file and copies it.
/// </summary>
public sealed class CopyScheduler
{
    private readonly object syncRoot = new();
    private readonly List<JobQueue> queues = new();
    private readonly JobManifestStore manifestStore;
    private readonly BackupFilePriorityService priorityService;
    private readonly System.Diagnostics.Stopwatch copyStopwatch = new();
    private bool copyingStarted;
    private int copiedFileCount;
    private bool finalStatisticsLogged;

    public CopyScheduler(JobManifestStore manifestStore, BackupFilePriorityService priorityService)
    {
        ArgumentNullException.ThrowIfNull(manifestStore);
        ArgumentNullException.ThrowIfNull(priorityService);
        this.manifestStore = manifestStore;
        this.priorityService = priorityService;
    }

    public int PendingFileCount
    {
        get
        {
            lock (syncRoot)
                return queues.Sum(queue => queue.Files.Count);
        }
    }

    public void Enqueue(BackupJobBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        foreach (BackupJob job in batch.Jobs)
        {
            if (job.Status == JobStatus.Copying)
            {
                Enqueue(job);
                continue;
            }

            BackupLog.Warning($"Задача диска {job.SourceDrive.Name} не добавлена в очередь | " +
                $"Status={job.Status} | Error={job.Manifest.LastError ?? "-"}");
        }
    }

    public void Enqueue(BackupJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Status != JobStatus.Copying)
            throw new InvalidOperationException("Only jobs ready for copying can be enqueued.");

        lock (syncRoot)
        {
            if (queues.Any(queue => queue.Job.Manifest.VolumeId == job.Manifest.VolumeId))
                return;

            if (job.Files.Count == 0)
            {
                job.MarkCompleted();
                manifestStore.Save(job.Manifest);
                return;
            }

            queues.Add(new JobQueue(job));
        }
    }

    public async Task CopyAvailableAsync(string destinationDirectory, CancellationToken cancellationToken)
    {
        while (await CopyNextAsync(destinationDirectory, cancellationToken).ConfigureAwait(false))
        {
        }
    }

    public async Task<bool> CopyNextAsync(string destinationDirectory, CancellationToken cancellationToken)
    {
        if (!TryTakeNext(out ScheduledFile scheduledFile))
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        if (!copyingStarted)
        {
            copyingStarted = true;
            copyStopwatch.Start();
            Stat.StartCopying();
            BackupLog.Info("Начало копирования");
            BackupLog.Flush();
        }

        try
        {
            try
            {
                if (!CloudFileState.IsContentAvailableLocally(scheduledFile.File))
                {
                    Skip(scheduledFile, "исходный файл находится только в облаке");
                    await Task.Yield();
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               System.ComponentModel.Win32Exception)
            {
                Skip(scheduledFile, "не удалось безопасно подтвердить локальную доступность исходного файла: " +
                    BackupLog.GetExceptionDescription(exception));
                await Task.Yield();
                return true;
            }

            if (!File.Exists(scheduledFile.File.FullName))
            {
                Skip(scheduledFile, "исходный файл недоступен или удалён");
                await Task.Yield();
                return true;
            }

            System.Diagnostics.Stopwatch fileCopyStopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool alreadyExists = FileCopier.CopyFile(scheduledFile.Candidate, destinationDirectory,
                cancellationToken);
            fileCopyStopwatch.Stop();
            if (alreadyExists)
            {
                Skip(scheduledFile, "актуальный файл уже существует");
                await Task.Yield();
                return true;
            }

            Stat.AddFileToCompletedStat(scheduledFile.Candidate, fileCopyStopwatch.Elapsed);
            Stat.RecalculateEstimatedTime();
            Complete(scheduledFile);
            copiedFileCount++;
            BackupLog.Raw($"{priorityService.GetPriorityCode(scheduledFile.Candidate)} " +
                $"({copyStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}) Скопирован файл №{copiedFileCount:N0} = " +
                $"{scheduledFile.File.FullName} — размер {FormatSize(scheduledFile.Length)}");
        }
        catch (OperationCanceledException)
        {
            scheduledFile.Job.MarkCancelled();
            manifestStore.Save(scheduledFile.Job.Manifest);
            throw;
        }
        catch (Exception exception)
        {
            if (!CanReadSourceFile(scheduledFile.File))
            {
                Skip(scheduledFile, "исходный файл недоступен: " +
                    BackupLog.GetExceptionDescription(exception));
                await Task.Yield();
                return true;
            }

            scheduledFile.Job.MarkFailed(exception.Message);
            manifestStore.Save(scheduledFile.Job.Manifest);
            RemoveQueue(scheduledFile.Job);
        }

        await Task.Yield();
        return true;
    }

    public void LogFinalStatistics()
    {
        if (finalStatisticsLogged)
            return;

        finalStatisticsLogged = true;
        if (copyingStarted)
            copyStopwatch.Stop();
        Stat.LogFinalProgress();
    }

    public bool TryTakeNext(out ScheduledFile scheduledFile)
    {
        lock (syncRoot)
        {
            JobQueue? selectedQueue = queues
                .Where(queue => queue.Files.Count > 0)
                .OrderBy(queue => queue.Files.Peek().Candidate, priorityService.Comparer)
                .FirstOrDefault();

            if (selectedQueue is null)
            {
                scheduledFile = null!;
                return false;
            }

            QueuedFile queuedFile = selectedQueue.Files.Dequeue();
            scheduledFile = new ScheduledFile(selectedQueue.Job, queuedFile.Candidate, queuedFile.Length);
            return true;
        }
    }

    private void Complete(ScheduledFile scheduledFile)
    {
        scheduledFile.Job.MarkFileCopied(scheduledFile.Candidate);
        lock (syncRoot)
        {
            JobQueue queue = queues.Single(item => item.Job == scheduledFile.Job);
            if (queue.Files.Count == 0)
                scheduledFile.Job.MarkCompleted();
        }

        manifestStore.Save(scheduledFile.Job.Manifest);
    }

    private void Skip(ScheduledFile scheduledFile, string reason)
    {
        Stat.RemoveFileFromTotalStat(scheduledFile.File, scheduledFile.Length);
        scheduledFile.Job.MarkFileSkipped(scheduledFile.Candidate, scheduledFile.Length);
        lock (syncRoot)
        {
            JobQueue queue = queues.Single(item => item.Job == scheduledFile.Job);
            if (queue.Files.Count == 0)
                scheduledFile.Job.MarkCompleted();
        }

        manifestStore.Save(scheduledFile.Job.Manifest);
        BackupLog.Raw($"({copyStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}) Файл пропущен = " +
            $"{scheduledFile.File.FullName} — {reason}");
    }

    private void RemoveQueue(BackupJob job)
    {
        lock (syncRoot)
            queues.RemoveAll(queue => queue.Job == job);
    }

    private static string FormatSize(long bytes)
    {
        double size = bytes;
        string[] units = { "байт", "КБ", "МБ", "ГБ", "ТБ" };
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:N2} {units[unitIndex]}";
    }

    private static bool CanReadSourceFile(FileInfo file)
    {
        try
        {
            using FileStream stream = new(file.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed class JobQueue
    {
        public JobQueue(BackupJob job)
        {
            Job = job;
            Files = new Queue<QueuedFile>(job.Files.Select(candidate =>
                new QueuedFile(candidate, candidate.Length)));
        }

        public BackupJob Job { get; }

        public Queue<QueuedFile> Files { get; }
    }

    private sealed record QueuedFile(BackupFileCandidate Candidate, long Length);
}

public sealed record ScheduledFile(BackupJob Job, BackupFileCandidate Candidate, long Length)
{
    public FileInfo File => Candidate.File;
}
