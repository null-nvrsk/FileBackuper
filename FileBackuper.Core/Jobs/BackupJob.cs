namespace FileBackuper.Core;

/// <summary>
/// Represents the work performed for one source volume within a backup run.
/// The job owns its volume lease until it is disposed.
/// </summary>
public sealed class BackupJob : IDisposable
{
    private readonly List<BackupFileCandidate> files = new();

    public BackupJob(DriveInfo sourceDrive, VolumeLease lease, JobManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(sourceDrive);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(manifest.VolumeId))
            throw new ArgumentException("The manifest must specify a volume ID.", nameof(manifest));
        if (!string.Equals(lease.VolumeId, manifest.VolumeId, StringComparison.Ordinal))
            throw new ArgumentException("The lease and manifest must belong to the same volume.", nameof(manifest));

        SourceDrive = sourceDrive;
        Lease = lease;
        Manifest = manifest;
    }

    public DriveInfo SourceDrive { get; }

    public VolumeLease Lease { get; }

    public JobManifest Manifest { get; private set; }

    public JobStatus Status => Manifest.Status;

    public IReadOnlyList<BackupFileCandidate> Files => files;

    public MediaAnalysisStatistics AnalysisStatistics { get; private set; } = MediaAnalysisStatistics.Empty;

    public void SetAnalysisStatistics(MediaAnalysisStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        AnalysisStatistics = statistics;
    }

    public void SetScannedFiles(IEnumerable<BackupFileCandidate> scannedFiles)
    {
        ArgumentNullException.ThrowIfNull(scannedFiles);
        ReplaceFiles(scannedFiles);

        Manifest = UpdateManifest(JobStatus.Sorting,
            filesFound: files.Count,
            totalBytes: files.Sum(candidate => candidate.Length));
    }

    public void SetSortedFiles(IEnumerable<BackupFileCandidate> sortedFiles)
    {
        ArgumentNullException.ThrowIfNull(sortedFiles);
        ReplaceFiles(sortedFiles);
        Manifest = UpdateManifest(JobStatus.Copying);
    }

    public void MarkFileCopied(BackupFileCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        FileInfo file = candidate.File;
        Manifest = UpdateManifest(JobStatus.Copying,
            filesCompleted: Manifest.FilesCompleted + 1,
            completedBytes: Manifest.CompletedBytes + candidate.Length,
            currentFile: file.FullName);
    }

    public void MarkFileSkipped(BackupFileCandidate candidate, long fileSize)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        FileInfo file = candidate.File;
        if (fileSize < 0)
            throw new ArgumentOutOfRangeException(nameof(fileSize));

        Manifest = UpdateManifest(JobStatus.Copying,
            filesFound: Math.Max(0, Manifest.FilesFound - 1),
            totalBytes: Math.Max(0, Manifest.TotalBytes - fileSize),
            currentFile: file.FullName);
    }

    public void MarkCompleted() => Manifest = UpdateManifest(JobStatus.Completed);

    public void MarkCancelled() => Manifest = UpdateManifest(JobStatus.Cancelled);

    public void MarkFailed(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("The error message cannot be empty.", nameof(error));
        Manifest = UpdateManifest(JobStatus.Failed, lastError: error);
    }

    public void Dispose() => Lease.Dispose();

    private void ReplaceFiles(IEnumerable<BackupFileCandidate> newFiles)
    {
        files.Clear();
        foreach (BackupFileCandidate candidate in newFiles)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            files.Add(candidate);
        }
    }

    private JobManifest UpdateManifest(JobStatus status, long? filesFound = null, long? totalBytes = null,
        long? filesCompleted = null, long? completedBytes = null, string? currentFile = null, string? lastError = null)
    {
        return new JobManifest
        {
            SchemaVersion = Manifest.SchemaVersion,
            VolumeId = Manifest.VolumeId,
            CurrentDriveLetter = Manifest.CurrentDriveLetter,
            Status = status,
            OwnerInstanceId = Manifest.OwnerInstanceId,
            OwnerProcessId = Manifest.OwnerProcessId,
            StartedUtc = Manifest.StartedUtc,
            LastHeartbeatUtc = DateTimeOffset.UtcNow,
            DestinationDirectory = Manifest.DestinationDirectory,
            FilesFound = filesFound ?? Manifest.FilesFound,
            TotalBytes = totalBytes ?? Manifest.TotalBytes,
            FilesCompleted = filesCompleted ?? Manifest.FilesCompleted,
            CompletedBytes = completedBytes ?? Manifest.CompletedBytes,
            CurrentFile = currentFile ?? Manifest.CurrentFile,
            LastError = lastError ?? Manifest.LastError
        };
    }
}
