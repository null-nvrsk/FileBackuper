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

    public List<FileSizeGroupOptions> FileSizeGroups { get; init; } = new()
    {
        new() { Name = "Small", MinBytes = 10_000, MaxBytes = 204_800 },
        new() { Name = "Medium", MinBytes = 204_801, MaxBytes = 10_485_760 },
        new() { Name = "Large", MinBytes = 10_485_761, MaxBytes = 104_857_600 },
        new() { Name = "VeryLarge", MinBytes = 104_857_601, MaxBytes = 1_073_741_824 },
        new() { Name = "Huge", MinBytes = 1_073_741_825, MaxBytes = 4_000_000_000 }
    };

    public bool IncludeBrowserCaches { get; init; } = false;
    public CloudFileMode CloudFileMode { get; init; } = CloudFileMode.FastSkip;

    public List<string> SkipDirectoryNames { get; init; } = new()
    {
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData",
        "AppData"
    };
}
