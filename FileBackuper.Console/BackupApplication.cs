using System.Diagnostics;
using FileBackuper.Core;

namespace FileBackuper.Console;

internal class BackupApplication
{
    public void Run()
    {
        if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            return;

        string destinationDirectory = FileCopier.CreateDestinationDirectory();
        LoggingConfiguration.Configure(destinationDirectory);

        List<FileInfo> files = ScanFiles();
        long totalSize = files.Sum(file => file.Length);
        files = OrderFiles(files);
        CopyFiles(files, destinationDirectory, totalSize);
    }

    private static List<FileInfo> ScanFiles()
    {
        Stat.Start();
        BackupLog.Info("Начало сканирования");
        List<FileInfo> files = new();
        List<DriveInfo> drives = FileScanner.GetDrivesToScan();

        BackupLog.Info("Диски:");
        foreach (DriveInfo drive in drives)
        {
            BackupLog.Info($"   {drive.Name}");
            files.AddRange(FileScanner.Scan(drive.RootDirectory) ?? Enumerable.Empty<FileInfo>());
        }

        TimeSpan scanDuration = Stat.Stop();
        BackupLog.Info($"Время сканирования: {scanDuration:hh\\:mm\\:ss\\.ff}");
        BackupLog.Info($"Найдено файлов: {files.Count:N0}");
        BackupLog.Info($"Общий размер файлов: {files.Sum(file => file.Length):N0} байтов");
        BackupLog.Flush();
        return files;
    }

    private static List<FileInfo> OrderFiles(List<FileInfo> files)
    {
        Stat.Start();
        BackupLog.Info("Начало сортировки");
        List<FileInfo> orderedFiles = FilePriorityService.OrderByBackupPriority(files);
        TimeSpan sortDuration = Stat.Stop();
        BackupLog.Info($"Конец сортировки. Время сортировки: {sortDuration:hh\\:mm\\:ss\\.ff}");
        BackupLog.Flush();
        return orderedFiles;
    }

    private static void CopyFiles(IReadOnlyList<FileInfo> files, string destinationDirectory, long totalSize)
    {
        Stat.Start();
        BackupLog.Info("Начало копирования");
        FileCopier.CopyFiles(files, destinationDirectory);
        TimeSpan copyDuration = Stat.Stop();

        BackupLog.Info($"Время копирования: {copyDuration:hh\\:mm\\:ss\\.ff}");
        double copySpeed = totalSize / copyDuration.TotalSeconds;
        BackupLog.Info($"Скорость: {(copySpeed / 1024 / 1024):F2} Mb/s");
        BackupLog.Info($"          {(copySpeed / 1024 / 1024 / 1024 * 60):F2} Gb/min");
        BackupLog.Flush();
    }
}
