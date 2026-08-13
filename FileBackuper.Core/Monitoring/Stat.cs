namespace FileBackuper.Core;

public static class Stat
{
    private static readonly TimeSpan EtaWarmupDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EtaWarmupDotInterval = TimeSpan.FromSeconds(10);
    private static readonly object syncRoot = new();
    private static DateTime startTime;
    private static DateTime copyStartTime;
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
    private static int displayedWarmupDots;
    private static bool warmupLineOpen;
    private static Timer? warmupTimer;
    private static int warmupGeneration;
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
            StopWarmupTimer();
            statFile.CloseFile();
            startTime = DateTime.Now;
            copyStartTime = default;
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
            displayedWarmupDots = 0;
            warmupLineOpen = false;
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

    public static void StartCopying()
    {
        lock (syncRoot)
        {
            StopWarmupTimer();
            copyStartTime = DateTime.Now;
            lastRecalculatedAt = default;
            displayedWarmupDots = 0;
            warmupLineOpen = false;
            int generation = warmupGeneration;
            warmupTimer = new Timer(_ => WarmupTimerTick(generation), null,
                EtaWarmupDotInterval, EtaWarmupDotInterval);
        }
    }

    public static TimeSpan Stop()
    {
        lock (syncRoot)
        {
            StopWarmupTimer();
            CompleteWarmupLine();
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
            DateTime now = DateTime.Now;
            if (copyStartTime == default)
                copyStartTime = now;

            TimeSpan copyElapsed = now - copyStartTime;
            WriteWarmupProgress(copyElapsed);
            if (copyElapsed <= EtaWarmupDuration)
                return;

            if ((now - lastRecalculatedAt).TotalSeconds < 27 || completeSize < 10_000_000)
                return;

            lastRecalculatedAt = now;
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
            double speed = completeImageSize / GetCopyElapsedTime().TotalSeconds;
            return TimeSpan.FromSeconds((totalImageSize - completeImageSize) / speed);
        }
    }

    public static TimeSpan GetTotalEta()
    {
        lock (syncRoot)
        {
            double speed = completeSize / GetCopyElapsedTime().TotalSeconds;
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

    internal static int GetWarmupDotCount(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
            return 0;

        return Math.Min(6, (int)(elapsed.TotalSeconds / EtaWarmupDotInterval.TotalSeconds));
    }

    private static TimeSpan GetCopyElapsedTime()
    {
        DateTime effectiveStartTime = copyStartTime == default ? startTime : copyStartTime;
        return DateTime.Now - effectiveStartTime;
    }

    private static void WriteWarmupProgress(TimeSpan copyElapsed)
    {
        int requiredDotCount = GetWarmupDotCount(copyElapsed);
        if (requiredDotCount <= displayedWarmupDots)
            return;

        Console.Write(new string('.', requiredDotCount - displayedWarmupDots));
        displayedWarmupDots = requiredDotCount;
        warmupLineOpen = true;
        if (displayedWarmupDots == 6)
            CompleteWarmupLine();
    }

    private static void CompleteWarmupLine()
    {
        if (!warmupLineOpen)
            return;

        Console.WriteLine();
        warmupLineOpen = false;
    }

    private static void WarmupTimerTick(int generation)
    {
        lock (syncRoot)
        {
            if (generation != warmupGeneration || copyStartTime == default)
                return;

            TimeSpan copyElapsed = DateTime.Now - copyStartTime;
            WriteWarmupProgress(copyElapsed);
            if (copyElapsed >= EtaWarmupDuration)
                StopWarmupTimer();
        }
    }

    private static void StopWarmupTimer()
    {
        warmupTimer?.Dispose();
        warmupTimer = null;
        warmupGeneration++;
    }
}

public sealed record StatSnapshot(
    int TotalFileCount,
    long TotalSize,
    int CompletedFileCount,
    long CompletedSize,
    int Percentage);
