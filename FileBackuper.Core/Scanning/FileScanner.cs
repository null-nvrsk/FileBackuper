namespace FileBackuper.Core;

public static class FileScanner
{
    private static readonly IReadOnlySet<string> DefaultSkipDirectoryNames = new HashSet<string>(
        new[] { "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData" },
        StringComparer.OrdinalIgnoreCase);

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

    public static List<FileInfo> Scan(DirectoryInfo root, CancellationToken cancellationToken,
        IEnumerable<string>? skipDirectoryNames = null) =>
        ScanWithStatistics(root, cancellationToken, skipDirectoryNames).Files;

    public static FileScanResult ScanWithStatistics(DirectoryInfo root, CancellationToken cancellationToken,
        IEnumerable<string>? skipDirectoryNames = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        HashSet<string> skippedDirectories = new(skipDirectoryNames ?? DefaultSkipDirectoryNames,
            StringComparer.OrdinalIgnoreCase);
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

                    result.Add(file);
                    BackupLog.Verbose($"Найден файл: {file.FullName}");
                }

                foreach (DirectoryInfo subdirectory in directory.EnumerateDirectories("*", enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ShouldSkipDirectory(subdirectory.Name, skippedDirectories) || IsFileSystemLink(subdirectory))
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

    public static bool ShouldSkipDirectory(string directoryName) =>
        ShouldSkipDirectory(directoryName, DefaultSkipDirectoryNames);

    public static bool ShouldSkipDirectory(string directoryName, IReadOnlySet<string> skipDirectoryNames)
    {
        if (string.IsNullOrWhiteSpace(directoryName))
            throw new ArgumentException("The directory name cannot be empty.", nameof(directoryName));
        ArgumentNullException.ThrowIfNull(skipDirectoryNames);
        return skipDirectoryNames.Contains(directoryName);
    }

    private static bool IsFileSystemLink(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) == 0)
            return false;

        // Cloud Files providers (including Yandex Disk 4) also mark placeholder
        // directories as reparse points. Unlike symbolic links and junctions,
        // they do not have a link target and must be traversed normally.
        return directory.LinkTarget is not null;
    }

}
