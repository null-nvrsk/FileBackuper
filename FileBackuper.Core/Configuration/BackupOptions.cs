namespace FileBackuper.Core;

public sealed class BackupOptions
{
    public bool ShowConsole { get; init; } = false;

    public string StateDirectory { get; init; } = string.Empty;

    public int DrivePollingIntervalSeconds { get; init; } = 5;

    public bool MonitorNewDrives { get; init; } = true;

    public string DestinationDirectory { get; init; } = string.Empty;
    public string? ScanDirectory { get; init; } = null;
    public long MinFileSizeBytes { get; init; } = 10 * 1024;
    public long MaxFileSizeBytes { get; init; } = 4L * 1024 * 1024 * 1024;
    public bool EnableExifAnalysis { get; init; } = true;

    public List<FileSizeGroupOptions> FileSizeGroups { get; init; } = new()
    {
        new() { Name = "From10KBTo200KB", MinBytes = 10_240, MaxBytes = 204_800 },
        new() { Name = "From200KBTo500KB", MinBytes = 204_801, MaxBytes = 512_000 },
        new() { Name = "From500KBTo1MB", MinBytes = 512_001, MaxBytes = 1_048_576 },
        new() { Name = "From1MBTo2MB", MinBytes = 1_048_577, MaxBytes = 2_097_152 },
        new() { Name = "From2MBTo3MB", MinBytes = 2_097_153, MaxBytes = 3_145_728 },
        new() { Name = "From3MBTo4MB", MinBytes = 3_145_729, MaxBytes = 4_194_304 },
        new() { Name = "From4MBTo5MB", MinBytes = 4_194_305, MaxBytes = 5_242_880 },
        new() { Name = "From5MBTo6MB", MinBytes = 5_242_881, MaxBytes = 6_291_456 },
        new() { Name = "From6MBTo7MB", MinBytes = 6_291_457, MaxBytes = 7_340_032 },
        new() { Name = "From7MBTo8MB", MinBytes = 7_340_033, MaxBytes = 8_388_608 },
        new() { Name = "From8MBTo9MB", MinBytes = 8_388_609, MaxBytes = 9_437_184 },
        new() { Name = "From9MBTo10MB", MinBytes = 9_437_185, MaxBytes = 10_485_760 },
        new() { Name = "From10MBTo12.5MB", MinBytes = 10_485_761, MaxBytes = 13_107_200 },
        new() { Name = "From12.5MBTo15MB", MinBytes = 13_107_201, MaxBytes = 15_728_640 },
        new() { Name = "From15MBTo20MB", MinBytes = 15_728_641, MaxBytes = 20_971_520 },
        new() { Name = "From20MBTo30MB", MinBytes = 20_971_521, MaxBytes = 31_457_280 },
        new() { Name = "From30MBTo40MB", MinBytes = 31_457_281, MaxBytes = 41_943_040 },
        new() { Name = "From40MBTo50MB", MinBytes = 41_943_041, MaxBytes = 52_428_800 },
        new() { Name = "From50MBTo60MB", MinBytes = 52_428_801, MaxBytes = 62_914_560 },
        new() { Name = "From60MBTo70MB", MinBytes = 62_914_561, MaxBytes = 73_400_320 },
        new() { Name = "From70MBTo80MB", MinBytes = 73_400_321, MaxBytes = 83_886_080 },
        new() { Name = "From80MBTo90MB", MinBytes = 83_886_081, MaxBytes = 94_371_840 },
        new() { Name = "From90MBTo100MB", MinBytes = 94_371_841, MaxBytes = 104_857_600 },
        new() { Name = "From100MBTo120MB", MinBytes = 104_857_601, MaxBytes = 125_829_120 },
        new() { Name = "From120MBTo140MB", MinBytes = 125_829_121, MaxBytes = 146_800_640 },
        new() { Name = "From140MBTo160MB", MinBytes = 146_800_641, MaxBytes = 167_772_160 },
        new() { Name = "From160MBTo180MB", MinBytes = 167_772_161, MaxBytes = 188_743_680 },
        new() { Name = "From180MBTo200MB", MinBytes = 188_743_681, MaxBytes = 209_715_200 },
        new() { Name = "From200MBTo225MB", MinBytes = 209_715_201, MaxBytes = 235_929_600 },
        new() { Name = "From225MBTo250MB", MinBytes = 235_929_601, MaxBytes = 262_144_000 },
        new() { Name = "From250MBTo275MB", MinBytes = 262_144_001, MaxBytes = 288_358_400 },
        new() { Name = "From275MBTo300MB", MinBytes = 288_358_401, MaxBytes = 314_572_800 },
        new() { Name = "From300MBTo350MB", MinBytes = 314_572_801, MaxBytes = 367_001_600 },
        new() { Name = "From350MBTo400MB", MinBytes = 367_001_601, MaxBytes = 419_430_400 },
        new() { Name = "From400MBTo450MB", MinBytes = 419_430_401, MaxBytes = 471_859_200 },
        new() { Name = "From450MBTo500MB", MinBytes = 471_859_201, MaxBytes = 524_288_000 },
        new() { Name = "From500MBTo600MB", MinBytes = 524_288_001, MaxBytes = 629_145_600 },
        new() { Name = "From600MBTo700MB", MinBytes = 629_145_601, MaxBytes = 734_003_200 },
        new() { Name = "From700MBTo800MB", MinBytes = 734_003_201, MaxBytes = 838_860_800 },
        new() { Name = "From800MBTo900MB", MinBytes = 838_860_801, MaxBytes = 943_718_400 },
        new() { Name = "From900MBTo1GB", MinBytes = 943_718_401, MaxBytes = 1_073_741_824 },
        new() { Name = "From1GBTo1.2GB", MinBytes = 1_073_741_825, MaxBytes = 1_288_490_189 },
        new() { Name = "From1.2GBTo1.4GB", MinBytes = 1_288_490_190, MaxBytes = 1_503_238_554 },
        new() { Name = "From1.4GBTo1.6GB", MinBytes = 1_503_238_555, MaxBytes = 1_717_986_918 },
        new() { Name = "From1.6GBTo1.8GB", MinBytes = 1_717_986_919, MaxBytes = 1_932_735_283 },
        new() { Name = "From1.8GBTo2GB", MinBytes = 1_932_735_284, MaxBytes = 2_147_483_648 },
        new() { Name = "From2GBTo2.25GB", MinBytes = 2_147_483_649, MaxBytes = 2_415_919_104 },
        new() { Name = "From2.25GBTo2.5GB", MinBytes = 2_415_919_105, MaxBytes = 2_684_354_560 },
        new() { Name = "From2.5GBTo2.75GB", MinBytes = 2_684_354_561, MaxBytes = 2_952_790_016 },
        new() { Name = "From2.75GBTo3GB", MinBytes = 2_952_790_017, MaxBytes = 3_221_225_472 },
        new() { Name = "From3GBTo3.5GB", MinBytes = 3_221_225_473, MaxBytes = 3_758_096_384 },
        new() { Name = "From3.5GBTo4GB", MinBytes = 3_758_096_385, MaxBytes = 4_294_967_296 }
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
