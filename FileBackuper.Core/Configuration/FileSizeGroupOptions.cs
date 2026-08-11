namespace FileBackuper.Core;

public sealed class FileSizeGroupOptions
{
    public string Name { get; init; } = string.Empty;

    public long MinBytes { get; init; }

    public long MaxBytes { get; init; }
}
