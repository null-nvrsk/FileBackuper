using System.Diagnostics;

namespace FileBackuper.Core;

public static class FilePriorityService
{
    // TODO: добавить автоматические тесты для паттернов камеры и ожидаемого порядка файлов
    // при одинаковых и разных приоритетах.
    public static List<FileInfo> OrderByBackupPriority(IEnumerable<FileInfo> files)
    {
        Dictionary<FileInfo, int> priorities = new();
        foreach (FileInfo file in files) priorities.Add(file, CalculatePriority(file));

        List<FileInfo> orderedFiles = priorities.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToList();
        foreach (FileInfo file in orderedFiles)
        {
            Stat.AddFileToTolalStat(file);
            Trace.WriteLine($"Key = {file}, size = {file.Length:N0}, Value = {priorities[file]}");
        }

        Trace.TraceInformation($"Sorted list size = {orderedFiles.Count:N0}");
        return orderedFiles;
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
        if (new[] { "фото", "фотки", "foto", "icloud", "apple", "telegram", "instagram", "whatsapp", "dcim", "camera", "pictures" }.Any(directory.Contains)) return 40;
        if (directory.Contains("desktop") || directory.Contains("documents")) return 30;
        if (directory.Contains("recycle.bin") || directory.Contains("temp")) return 10;
        if (directory.Contains("downloads") || directory.Contains("загрузки")) return 0;
        return 20;
    }
}
