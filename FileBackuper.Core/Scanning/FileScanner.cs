using System.Diagnostics;

namespace FileBackuper.Core;

public static class FileScanner
{
    public static List<DriveInfo> GetDrivesToScan(string destinationDirectory)
    {
        List<DriveInfo> drivesToScan = new();
        string destinationDrive = Path.GetPathRoot(destinationDirectory)
            ?? throw new ArgumentException("Не удалось определить диск назначения.", nameof(destinationDirectory));
        foreach (string driveName in Environment.GetLogicalDrives())
        {
            DriveInfo drive = new(driveName);
            if (!drive.IsReady)
            {
                BackupLog.Warning($"Не удалось прочитать диск {drive.Name}");
                continue;
            }

            if (string.Equals(destinationDrive, drive.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            drivesToScan.Add(drive);
        }

        return drivesToScan;
    }

    public static List<FileInfo> Scan(DirectoryInfo root, CancellationToken cancellationToken)
        => ScanWithStatistics(root, cancellationToken).Files;

    public static FileScanResult ScanWithStatistics(DirectoryInfo root, CancellationToken cancellationToken)
    {
        List<FileInfo> result = new();
        int cloudFilesSkipped = 0;
        Stack<DirectoryInfo> directoriesToScan = new();
        directoriesToScan.Push(root);

        EnumerationOptions enumerationOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        while (directoriesToScan.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo directory = directoriesToScan.Pop();

            try
            {
                foreach (FileInfo file in directory.EnumerateFiles("*", enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!MediaFileClassifier.IsImage(file) && !MediaFileClassifier.IsVideo(file))
                        continue;

                    try
                    {
                        if (!CloudFileState.IsContentAvailableLocally(file))
                        {
                            cloudFilesSkipped++;
                            BackupLog.Verbose($"Облачный файл пропущен без скачивания: {file.FullName}");
                            continue;
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                       System.ComponentModel.Win32Exception)
                    {
                        cloudFilesSkipped++;
                        BackupLog.Warning($"Не удалось определить локальную доступность файла {file.FullName}. " +
                            "Файл пропущен, чтобы не запустить его скачивание. " +
                            BackupLog.GetExceptionDescription(exception));
                        continue;
                    }

                    if (ShouldSkipFile(file))
                        continue;

                    result.Add(file);
                    Trace.WriteLine(file.FullName);
                }

                foreach (DirectoryInfo subdirectory in directory.EnumerateDirectories("*", enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ShouldSkipDirectory(subdirectory.Name) || IsFileSystemLink(subdirectory))
                        continue;

                    directoriesToScan.Push(subdirectory);
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException ||
                                               exception is DirectoryNotFoundException ||
                                               exception is IOException)
            {
                BackupLog.Warning($"Не удалось просканировать каталог {directory.FullName}. " +
                    BackupLog.GetExceptionDescription(exception));
            }
        }

        return new FileScanResult(result, cloudFilesSkipped);
    }

    public static bool ShouldSkipDirectory(string directoryName) => directoryName switch
    {
        "Windows" => true,
        "Program Files" => true,
        "Program Files (x86)" => true,
        "ProgramData" => true,
#if RELEASE
        "AppData" => true,
#endif
        _ => false
    };

    private static bool IsFileSystemLink(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) == 0)
            return false;

        // Cloud Files providers (including Yandex Disk 4) also mark placeholder
        // directories as reparse points. Unlike symbolic links and junctions,
        // they do not have a link target and must be traversed normally.
        return directory.LinkTarget is not null;
    }

    public static bool ShouldSkipFile(FileInfo file)
    {
        // TODO: добавить автоматические тесты для ограничений размера и blacklist видеофайлов.
        if (!MediaFileClassifier.IsImage(file) && !MediaFileClassifier.IsVideo(file)) return true;
        if (file.Length < 10_000 || file.Length > 4_000_000_000)
        {
            BackupLog.Verbose($"Файл пропущен из-за размера ({file.Length:N0} байтов): {file.Name}");
            return true;
        }

        string name = file.Name.ToLowerInvariant();
        bool isFilm = name.Contains("rip") || name.Contains("web") || name.Contains(".ts.") ||
            name.Contains(".org") || name.Contains("dub") || name.Contains("remux") ||
            name.Contains("season") || name.Contains("сезон") || name.Contains("xvid") ||
            name.Contains("720i") || name.Contains("720p") || name.Contains("1080i") || name.Contains("1080p");
        if (MediaFileClassifier.IsVideo(file) && isFilm)
        {
            Trace.WriteLine($"Видеофайл пропущен по признакам фильма: {file.Name}");
            return true;
        }

        return false;
    }
}
