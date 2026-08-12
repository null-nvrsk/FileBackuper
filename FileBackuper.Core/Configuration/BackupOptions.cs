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
    public bool EnableExifAnalysis { get; init; } = true;

    public List<FileSizeGroupOptions> FileSizeGroups { get; init; } = new()
    {
        new() { Name = "UpTo200KB", MinBytes = 10_000, MaxBytes = 204_800 },
        new() { Name = "UpTo500KB", MinBytes = 204_801, MaxBytes = 512_000 },
        new() { Name = "UpTo1MB", MinBytes = 512_001, MaxBytes = 1_048_576 },
        new() { Name = "UpTo5MB", MinBytes = 1_048_577, MaxBytes = 5_242_880 },
        new() { Name = "UpTo10MB", MinBytes = 5_242_881, MaxBytes = 10_485_760 },
        new() { Name = "UpTo15MB", MinBytes = 10_485_761, MaxBytes = 15_728_640 },
        new() { Name = "UpTo20MB", MinBytes = 15_728_641, MaxBytes = 20_971_520 },
        new() { Name = "UpTo50MB", MinBytes = 20_971_521, MaxBytes = 52_428_800 },
        new() { Name = "UpTo100MB", MinBytes = 52_428_801, MaxBytes = 104_857_600 },
        new() { Name = "UpTo150MB", MinBytes = 104_857_601, MaxBytes = 157_286_400 },
        new() { Name = "UpTo200MB", MinBytes = 157_286_401, MaxBytes = 209_715_200 },
        new() { Name = "UpTo250MB", MinBytes = 209_715_201, MaxBytes = 262_144_000 },
        new() { Name = "UpTo300MB", MinBytes = 262_144_001, MaxBytes = 314_572_800 },
        new() { Name = "UpTo400MB", MinBytes = 314_572_801, MaxBytes = 419_430_400 },
        new() { Name = "UpTo500MB", MinBytes = 419_430_401, MaxBytes = 524_288_000 },
        new() { Name = "UpTo750MB", MinBytes = 524_288_001, MaxBytes = 786_432_000 },
        new() { Name = "UpTo1GB", MinBytes = 786_432_001, MaxBytes = 1_073_741_824 },
        new() { Name = "UpTo1.5GB", MinBytes = 1_073_741_825, MaxBytes = 1_610_612_736 },
        new() { Name = "UpTo2GB", MinBytes = 1_610_612_737, MaxBytes = 2_147_483_648 },
        new() { Name = "UpTo2.5GB", MinBytes = 2_147_483_649, MaxBytes = 2_684_354_560 },
        new() { Name = "UpTo3GB", MinBytes = 2_684_354_561, MaxBytes = 3_221_225_472 },
        new() { Name = "UpTo3.5GB", MinBytes = 3_221_225_473, MaxBytes = 3_758_096_384 },
        new() { Name = "UpTo4GB", MinBytes = 3_758_096_385, MaxBytes = 4_000_000_000 }
    };

    public bool IncludeBrowserCaches { get; init; } = true;
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
