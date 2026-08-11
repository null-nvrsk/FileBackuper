namespace FileBackuper.Core;

public sealed record BackupFileCandidate
{
    public BackupFileCandidate(FileInfo file, MediaFileAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(analysis);
        File = file;
        Analysis = analysis;
    }

    public FileInfo File { get; }

    public MediaFileAnalysis Analysis { get; }
}
