using System.Collections.Concurrent;
using System.Diagnostics;

namespace FileBackuper.Core;

/// <summary>
/// Discovers source volumes and prepares their backup jobs in the background.
/// A prepared job is ready for a copy scheduler, which is intentionally not part of this class.
/// </summary>
public sealed class BackupJobManager : IDisposable
{
    private readonly string stateDirectory;
    private readonly string destinationDirectory;
    private readonly JobManifestStore manifestStore;
    private readonly string instanceId;
    private readonly Func<DirectoryInfo, CancellationToken, List<FileInfo>> scanFiles;
    private readonly Func<IEnumerable<FileInfo>, CancellationToken, List<FileInfo>> orderFiles;
    private readonly ConcurrentDictionary<string, ManagedJob> jobs = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource stopSource = new();
    private bool disposed;

    public BackupJobManager(string stateDirectory, string destinationDirectory, JobManifestStore manifestStore,
        string instanceId,
        Func<DirectoryInfo, CancellationToken, List<FileInfo>>? scanFiles = null,
        Func<IEnumerable<FileInfo>, CancellationToken, List<FileInfo>>? orderFiles = null)
    {
        if (string.IsNullOrWhiteSpace(stateDirectory))
            throw new ArgumentException("The state directory cannot be empty.", nameof(stateDirectory));
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("The destination directory cannot be empty.", nameof(destinationDirectory));
        ArgumentNullException.ThrowIfNull(manifestStore);
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("The instance ID cannot be empty.", nameof(instanceId));

        this.stateDirectory = Path.GetFullPath(stateDirectory);
        this.destinationDirectory = Path.GetFullPath(destinationDirectory);
        this.manifestStore = manifestStore;
        this.instanceId = instanceId;
        this.scanFiles = scanFiles ?? FileScanner.Scan;
        this.orderFiles = orderFiles ?? FilePriorityService.OrderByBackupPriority;
    }

    public IReadOnlyList<BackupJob> Jobs => jobs.Values.Select(item => item.Job).ToList();

    public IReadOnlyList<BackupJob> ReadyJobs => jobs.Values
        .Where(item => item.PreparationTask.IsCompletedSuccessfully && item.Job.Status == JobStatus.Copying)
        .Select(item => item.Job)
        .ToList();

    public bool HasPreparingJobs => jobs.Values.Any(item =>
        item.Job.Status is JobStatus.Scanning or JobStatus.Sorting);

    public bool HasForeignWork { get; private set; }

