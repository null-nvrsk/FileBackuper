namespace FileBackuper.Core;

public sealed class FolderPriorityService
{
    private static readonly IReadOnlySet<string> CameraFolderNames = new HashSet<string>(
        new[]
        {
            "фото", "фотки", "foto", "icloud", "apple", "telegram", "instagram", "whatsapp",
            "dcim", "camera", "pictures"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> UserFolderNames = new HashSet<string>(
        new[] { "desktop", "documents", "рабочий стол", "документы" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> LowPriorityFolderNames = new HashSet<string>(
        new[] { "temp", "$recycle.bin", "recycle.bin" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> LastPriorityFolderNames = new HashSet<string>(
        new[] { "downloads", "загрузки" },
        StringComparer.OrdinalIgnoreCase);

    public int GetPriority(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string? directoryName = file.DirectoryName;
        if (string.IsNullOrWhiteSpace(directoryName))
            return 20;

        string[] segments = directoryName.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Any(CameraFolderNames.Contains))
            return 40;
        if (segments.Any(UserFolderNames.Contains))
            return 30;
        if (segments.Any(LowPriorityFolderNames.Contains))
            return 10;
        if (segments.Any(LastPriorityFolderNames.Contains))
            return 0;
        return 20;
    }
}
