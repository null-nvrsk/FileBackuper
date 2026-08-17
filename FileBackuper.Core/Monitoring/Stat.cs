namespace FileBackuper.Core;

public static class Stat
{
    private static readonly TimeSpan EstimateWarmupDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ProgressUpdateInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LogUpdateInterval = TimeSpan.FromMinutes(1);
    private static readonly string LogSeparator = new('-', 130);
    private static readonly object syncRoot = new();
    private static DateTime startTime;
    private static DateTime copyStartTime;
    private static DateTime endTime;
    private static TimeSpan? imagesEta;
    private static TimeSpan totalEta;
    private static DateTime lastRecalculatedAt;
    private static DateTime lastLoggedAt;
    private static int totalCount;
    private static int completeCount;
    private static long totalSize;
    private static long completeSize;
    private static long totalImageSize;
    private static long completeImageSize;
    private static long totalVideoSize;
    private static long completeVideoSize;
    private static int totalImageCount;
    private static int completeImageCount;
    private static int totalVideoCount;
    private static int completeVideoCount;
    private static long currentFileSize;
    private static Timer? progressTimer;
    private static int progressTimerGeneration;
    private static bool finalProgressLogged;
    private static bool isPaused;
    private static DateTime pauseStartedAt;
    private static TimeSpan pausedDuration;
    private static readonly SortedSet<char> sourceDriveLetters = new();
    private static readonly StatFile statFile = new();
    private static readonly List<SizeGroupStat> sizeGroups = new();
    private static readonly long[] milestoneLimits =
    {
        100L * 1024 * 1024,
        200L * 1024 * 1024,
        300L * 1024 * 1024
    };
    private static readonly long[] milestoneTotalSizes = new long[milestoneLimits.Length];
    private static readonly long[] milestoneCompletedSizes = new long[milestoneLimits.Length];

    public static void ConfigureStatusDirectory(string destinationDirectory)
    {
        lock (syncRoot)
            statFile.SetRootDirectory(destinationDirectory);
    }

    public static void ConfigureSizeGroups(IEnumerable<FileSizeGroupOptions> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        lock (syncRoot)
        {
            sizeGroups.Clear();
            sizeGroups.AddRange(groups.Select(group => new SizeGroupStat(group.Name, group.MinBytes, group.MaxBytes)));
        }
    }

    public static void Reset()
    {
        lock (syncRoot)
        {
            StopProgressTimer();
            statFile.CloseFile();
            startTime = DateTime.Now;
            copyStartTime = default;
            endTime = default;
            imagesEta = null;
            totalEta = default;
            lastRecalculatedAt = default;
            lastLoggedAt = default;
            finalProgressLogged = false;
            isPaused = false;
            pauseStartedAt = default;
            pausedDuration = TimeSpan.Zero;
            totalCount = 0;
            completeCount = 0;
            totalSize = 0;
            completeSize = 0;
            totalImageSize = 0;
            completeImageSize = 0;
            totalVideoSize = 0;
            completeVideoSize = 0;
            totalImageCount = 0;
            completeImageCount = 0;
            totalVideoCount = 0;
            completeVideoCount = 0;
            currentFileSize = 0;
            Array.Clear(milestoneTotalSizes, 0, milestoneTotalSizes.Length);
            Array.Clear(milestoneCompletedSizes, 0, milestoneCompletedSizes.Length);
            sourceDriveLetters.Clear();
            statFile.SetDrivePrefix(string.Empty);
            foreach (SizeGroupStat group in sizeGroups)
                group.Reset();
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
            StopProgressTimer();
            copyStartTime = DateTime.Now;
            lastRecalculatedAt = default;
            int generation = progressTimerGeneration;
            progressTimer = new Timer(_ => ProgressTimerTick(generation), null,
                ProgressUpdateInterval, ProgressUpdateInterval);
            RecalculateEstimatedTime();
        }
    }