    /// <summary>
    /// Starts background preparation for every newly available volume.
    /// Volumes completed in the shared state or locked by another process are skipped.
    /// </summary>
    public IReadOnlyList<BackupJob> DiscoverAndStartAdditionalJobs(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        HasForeignWork = false;
        List<BackupJob> startedJobs = new();
        foreach (DriveInfo drive in FileScanner.GetDrivesToScan(destinationDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            BackupJob? job = TryStart(drive, cancellationToken);
            if (job is not null)
                startedJobs.Add(job);
        }

        return startedJobs;
    }

    [Obsolete("Use DiscoverAndStartAdditionalJobs to make the purpose explicit.")]
    public IReadOnlyList<BackupJob> DiscoverAndStart(CancellationToken cancellationToken) =>
        DiscoverAndStartAdditionalJobs(cancellationToken);

    /// <summary>
    /// Prepares a single, globally sorted queue from all volumes available at startup.
    /// Scanning is performed in parallel, while sorting happens once for the combined file list.
    /// </summary>
    public async Task<BackupJobBatch?> DiscoverAndPrepareInitialBatchAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        HasForeignWork = false;
        List<BackupJob> startedJobs = new();
        foreach (DriveInfo drive in FileScanner.GetDrivesToScan(destinationDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            BackupJob? job = TryCreateJob(drive);
            if (job is null)
                continue;

            startedJobs.Add(job);
            jobs[job.Manifest.VolumeId].PreparationTask = Task.Run(() => ScanJob(job, cancellationToken));
        }

        if (startedJobs.Count == 0)
            return null;

        Task[] scanTasks = startedJobs
            .Select(job => jobs[job.Manifest.VolumeId].PreparationTask)
            .ToArray();
        await Task.WhenAll(scanTasks).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        BackupJobBatch batch = new(startedJobs);
        batch.CollectScannedFiles();
        Stopwatch sortingStopwatch = Stopwatch.StartNew();
        BackupLog.Info($"Начало сортировки общей очереди: {batch.Files.Count:N0} файлов.");
        List<FileInfo> sortedFiles = orderFiles(batch.Files, cancellationToken);
        sortingStopwatch.Stop();
        BackupLog.Info($"Конец сортировки. Время сортировки: " +
            $"{sortingStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}");
        batch.SetSortedFiles(sortedFiles);
        foreach (BackupJob job in batch.Jobs)
            manifestStore.Save(job.Manifest);

        return batch;
    }

    public Task? GetPreparationTask(string volumeId)
    {
        if (string.IsNullOrWhiteSpace(volumeId))
            throw new ArgumentException("The volume ID cannot be empty.", nameof(volumeId));

        return jobs.TryGetValue(volumeId, out ManagedJob? managedJob)
            ? managedJob.PreparationTask
            : null;
    }

    public async Task StopAsync()
    {
        stopSource.Cancel();
        Task[] preparationTasks = jobs.Values.Select(item => item.PreparationTask).ToArray();
        await Task.WhenAll(preparationTasks).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        StopAsync().GetAwaiter().GetResult();
        disposed = true;
        stopSource.Dispose();
        foreach (ManagedJob managedJob in jobs.Values)
            managedJob.Job.Dispose();
    }

    private BackupJob? TryStart(DriveInfo drive, CancellationToken cancellationToken)
    {
        BackupJob? job = TryCreateJob(drive);
        if (job is null)
            return null;

        jobs[job.Manifest.VolumeId].PreparationTask = Task.Run(() => PrepareJob(job, cancellationToken));
        return job;
    }

    private BackupJob? TryCreateJob(DriveInfo drive)
    {
        string volumeId = VolumeIdentity.GetVolumeId(drive);
        if (jobs.ContainsKey(volumeId) || manifestStore.Read(volumeId)?.Status == JobStatus.Completed)
            return null;

        VolumeLease? lease = VolumeLease.TryAcquire(stateDirectory, volumeId);
        if (lease is null)
        {
            HasForeignWork = true;
            return null;
        }

        JobManifest manifest = CreateManifest(volumeId, drive.Name);
        BackupJob job = new(drive, lease, manifest);
        ManagedJob managedJob = new(job, Task.CompletedTask);
        if (!jobs.TryAdd(volumeId, managedJob))
        {
            job.Dispose();
            return null;
        }

        try
        {
            Stat.RegisterSourceDrive(drive.Name);
            manifestStore.Save(job.Manifest);
            return job;
        }
        catch
        {
            jobs.TryRemove(volumeId, out _);
            job.Dispose();
            throw;
        }
    }

    private void PrepareJob(BackupJob job, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, stopSource.Token);
        CancellationToken token = linkedSource.Token;

        try
        {
            List<FileInfo> scannedFiles = ScanAndRecord(job, token);
            Stopwatch sortingStopwatch = Stopwatch.StartNew();
            BackupLog.Info($"Начало сортировки файлов диска {job.SourceDrive.Name}");
            List<FileInfo> sortedFiles = orderFiles(scannedFiles, token);
            sortingStopwatch.Stop();
            BackupLog.Info($"Сортировка файлов диска {job.SourceDrive.Name} завершена. Время сортировки: " +
                $"{sortingStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}");
            job.SetSortedFiles(sortedFiles);
            manifestStore.Save(job.Manifest);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            job.MarkCancelled();
            manifestStore.Save(job.Manifest);
        }
        catch (Exception exception)
        {
            job.MarkFailed(exception.Message);
            manifestStore.Save(job.Manifest);
        }
    }

    private void ScanJob(BackupJob job, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, stopSource.Token);
        CancellationToken token = linkedSource.Token;

        try
        {
            ScanAndRecord(job, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            job.MarkCancelled();
            manifestStore.Save(job.Manifest);
        }
        catch (Exception exception)
        {
            job.MarkFailed(exception.Message);
            manifestStore.Save(job.Manifest);
        }
    }

    private List<FileInfo> ScanAndRecord(BackupJob job, CancellationToken cancellationToken)
    {
        Stopwatch scanningStopwatch = Stopwatch.StartNew();
        BackupLog.Info($"Начало сканирования диска {job.SourceDrive.Name}");
        List<FileInfo> scannedFiles = scanFiles(job.SourceDrive.RootDirectory, cancellationToken);
        scanningStopwatch.Stop();

        Stat.AddFilesToTotalStat(scannedFiles);
        job.SetScannedFiles(scannedFiles);
        manifestStore.Save(job.Manifest);

        BackupLog.Info($"Сканирование диска {job.SourceDrive.Name} завершено.");
        BackupLog.Info($"Время сканирования: {scanningStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}");
        BackupLog.Info($"Найдено файлов: {scannedFiles.Count:N0}");
        BackupLog.Info($"Общий размер файлов: {scannedFiles.Sum(file => file.Length):N0} байтов");
        BackupLog.Flush();
        return scannedFiles;
    }

    private JobManifest CreateManifest(string volumeId, string driveLetter)
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

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(BackupJobManager));
    }

    private sealed class ManagedJob
    {
        public ManagedJob(BackupJob job, Task preparationTask)
        {
            Job = job;
            PreparationTask = preparationTask;
        }

        public BackupJob Job { get; }

        public Task PreparationTask { get; set; }
    }
}
