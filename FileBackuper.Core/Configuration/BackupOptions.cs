namespace FileBackuper.Core;

public sealed class BackupOptions
{
    public bool ShowConsole { get; init; } = false;

    public string StateDirectory { get; init; } = string.Empty;

    public int DrivePollingIntervalSeconds { get; init; } = 5;

    public bool MonitorNewDrives { get; init; } = true;

    public string DestinationDirectory { get; init; } = string.Empty;
    public string? ScanDirectory { get; init; } = null;
    public long MinFileSizeBytes { get; init; } = 10_000;
    public long MaxFileSizeBytes { get; init; } = 4_000_000_000;
    public bool IncludeBrowserCaches { get; init; } = false;
    public CloudFileMode CloudFileMode { get; init; } = CloudFileMode.FastSkip;
}
