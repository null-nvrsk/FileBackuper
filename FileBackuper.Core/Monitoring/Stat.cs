namespace FileBackuper.Core;

public static class Stat
{
    private static readonly object syncRoot = new();
    private static DateTime startTime;
    private static DateTime endTime;
    private static TimeSpan? imagesEta;
    private static TimeSpan totalEta;
    private static DateTime lastRecalculatedAt;
    private static int totalCount;
    private static int completeCount;
    private static long totalSize;
    private static long completeSize;
    private static long totalImageSize;
    private static long completeImageSize;
    private static long totalVideoSize;
    private static long completeVideoSize;
    private static long currentFileSize;
    private static readonly SortedSet<char> sourceDriveLetters = new();
    private static readonly StatFile statFile = new();

    public static void ConfigureStatusDirectory(string destinationDirectory)
    {
        lock (syncRoot)
            statFile.SetRootDirectory(destinationDirectory);
    }

    public static void Reset()
    {
        lock (syncRoot)
        {
            statFile.CloseFile();
            startTime = DateTime.Now;
            endTime = default;
            imagesEta = null;
            totalEta = default;
            lastRecalculatedAt = default;
            totalCount = 0;
            completeCount = 0;
            totalSize = 0;
            completeSize = 0;
            totalImageSize = 0;
            completeImageSize = 0;
            totalVideoSize = 0;
            completeVideoSize = 0;
            currentFileSize = 0;
            sourceDriveLetters.Clear();
            statFile.SetDrivePrefix(string.Empty);
        }
    }

    public static void RegisterSourceDrive(string driveName)
    {
        if (string.IsNullOrWhiteSpace(driveName))
            throw new ArgumentException("Имя диска не может быть пустым.", nameof(driveName));

        char driveLetter = char.ToLowerInvariant(driveName[0]);
        if (!char.IsLetter(driveLetter))
            throw new ArgumentException("Не удалось определить букву диска.", nameof(driveName));

        lock (syncRoot)
        {
            if (!sourceDriveLetters.Add(driveLetter))
                return;

            statFile.SetDrivePrefix(new string(sourceDriveLetters.ToArray()) + "-");
        }
    }

    public static void Start()
    {
        lock (syncRoot)
            startTime = DateTime.Now;
    }

    public static TimeSpan Stop()
    {
        lock (syncRoot)
        {
            statFile.CloseFile();
            endTime = DateTime.Now;
            return endTime - startTime;
        }
    }

    public static TimeSpan GetCurrentScanTime()
    {
        lock (syncRoot)
            return DateTime.Now - startTime;
    }

    public static string GetCurrentScanTimeAsString() => $"{GetCurrentScanTime():hh\\:mm\\:ss\\.ff}";

    public static void RecalculateEstimatedTime()
    {
        lock (syncRoot)
        {
            if ((DateTime.Now - lastRecalculatedAt).TotalSeconds < 27 || completeSize < 10_000_000)
                return;

            lastRecalculatedAt = DateTime.Now;
            imagesEta = GetImagesEta() ?? imagesEta;
            totalEta = GetTotalEta();
            statFile.GenerateNewFile(GetPercentageOfCompletion(), GetCurrentGroupType(), currentFileSize,
                imagesEta, totalEta, GetCurrentScanTime());
        }
    }

    public static int GetPercentageOfCompletion()
    {
        lock (syncRoot)
            return totalSize > 0 ? (int)((double)completeSize / totalSize * 100) : 0;
    }

    public static GroupType GetCurrentGroupType()
    {
        lock (syncRoot)
            return completeVideoSize == 0 ? GroupType.Image : GroupType.Video;
    }

    public static void AddFileToTolalStat(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        lock (syncRoot)
            AddFileToTotalStatCore(file);
    }

    public static void AddFilesToTotalStat(IEnumerable<FileInfo> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        lock (syncRoot)
        {
            foreach (FileInfo file in files)
            {
                ArgumentNullException.ThrowIfNull(file);
                AddFileToTotalStatCore(file);
            }
        }
    }

    public static void AddFilesToTotalStat(IEnumerable<BackupFileCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        lock (syncRoot)
        {
            foreach (BackupFileCandidate candidate in candidates)
            {
                ArgumentNullException.ThrowIfNull(candidate);
                totalCount++;
                totalSize += candidate.Length;
                if (candidate.Analysis.Kind == MediaKind.Image) totalImageSize += candidate.Length;
                if (candidate.Analysis.Kind == MediaKind.Video) totalVideoSize += candidate.Length;
            }
        }
    }

    public static void AddFileToCompletedStat(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        lock (syncRoot)
        {
            currentFileSize = file.Length;
            completeCount++;
            completeSize += file.Length;
            if (MediaFileClassifier.IsImage(file)) completeImageSize += file.Length;
            if (MediaFileClassifier.IsVideo(file)) completeVideoSize += file.Length;
        }
    }

    public static void AddFileToCompletedStat(BackupFileCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (syncRoot)
        {
            currentFileSize = candidate.Length;
            completeCount++;
            completeSize += candidate.Length;
            if (candidate.Analysis.Kind == MediaKind.Image) completeImageSize += candidate.Length;
            if (candidate.Analysis.Kind == MediaKind.Video) completeVideoSize += candidate.Length;
        }
    }

    public static void RemoveFileFromTotalStat(FileInfo file, long fileSize)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (fileSize < 0)
            throw new ArgumentOutOfRangeException(nameof(fileSize));

        lock (syncRoot)
        {
            totalCount = Math.Max(0, totalCount - 1);
            totalSize = Math.Max(0, totalSize - fileSize);
            if (MediaFileClassifier.IsImage(file))
                totalImageSize = Math.Max(0, totalImageSize - fileSize);
            if (MediaFileClassifier.IsVideo(file))
                totalVideoSize = Math.Max(0, totalVideoSize - fileSize);
        }
    }

    public static TimeSpan? GetImagesEta()
    {
        lock (syncRoot)
        {
            if (completeImageSize == totalImageSize) return null;
            double speed = completeImageSize / GetCurrentScanTime().TotalSeconds;
            return TimeSpan.FromSeconds((totalImageSize - completeImageSize) / speed);
        }
    }

    public static TimeSpan GetTotalEta()
    {
        lock (syncRoot)
        {
            double speed = completeSize / GetCurrentScanTime().TotalSeconds;
            return TimeSpan.FromSeconds((totalSize - completeSize) / speed);
        }
    }

    public static StatSnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            int percentage = totalSize > 0 ? (int)((double)completeSize / totalSize * 100) : 0;
            return new StatSnapshot(totalCount, totalSize, completeCount, completeSize, percentage);
        }
    }

    private static void AddFileToTotalStatCore(FileInfo file)
    {
        totalCount++;
        totalSize += file.Length;
        if (MediaFileClassifier.IsImage(file)) totalImageSize += file.Length;
        if (MediaFileClassifier.IsVideo(file)) totalVideoSize += file.Length;
    }
}

public sealed record StatSnapshot(
    int TotalFileCount,
    long TotalSize,
    int CompletedFileCount,
    long CompletedSize,
    int Percentage);