    public static void SetPaused(bool value)
    {
        lock (syncRoot)
        {
            if (isPaused == value)
                return;

            DateTime now = DateTime.Now;
            isPaused = value;
            if (value)
            {
                if (copyStartTime != default)
                    pauseStartedAt = now;
            }
            else if (pauseStartedAt != default)
            {
                pausedDuration += now - pauseStartedAt;
                pauseStartedAt = default;
            }

            lastRecalculatedAt = default;
            if (copyStartTime != default)
            {
                RecalculateEstimatedTime();
                return;
            }

            string report = BuildProgressReportCore(now, includeTimeBlock: true);
            if (statFile.IsRootDirectoryConfigured)
                UpdateStatusFile(report);
            WriteProgressToConsole(report);
        }
    }

    public static TimeSpan Stop()
    {
        lock (syncRoot)
        {
            StopProgressTimer();
            if (!finalProgressLogged)
            {
                WriteProgressToLogCore(DateTime.Now);
                finalProgressLogged = true;
            }
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

            if (lastRecalculatedAt != default && now - lastRecalculatedAt < ProgressUpdateInterval)
                return;

            lastRecalculatedAt = now;
            TimeSpan copyElapsed = GetCopyElapsedTime(now);
            bool estimatesEnabled = copyElapsed >= EstimateWarmupDuration && completeSize > 0;
            if (estimatesEnabled)
            {
                imagesEta = GetImagesEta() ?? imagesEta;
                totalEta = GetTotalEta();
            }
            string report = BuildProgressReportCore(now, includeTimeBlock: true);
            if (statFile.IsRootDirectoryConfigured)
                UpdateStatusFile(report);
            WriteProgressToConsole(report);
            if (copyElapsed >= LogUpdateInterval &&
                (lastLoggedAt == default || now - lastLoggedAt >= LogUpdateInterval))
            {
                WriteProgressToLogCore(now);
                lastLoggedAt = now;
            }
        }
    }

