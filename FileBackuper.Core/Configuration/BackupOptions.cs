namespace FileBackuper.Core;

public sealed class BackupOptions
{
    public string DestinationDirectory { get; init; } = string.Empty;
    public string? ScanDirectory { get; init; } = null;
    public long MinFileSizeBytes { get; init; } = 10_000;
    public long MaxFileSizeBytes { get; init; } = 4_000_000_000;
    public bool IncludeBrowserCaches { get; init; } = false;
}
