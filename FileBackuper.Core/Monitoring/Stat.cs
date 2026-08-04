namespace FileBackuper.Core;

public static class Stat
{
    private static DateTime startTime;
    private static DateTime endTime;
    private static TimeSpan? imagesEta;
    private static TimeSpan totalEta;
    private static DateTime lastRecalculatedAt;
    private static int totalCount;
    private static long totalSize;
    private static long completeSize;
    private static long totalImageSize;
    private static long completeImageSize;
    private static long totalVideoSize;
    private static long completeVideoSize;
    private static long currentFileSize;
    private static readonly StatFile statFile = new();

    public static void Start() => startTime = DateTime.Now;

    public static TimeSpan Stop()
    {
        statFile.CloseFile();
        endTime = DateTime.Now;
        return endTime - startTime;
    }

    public static TimeSpan GetCurrentScanTime() => DateTime.Now - startTime;

    public static string GetCurrentScanTimeAsString() => $"{GetCurrentScanTime():hh\\:mm\\:ss\\.ff}";

    public static void RecalculateEstimatedTime()
    {
        if ((DateTime.Now - lastRecalculatedAt).TotalSeconds < 27 || completeSize < 10_000_000)
            return;

        lastRecalculatedAt = DateTime.Now;
        imagesEta = GetImagesEta() ?? imagesEta;
        totalEta = GetTotalEta();
        statFile.GenerateNewFile(GetPercentageOfCompletion(), GetCurrentGroupType(), currentFileSize,
            imagesEta, totalEta, GetCurrentScanTime());
    }

    public static int GetPercentageOfCompletion() =>
        totalSize > 0 ? (int)((double)completeSize / totalSize * 100) : 0;

    public static GroupType GetCurrentGroupType() =>
        completeVideoSize == 0 ? GroupType.Image : GroupType.Video;

    public static void AddFileToTolalStat(FileInfo file)
    {
        totalCount++;
        totalSize += file.Length;
        if (MediaFileClassifier.IsImage(file)) totalImageSize += file.Length;
        if (MediaFileClassifier.IsVideo(file)) totalVideoSize += file.Length;
    }

    public static void AddFileToCompletedStat(FileInfo file)
    {
        currentFileSize = file.Length;
        completeSize += file.Length;
        if (MediaFileClassifier.IsImage(file)) completeImageSize += file.Length;
        if (MediaFileClassifier.IsVideo(file)) completeVideoSize += file.Length;
    }

    public static TimeSpan? GetImagesEta()
    {
        if (completeImageSize == totalImageSize) return null;
        double speed = completeImageSize / GetCurrentScanTime().TotalSeconds;
        return TimeSpan.FromSeconds((totalImageSize - completeImageSize) / speed);
    }

    public static TimeSpan GetTotalEta()
    {
        double speed = completeSize / GetCurrentScanTime().TotalSeconds;
        return TimeSpan.FromSeconds((totalSize - completeSize) / speed);
    }
}
