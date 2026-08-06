using System.Diagnostics;

namespace FileBackuper.Core;

public static class FileCopier
{
    public static void CreateDestinationDirectory(string destinationDirectory)
    {
        Trace.TraceInformation($"Machine name: {Environment.MachineName}");
        Trace.TraceInformation($"Destination directory: {destinationDirectory}");
        Directory.CreateDirectory(destinationDirectory);
    }

    /// <summary>Copies one file and returns true when an up-to-date target already existed.</summary>
    public static bool CopyFile(FileInfo sourceFile, string destinationDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("The destination directory cannot be empty.", nameof(destinationDirectory));
        cancellationToken.ThrowIfCancellationRequested();

        string targetDirectory = destinationDirectory + "\\" + sourceFile.DirectoryName?.Replace(":", "");
        Directory.CreateDirectory(targetDirectory);
        string targetFile = Path.Combine(targetDirectory, sourceFile.Name);
        bool existsAndIsCurrent = File.Exists(targetFile) &&
            sourceFile.LastWriteTime <= File.GetLastWriteTime(targetFile);
        if (!existsAndIsCurrent)
            File.Copy(sourceFile.FullName, targetFile, overwrite: true);

        return existsAndIsCurrent;
    }
}
