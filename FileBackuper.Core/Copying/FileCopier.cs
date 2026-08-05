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

    public static void CopyFiles(IReadOnlyList<FileInfo> sourceFiles, string destinationDirectory,
        CancellationToken cancellationToken)
    {
        DateTime startedAt = DateTime.Now;
        int copiedFileCount = 0;
        long completedBytes = 0;
        long totalBytes = sourceFiles.Sum(file => file.Length);

        foreach (FileInfo sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string targetDirectory = destinationDirectory + "\\" + sourceFile.DirectoryName?.Replace(":", "");
            Directory.CreateDirectory(targetDirectory);
            try
            {
                string targetFile = Path.Combine(targetDirectory, sourceFile.Name);
                completedBytes += sourceFile.Length;
                long percent = completedBytes * 100 / totalBytes;
                double completedGigabytes = completedBytes / 1073741824.0;
                bool existsAndIsCurrent = File.Exists(targetFile) && sourceFile.LastWriteTime <= File.GetLastWriteTime(targetFile);
                if (!existsAndIsCurrent) File.Copy(sourceFile.FullName, targetFile, overwrite: true);

                string action = existsAndIsCurrent ? "файл уже существует, пропускаем" : $"size {FormatSize(sourceFile.Length)}";
                Trace.TraceInformation($"[{(DateTime.Now - startedAt):hh\\:mm\\:ss\\.ff}]" +
                    $"[Copied {completedGigabytes:F2} GB ({percent}%)] Copy file #{++copiedFileCount:N0} = " +
                    $"{sourceFile.FullName} - {action}");
                Stat.AddFileToCompletedStat(sourceFile);
                Stat.RecalculateEstimatedTime();
            }
            catch (Exception exception) { Trace.TraceWarning($"[{DateTime.Now - startedAt}] {exception.Message}"); }
        }
    }

    private static string FormatSize(long bytes)
    {
        double size = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1) { size /= 1024; unitIndex++; }
        return $"{size:N2} {units[unitIndex]}";
    }
}