    public static void LogFinalProgress()
    {
        lock (syncRoot)
        {
            if (finalProgressLogged)
                return;

            DateTime now = DateTime.Now;
            WriteProgressToConsole(BuildProgressReportCore(now, includeTimeBlock: true));
            WriteProgressToLogCore(now);
            finalProgressLogged = true;
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
        {
            GroupType result = GroupType.None;
            if (completeImageCount > 0 && completeImageCount < totalImageCount)
                result |= GroupType.Image;
            if (completeVideoCount > 0 && completeVideoCount < totalVideoCount)
                result |= GroupType.Video;
            return result;
        }
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
                AddToMilestones(milestoneTotalSizes, candidate.Length);
                FindSizeGroup(candidate.Length)?.AddTotal(candidate.Length);
                if (candidate.Analysis.Kind == MediaKind.Image)
                {
                    totalImageCount++;
                    totalImageSize += candidate.Length;
                }
                if (candidate.Analysis.Kind == MediaKind.Video)
                {
                    totalVideoCount++;
                    totalVideoSize += candidate.Length;
                }
            }
        }
    }

    public static void AddFileToCompletedStat(FileInfo file, TimeSpan copyDuration = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        lock (syncRoot)
        {
            currentFileSize = file.Length;
            completeCount++;
            completeSize += file.Length;
            AddToMilestones(milestoneCompletedSizes, file.Length);
            FindSizeGroup(file.Length)?.AddCompleted(file.Length, copyDuration);
            if (MediaFileClassifier.IsImage(file))
            {
                completeImageCount++;
                completeImageSize += file.Length;
            }
            if (MediaFileClassifier.IsVideo(file))
            {
                completeVideoCount++;
                completeVideoSize += file.Length;
            }
        }
    }

    public static void AddFileToCompletedStat(BackupFileCandidate candidate, TimeSpan copyDuration = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (syncRoot)
        {
            currentFileSize = candidate.Length;
            completeCount++;
            completeSize += candidate.Length;
            AddToMilestones(milestoneCompletedSizes, candidate.Length);
            FindSizeGroup(candidate.Length)?.AddCompleted(candidate.Length, copyDuration);
            if (candidate.Analysis.Kind == MediaKind.Image)
            {
                completeImageCount++;
                completeImageSize += candidate.Length;
            }
            if (candidate.Analysis.Kind == MediaKind.Video)
            {
                completeVideoCount++;
                completeVideoSize += candidate.Length;
            }
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
            RemoveFromMilestones(milestoneTotalSizes, fileSize);
            FindSizeGroup(fileSize)?.RemoveTotal(fileSize);
            if (MediaFileClassifier.IsImage(file))
            {
                totalImageCount = Math.Max(0, totalImageCount - 1);
                totalImageSize = Math.Max(0, totalImageSize - fileSize);
            }
            if (MediaFileClassifier.IsVideo(file))
            {
                totalVideoCount = Math.Max(0, totalVideoCount - 1);
                totalVideoSize = Math.Max(0, totalVideoSize - fileSize);
            }
        }
    }

    public static void RemoveFileFromTotalStat(BackupFileCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (syncRoot)
        {
            totalCount = Math.Max(0, totalCount - 1);
            totalSize = Math.Max(0, totalSize - candidate.Length);
            RemoveFromMilestones(milestoneTotalSizes, candidate.Length);
            FindSizeGroup(candidate.Length)?.RemoveTotal(candidate.Length);
            if (candidate.Analysis.Kind == MediaKind.Image)
            {
                totalImageCount = Math.Max(0, totalImageCount - 1);
                totalImageSize = Math.Max(0, totalImageSize - candidate.Length);
            }
            if (candidate.Analysis.Kind == MediaKind.Video)
            {
                totalVideoCount = Math.Max(0, totalVideoCount - 1);
                totalVideoSize = Math.Max(0, totalVideoSize - candidate.Length);
            }
        }
    }

    public static TimeSpan? GetImagesEta()
    {
        lock (syncRoot)
        {
            if (completeImageSize == totalImageSize) return null;
            double elapsedSeconds = GetCopyElapsedTime().TotalSeconds;
            if (completeImageSize <= 0 || elapsedSeconds <= 0)
                return null;
            double speed = completeImageSize / elapsedSeconds;
            return TimeSpan.FromSeconds((totalImageSize - completeImageSize) / speed);
        }
    }

    public static TimeSpan GetTotalEta()
    {
        lock (syncRoot)
        {
            double elapsedSeconds = GetCopyElapsedTime().TotalSeconds;
            if (completeSize <= 0 || elapsedSeconds <= 0)
                return TimeSpan.Zero;
            double speed = completeSize / elapsedSeconds;
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

    public static string BuildProgressReport()
    {
        lock (syncRoot)
            return BuildProgressReportCore(DateTime.Now, includeTimeBlock: true);
    }

    internal static string BuildProgressReport(DateTime now)
    {
        lock (syncRoot)
            return BuildProgressReportCore(now, includeTimeBlock: true);
    }

    internal static string BuildLogProgressBlock(DateTime now)
    {
        lock (syncRoot)
            return BuildLogProgressBlockCore(now);
    }

    private static void AddFileToTotalStatCore(FileInfo file)
    {
        totalCount++;
        totalSize += file.Length;
        AddToMilestones(milestoneTotalSizes, file.Length);
        FindSizeGroup(file.Length)?.AddTotal(file.Length);
        if (MediaFileClassifier.IsImage(file))
        {
            totalImageCount++;
            totalImageSize += file.Length;
        }
        if (MediaFileClassifier.IsVideo(file))
        {
            totalVideoCount++;
            totalVideoSize += file.Length;
        }
    }

    private static void UpdateStatusFile(string report)
    {
        GroupType group = GetCurrentGroupType();
        if (group == GroupType.None)
        {
            statFile.CloseFile();
            return;
        }

        statFile.GenerateNewFile(GetPercentageOfCompletion(), group, currentFileSize,
            imagesEta, totalEta, GetCurrentScanTime(), report);
    }

    private static SizeGroupStat? FindSizeGroup(long fileSize) =>
        sizeGroups.FirstOrDefault(group => fileSize >= group.MinBytes && fileSize <= group.MaxBytes);

    private static string BuildProgressReportCore(DateTime now, bool includeTimeBlock)
    {
        TimeSpan copyElapsed = copyStartTime == default ? TimeSpan.Zero : GetCopyElapsedTime(now);
        bool estimatesEnabled = copyElapsed >= EstimateWarmupDuration && completeSize > 0;
        double averageBytesPerSecond = estimatesEnabled && copyElapsed.TotalSeconds > 0
            ? completeSize / copyElapsed.TotalSeconds
            : 0;
        List<string> lines = new();
        List<SizeGroupStat> displayedGroups = sizeGroups.Where(group => group.TotalCount > 0).ToList();
        int groupWidth = displayedGroups.Select(group => $"[{group.Name}]".Length).DefaultIfEmpty(0).Max();
        int completedSizeWidth = displayedGroups.Select(group => FormatSize(group.CompletedSize).Length)
            .DefaultIfEmpty(0).Max();
        int totalSizeWidth = displayedGroups.Select(group => FormatSize(group.TotalSize).Length)
            .DefaultIfEmpty(0).Max();
        int countWidth = displayedGroups.SelectMany(group => new[]
            {
                group.CompletedCount.ToString("N0"),
                group.TotalCount.ToString("N0")
            })
            .Select(value => value.Length)
            .DefaultIfEmpty(0).Max();
        Dictionary<SizeGroupStat, GroupDisplayMetrics> displayMetrics = displayedGroups.ToDictionary(
            group => group, group => GetGroupDisplayMetrics(group, estimatesEnabled));
        int durationWidth = displayMetrics.Values.Select(metrics => FormatOptionalDuration(metrics.Duration).Length)
            .DefaultIfEmpty(0).Max();
        int speedWidth = displayMetrics.Values.Select(metrics => FormatOptionalSpeed(metrics.BytesPerSecond).Length)
            .DefaultIfEmpty(0).Max();

        foreach (SizeGroupStat group in displayedGroups)
        {
            int percentage = group.TotalSize > 0
                ? (int)Math.Clamp(group.CompletedSize * 100d / group.TotalSize, 0, 100)
                : 0;
            string groupColumn = $"[{group.Name}]".PadRight(groupWidth);
            string completedSize = FormatSize(group.CompletedSize).PadLeft(completedSizeWidth);
            string groupTotalSize = FormatSize(group.TotalSize).PadLeft(totalSizeWidth);
            string completedCount = group.CompletedCount.ToString("N0").PadLeft(countWidth);
            string groupTotalCount = group.TotalCount.ToString("N0").PadLeft(countWidth);
            GroupDisplayMetrics metrics = displayMetrics[group];
            string duration = FormatOptionalDuration(metrics.Duration).PadLeft(durationWidth);
            string speed = FormatOptionalSpeed(metrics.BytesPerSecond).PadLeft(speedWidth);
            lines.Add($"{BuildProgressBar(percentage)} {percentage,3}% {groupColumn} " +
                $"[{completedSize} / {groupTotalSize}] [{completedCount} / {groupTotalCount}], " +
                $"время {duration}, сред. скорость {speed}");
        }

        int totalPercentage = totalSize > 0
            ? (int)Math.Clamp(completeSize * 100d / totalSize, 0, 100)
            : 0;
        string totalSpeed = estimatesEnabled
            ? $"{FormatSpeed(averageBytesPerSecond)} / {FormatGigabytesPerMinute(averageBytesPerSecond)}"
            : "—";
        lines.Add(string.Empty);
        lines.Add($"{totalPercentage}% [время копирования {FormatDuration(copyElapsed)}]" +
            $"[Скопировано {FormatSize(completeSize)} из {FormatSize(totalSize)}]" +
            $"[Файлов: {completeCount:N0} из {totalCount:N0}] " +
            $"[Скорость: {totalSpeed}]");
        if (!includeTimeBlock)
            return WrapReport(lines);

        lines.Add(string.Empty);
        lines.Add("Блок времени:");
        List<(string Label, string Value)> timeRows = new()
        {
            ("время начала", startTime.ToString("dd.MM.yyyy HH:mm:ss")),
            ("примерное время конца картинок",
                FormatEstimatedEnd(now, totalImageSize, completeImageSize, averageBytesPerSecond))
        };
        foreach (long limit in milestoneLimits)
        {
            (long milestoneTotal, long milestoneCompleted) = GetMilestoneProgress(limit);
            timeRows.Add(($"примерное время копирования до {limit / 1024 / 1024} МБ",
                FormatEstimatedEnd(now, milestoneTotal, milestoneCompleted, averageBytesPerSecond)));
        }
        timeRows.Add(("примерное время всего",
            FormatEstimatedEnd(now, totalSize, completeSize, averageBytesPerSecond)));
        int timeLabelWidth = timeRows.Max(row => row.Label.Length);
        lines.AddRange(timeRows.Select(row => $"- {row.Label.PadRight(timeLabelWidth)} : {row.Value}"));
        return WrapReport(lines);
    }

    private static string WrapReport(IEnumerable<string> lines)
    {
        IEnumerable<string> firstLines = isPaused
            ? new[] { "ПАУЗА", LogSeparator }
            : new[] { LogSeparator };
        return string.Join(Environment.NewLine, firstLines.Concat(lines).Append(LogSeparator));
    }

    private static string BuildLogProgressBlockCore(DateTime now) =>
        BuildProgressReportCore(now, includeTimeBlock: false);

    private static void WriteProgressToLogCore(DateTime now)
    {
        BackupLog.Raw(BuildLogProgressBlockCore(now));
        BackupLog.Flush();
    }

    private static void WriteProgressToConsole(string report)
    {
        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Some console hosts do not support clearing; retain append-only output there.
            }
            catch (PlatformNotSupportedException)
            {
                // Redirect-like console hosts can report themselves as interactive.
            }
        }

        Console.WriteLine(report);
    }

    private static GroupDisplayMetrics GetGroupDisplayMetrics(SizeGroupStat group, bool estimatesEnabled)
    {
        if (group.CompletedCount > 0)
            return new GroupDisplayMetrics(group.CopyDuration, group.AverageBytesPerSecond);
        if (!estimatesEnabled)
            return new GroupDisplayMetrics(null, null);

        int groupIndex = sizeGroups.IndexOf(group);
        double[] previousSpeeds = sizeGroups
            .Take(groupIndex)
            .Where(previous => previous.CompletedCount > 0 && previous.AverageBytesPerSecond > 0)
            .TakeLast(3)
            .Select(previous => previous.AverageBytesPerSecond)
            .ToArray();
        if (previousSpeeds.Length == 0)
            return new GroupDisplayMetrics(null, null);

        double estimatedBytesPerSecond = previousSpeeds.Average();
        TimeSpan estimatedDuration = TimeSpan.FromSeconds(group.TotalSize / estimatedBytesPerSecond);
        return new GroupDisplayMetrics(estimatedDuration, estimatedBytesPerSecond);
    }

    private static (long Total, long Completed) GetMilestoneProgress(long maxFileSize)
    {
        int index = Array.IndexOf(milestoneLimits, maxFileSize);
        return index >= 0
            ? (milestoneTotalSizes[index], milestoneCompletedSizes[index])
            : (0, 0);
    }

    private static void AddToMilestones(long[] values, long fileSize)
    {
        for (int index = 0; index < milestoneLimits.Length; index++)
            if (fileSize <= milestoneLimits[index])
                values[index] += fileSize;
    }

    private static void RemoveFromMilestones(long[] values, long fileSize)
    {
        for (int index = 0; index < milestoneLimits.Length; index++)
            if (fileSize <= milestoneLimits[index])
                values[index] = Math.Max(0, values[index] - fileSize);
    }

    internal static string BuildProgressBar(int percentage)
    {
        int completedBlocks = Math.Clamp(percentage / 5, 0, 20);
        return $"[{new string('+', completedBlocks)}{new string('-', 20 - completedBlocks)}]";
    }

    private static string FormatEstimatedEnd(DateTime now, long targetBytes, long completedBytes,
        double bytesPerSecond)
    {
        if (targetBytes <= 0)
            return "нет файлов";
        long remainingBytes = Math.Max(0, targetBytes - completedBytes);
        if (remainingBytes <= 0)
            return "выполнено";
        if (bytesPerSecond <= 0 || double.IsNaN(bytesPerSecond) || double.IsInfinity(bytesPerSecond))
            return "недостаточно данных";

        double remainingSeconds = remainingBytes / bytesPerSecond;
        if (remainingSeconds > TimeSpan.MaxValue.TotalSeconds)
            return "недостаточно данных";
        TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);
        DateTime estimatedEnd = now.Add(remaining);
        string estimatedEndText = remaining < TimeSpan.FromDays(1)
            ? estimatedEnd.ToString("HH:mm:ss")
            : estimatedEnd.ToString("dd.MM.yyyy HH:mm:ss");
        return $"{estimatedEndText} (через {FormatDuration(remaining)})";
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalDays >= 1
            ? $"{(int)duration.TotalDays}.{duration:hh\\:mm\\:ss}"
            : $"{duration:hh\\:mm\\:ss}";

    private static string FormatOptionalDuration(TimeSpan? duration) =>
        duration.HasValue ? FormatDuration(duration.Value) : "—";

    private static string FormatSpeed(double bytesPerSecond) =>
        $"{bytesPerSecond / 1024d / 1024d:N2} МБ/с";

    private static string FormatGigabytesPerMinute(double bytesPerSecond) =>
        $"{bytesPerSecond / 1024d / 1024d / 1024d * 60:N2} ГБ/мин";

    private static string FormatOptionalSpeed(double? bytesPerSecond) =>
        bytesPerSecond.HasValue ? FormatSpeed(bytesPerSecond.Value) : "—";

    private static string FormatSize(long bytes)
    {
        double size = bytes;
        string[] units = { "байт", "КБ", "МБ", "ГБ", "ТБ" };
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:N2} {units[unitIndex]}";
    }

    private static TimeSpan GetCopyElapsedTime() => GetCopyElapsedTime(DateTime.Now);

    private static TimeSpan GetCopyElapsedTime(DateTime now)
    {
        DateTime effectiveStartTime = copyStartTime == default ? startTime : copyStartTime;
        DateTime effectiveNow = isPaused && pauseStartedAt != default ? pauseStartedAt : now;
        return TimeSpan.FromTicks(Math.Max(0, (effectiveNow - effectiveStartTime - pausedDuration).Ticks));
    }

    private static void ProgressTimerTick(int generation)
    {
        lock (syncRoot)
        {
            if (generation != progressTimerGeneration || copyStartTime == default)
                return;
            RecalculateEstimatedTime();
        }
    }

    private static void StopProgressTimer()
    {
        progressTimer?.Dispose();
        progressTimer = null;
        progressTimerGeneration++;
    }
}

