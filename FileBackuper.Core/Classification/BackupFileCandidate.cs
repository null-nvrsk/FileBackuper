namespace FileBackuper.Core;

public sealed record BackupFileCandidate
{
    public BackupFileCandidate(FileInfo file, MediaFileAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(analysis);
        File = file;
        Analysis = analysis;
        Length = analysis.FileSizeBytes ?? file.Length;
    }

    public FileInfo File { get; }

    public MediaFileAnalysis Analysis { get; }

    public long Length { get; }
}
