using System.Diagnostics;

namespace FileBackuper.Core;

public static class FileScanner
{
    // TODO: заменить рекурсивный Scan на итеративный обход через Stack<DirectoryInfo>
    // и EnumerationOptions { IgnoreInaccessible = true }, чтобы глубокие или недоступные папки
    // не приводили к переполнению стека и не останавливали сканирование диска.
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

    public static List<FileInfo>? Scan(DirectoryInfo root)
    {
        List<FileInfo> result = new();
        FileInfo[] files;
        try { files = root.GetFiles(); }
        catch (Exception exception)
        {
            Trace.TraceWarning(exception.Message);
            return null;
        }

        foreach (FileInfo file in files)
        {
            if (ShouldSkipFile(file)) continue;
            result.Add(file);
            Trace.WriteLine(file.FullName);
        }

        DirectoryInfo[] subdirectories;
        try { subdirectories = root.GetDirectories(); }
        catch (Exception exception)
        {
            Trace.TraceWarning(exception.Message);
            return result;
        }

        foreach (DirectoryInfo directory in subdirectories)
        {
            if (ShouldSkipDirectory(directory.Name) ||
                (directory.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                continue;

            List<FileInfo>? nestedFiles = Scan(directory);
            if (nestedFiles != null) result.AddRange(nestedFiles);
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