internal sealed class SizeGroupStat
{
    public SizeGroupStat(string name, long minBytes, long maxBytes) =>
        (Name, MinBytes, MaxBytes) = (name, minBytes, maxBytes);

    public string Name { get; }
    public long MinBytes { get; }
    public long MaxBytes { get; }
    public int TotalCount { get; private set; }
    public long TotalSize { get; private set; }
    public int CompletedCount { get; private set; }
    public long CompletedSize { get; private set; }
    public TimeSpan CopyDuration { get; private set; }
    public double AverageBytesPerSecond => CopyDuration.TotalSeconds > 0
        ? CompletedSize / CopyDuration.TotalSeconds
        : 0;

    public void AddTotal(long size) => (TotalCount, TotalSize) = (TotalCount + 1, TotalSize + size);
    public void AddCompleted(long size, TimeSpan duration) =>
        (CompletedCount, CompletedSize, CopyDuration) =
        (CompletedCount + 1, CompletedSize + size, CopyDuration + duration);
    public void RemoveTotal(long size) =>
        (TotalCount, TotalSize) = (Math.Max(0, TotalCount - 1), Math.Max(0, TotalSize - size));
    public void Reset() =>
        (TotalCount, TotalSize, CompletedCount, CompletedSize, CopyDuration) = (0, 0, 0, 0, TimeSpan.Zero);
}

internal sealed record GroupDisplayMetrics(TimeSpan? Duration, double? BytesPerSecond);

public sealed record StatSnapshot(
    int TotalFileCount,
    long TotalSize,
    int CompletedFileCount,
    long CompletedSize,
    int Percentage);
