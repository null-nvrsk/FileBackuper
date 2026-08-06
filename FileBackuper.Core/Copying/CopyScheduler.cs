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

    public CopyScheduler(JobManifestStore manifestStore)
    {
        ArgumentNullException.ThrowIfNull(manifestStore);
        this.manifestStore = manifestStore;
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
            Enqueue(job);
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
        try
        {
            bool alreadyExists = FileCopier.CopyFile(scheduledFile.File, destinationDirectory, cancellationToken);
            Stat.AddFileToCompletedStat(scheduledFile.File);
            Stat.RecalculateEstimatedTime();
            Complete(scheduledFile);
            BackupLog.Info($"Copy file = {scheduledFile.File.FullName} - " +
                (alreadyExists ? "file already exists, skipped" : $"size {scheduledFile.File.Length:N0}"));
        }
        catch (OperationCanceledException)
        {
            scheduledFile.Job.MarkCancelled();
            manifestStore.Save(scheduledFile.Job.Manifest);
            throw;
        }
        catch (Exception exception)
        {
            scheduledFile.Job.MarkFailed(exception.Message);
            manifestStore.Save(scheduledFile.Job.Manifest);
            RemoveQueue(scheduledFile.Job);
        }

        await Task.Yield();
        return true;
    }

    public bool TryTakeNext(out ScheduledFile scheduledFile)
    {
        lock (syncRoot)
        {
            JobQueue? selectedQueue = queues
                .Where(queue => queue.Files.Count > 0)
                .OrderBy(queue => queue.Files.Peek(), FilePriorityService.BackupPriorityComparer)
                .FirstOrDefault();

            if (selectedQueue is null)
            {
                scheduledFile = null!;
                return false;
            }

            scheduledFile = new ScheduledFile(selectedQueue.Job, selectedQueue.Files.Dequeue());
            return true;
        }
    }

    private void Complete(ScheduledFile scheduledFile)
    {
        scheduledFile.Job.MarkFileCopied(scheduledFile.File);
        lock (syncRoot)
        {
            JobQueue queue = queues.Single(item => item.Job == scheduledFile.Job);
            if (queue.Files.Count == 0)
                scheduledFile.Job.MarkCompleted();
        }

        manifestStore.Save(scheduledFile.Job.Manifest);
    }

    private void RemoveQueue(BackupJob job)
    {
        lock (syncRoot)
            queues.RemoveAll(queue => queue.Job == job);
    }

    private sealed class JobQueue
    {
        public JobQueue(BackupJob job)
        {
            Job = job;
            Files = new Queue<FileInfo>(job.Files);
        }

        public BackupJob Job { get; }

        public Queue<FileInfo> Files { get; }
    }
}

public sealed record ScheduledFile(BackupJob Job, FileInfo File);
