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
                Trace.TraceWarning("The drive {0} could not be read", drive.Name);
                continue;
            }

            if (string.Equals(destinationDrive, drive.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            drivesToScan.Add(drive);
        }

        return drivesToScan;
    }

    public static List<FileInfo> Scan(DirectoryInfo root, CancellationToken cancellationToken)
    {
        List<FileInfo> result = new();
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
                    if (ShouldSkipFile(file))
                        continue;

                    result.Add(file);
                    Trace.WriteLine(file.FullName);
                }

                foreach (DirectoryInfo subdirectory in directory.EnumerateDirectories("*", enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ShouldSkipDirectory(subdirectory.Name) ||
                        (subdirectory.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        continue;

                    directoriesToScan.Push(subdirectory);
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException ||
                                               exception is DirectoryNotFoundException ||
                                               exception is IOException)
            {
                Trace.TraceWarning($"Could not scan directory {directory.FullName}: {exception.Message}");
            }
        }

        return result;
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

    public static bool ShouldSkipFile(FileInfo file)
    {
        // TODO: добавить автоматические тесты для ограничений размера и blacklist видеофайлов.
        if (!MediaFileClassifier.IsImage(file) && !MediaFileClassifier.IsVideo(file)) return true;
        if (file.Length < 10_000 || file.Length > 4_000_000_000)
        {
            Trace.WriteLine($"Skip file by size ({file.Length}) - {file.Name}");
            return true;
        }

        string name = file.Name.ToLowerInvariant();
        bool isFilm = name.Contains("rip") || name.Contains("web") || name.Contains(".ts.") ||
            name.Contains(".org") || name.Contains("dub") || name.Contains("remux") ||
            name.Contains("season") || name.Contains("сезон") || name.Contains("xvid") ||
            name.Contains("720i") || name.Contains("720p") || name.Contains("1080i") || name.Contains("1080p");
        if (MediaFileClassifier.IsVideo(file) && isFilm)
        {
            Trace.WriteLine($"Skip file by film name - {file.Name}");
            return true;
        }

        return false;
    }
}
