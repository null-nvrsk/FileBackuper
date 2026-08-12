namespace FileBackuper.Core;

public static class FileCopier
{
    public static void CreateDestinationDirectory(string destinationDirectory)
    {
        BackupLog.Info($"Имя компьютера: {Environment.MachineName}");
        BackupLog.Info($"Каталог назначения: {destinationDirectory}");
        Directory.CreateDirectory(destinationDirectory);
    }

    /// <summary>Copies one file and returns true when an up-to-date target already existed.</summary>
    public static bool CopyFile(FileInfo sourceFile, string destinationDirectory, CancellationToken cancellationToken)
        => CopyFile(sourceFile, destinationDirectory, detectedExtension: null, cancellationToken);

    /// <summary>
    /// Copies a classified file. For an extensionless source, the detected media extension is appended
    /// to the destination file name.
    /// </summary>
    public static bool CopyFile(BackupFileCandidate candidate, string destinationDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return CopyFile(candidate.File, destinationDirectory, candidate.Analysis.DetectedExtension,
            cancellationToken);
    }

    private static bool CopyFile(FileInfo sourceFile, string destinationDirectory, string? detectedExtension,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("Каталог назначения не может быть пустым.", nameof(destinationDirectory));
        cancellationToken.ThrowIfCancellationRequested();

        string targetDirectory = destinationDirectory + "\\" + sourceFile.DirectoryName?.Replace(":", "");
        Directory.CreateDirectory(targetDirectory);
        string targetFile = Path.Combine(targetDirectory, GetTargetFileName(sourceFile, detectedExtension));
        bool existsAndIsCurrent = File.Exists(targetFile) &&
            sourceFile.LastWriteTime <= File.GetLastWriteTime(targetFile);
        if (!existsAndIsCurrent)
            File.Copy(sourceFile.FullName, targetFile, overwrite: true);

        return existsAndIsCurrent;
    }

    private static string GetTargetFileName(FileInfo sourceFile, string? detectedExtension)
    {
        if (!string.IsNullOrEmpty(sourceFile.Extension) || string.IsNullOrWhiteSpace(detectedExtension))
            return sourceFile.Name;

        string extension = detectedExtension.Trim();
        if (!extension.StartsWith(".", StringComparison.Ordinal))
            extension = "." + extension;
        if (extension.Length == 1 || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            extension.Contains(Path.DirectorySeparatorChar) || extension.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("The detected file extension is invalid.", nameof(detectedExtension));
        }

        return sourceFile.Name + extension.ToLowerInvariant();
    }
}
