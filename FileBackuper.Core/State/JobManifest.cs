namespace FileBackuper.Core;

public sealed class JobManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string VolumeId { get; init; } = string.Empty;
    public string CurrentDriveLetter { get; init; } = string.Empty;
    public JobStatus Status { get; init; } = JobStatus.Pending;
    public string? OwnerInstanceId { get; init; }
    public int? OwnerProcessId { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset LastHeartbeatUtc { get; init; }
    public string DestinationDirectory { get; init; } = string.Empty;
    public long FilesFound { get; init; }
    public long TotalBytes { get; init; }
    public long FilesCompleted { get; init; }
    public long CompletedBytes { get; init; }
    public string? CurrentFile { get; init; }
    public string? LastError { get; init; }
}
