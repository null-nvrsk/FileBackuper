using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FileBackuper.Core;

public static class FilePriorityService
{
    private static readonly ConditionalWeakTable<FileInfo, PriorityValue> PriorityCache = new();
    private static readonly string[] PreferredFolderNames =
    {
        "фото", "фотки", "foto", "icloud", "apple", "telegram", "instagram", "whatsapp", "dcim", "camera",
        "pictures"
    };

    public static IComparer<FileInfo> BackupPriorityComparer { get; } =
        Comparer<FileInfo>.Create(CompareByBackupPriority);

    // TODO: добавить автоматические тесты для паттернов камеры и ожидаемого порядка файлов
    // при одинаковых и разных приоритетах.
    public static List<FileInfo> OrderByBackupPriority(IEnumerable<FileInfo> files,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);

        Stopwatch priorityStopwatch = Stopwatch.StartNew();
        List<PrioritizedFile> prioritizedFiles = new();
        foreach (FileInfo file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            prioritizedFiles.Add(new PrioritizedFile(file, GetBackupPriority(file)));
        }
        priorityStopwatch.Stop();

        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch sortingStopwatch = Stopwatch.StartNew();
        prioritizedFiles.Sort(ComparePrioritizedFiles);
        sortingStopwatch.Stop();

        List<FileInfo> orderedFiles = prioritizedFiles.Select(item => item.File).ToList();
        TimeSpan totalSortingDuration = priorityStopwatch.Elapsed + sortingStopwatch.Elapsed;
        BackupLog.Info($"Рассчитаны приоритеты {orderedFiles.Count:N0} файлов. Время: " +
            $"{priorityStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}");
        BackupLog.Info($"Сортировка готовых приоритетов завершена. Время: " +
            $"{sortingStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}");
        BackupLog.Info($"Размер отсортированного списка: {orderedFiles.Count:N0}");
        BackupLog.Info($"Конец сортировки. Время сортировки: {totalSortingDuration:hh\\:mm\\:ss\\.ff}");

        if (BackupLog.IsVerboseEnabled)
        {
            Stopwatch verboseLoggingStopwatch = Stopwatch.StartNew();
            foreach (PrioritizedFile item in prioritizedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BackupLog.Verbose($"Файл = {item.File}, размер = {item.File.Length:N0}, " +
                    $"приоритет = {item.Priority}");
            }
            verboseLoggingStopwatch.Stop();
            BackupLog.Info($"Подробный журнал приоритетов записан. Время: " +
                $"{verboseLoggingStopwatch.Elapsed:hh\\:mm\\:ss\\.ff}");
        }

        return orderedFiles;
    }

    public static int GetBackupPriority(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return PriorityCache.GetValue(file, static value => new PriorityValue(CalculatePriority(value))).Value;
    }

    /// <summary>Сравнивает файлы в порядке резервного копирования.</summary>
    public static int CompareByBackupPriority(FileInfo? first, FileInfo? second)
    {
        if (ReferenceEquals(first, second))
            return 0;
        if (first is null)
            return 1;
        if (second is null)
            return -1;

        int priorityComparison = GetBackupPriority(second).CompareTo(GetBackupPriority(first));
        return priorityComparison != 0
            ? priorityComparison
            : StringComparer.OrdinalIgnoreCase.Compare(first.FullName, second.FullName);
    }

    private static int ComparePrioritizedFiles(PrioritizedFile first, PrioritizedFile second)
    {
        int priorityComparison = second.Priority.CompareTo(first.Priority);
        return priorityComparison != 0
            ? priorityComparison
            : StringComparer.OrdinalIgnoreCase.Compare(first.File.FullName, second.File.FullName);
    }

    private static int CalculatePriority(FileInfo file)
    {
        int priority = GetMediaAndSizePriority(file) + GetFolderPriority(file.DirectoryName ?? string.Empty);
        return MediaFileClassifier.HasCameraFileNamePattern(file) ? priority + 1 : priority;
    }

    private static int GetMediaAndSizePriority(FileInfo file)
    {
        if (MediaFileClassifier.IsImage(file))
        {
            int priority = 50_000;
            if (file.Length > 10_000 && file.Length <= 10_000_000) priority += 1_000;
            if (file.Length > 10_000 && file.Length <= 200_000) return priority + 9_900;
            if (file.Length > 200_000 && file.Length <= 10_000_000)
                return priority + (98 - (int)((file.Length - 200_000) / 100_000)) * 100;
            if (file.Length > 10_000_000 && file.Length <= 20_000_000)
                return priority + (10 - (int)((file.Length - 10_000_000) / 1_000_000)) * 100;
            return priority;
        }

        return MediaFileClassifier.IsVideo(file) && file.Length <= 4_000_000_000
            ? (400 - (int)(file.Length / 10_000_000)) * 100
            : 0;
    }

    private static int GetFolderPriority(string directoryName)
    {
        string directory = directoryName.ToLowerInvariant();
        if (PreferredFolderNames.Any(directory.Contains)) return 40;
        if (directory.Contains("desktop") || directory.Contains("documents")) return 30;
        if (directory.Contains("recycle.bin") || directory.Contains("temp")) return 10;
        if (directory.Contains("downloads") || directory.Contains("загрузки")) return 0;
        return 20;
    }

    private sealed record PriorityValue(int Value);

    private readonly record struct PrioritizedFile(FileInfo File, int Priority);
}
