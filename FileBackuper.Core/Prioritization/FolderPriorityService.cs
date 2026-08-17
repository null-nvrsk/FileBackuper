namespace FileBackuper.Core;

public sealed class FolderPriorityService
{
    private static readonly IReadOnlySet<string> PrivateFolderNames = new HashSet<string>(
        new[]
        {
            "private", "intimate", "18+", "adult", "secret", "hidden", "sex", "nude", "личное",
            "личные", "приват", "взрослых", "секс", "секрет", "скрыт", "открывать", "мои фот"
        },
        StringComparer.OrdinalIgnoreCase);

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
            return 3;

        string[] segments = directoryName.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Any(segment => ContainsFolderName(segment, PrivateFolderNames)))
            return 6;
        if (segments.Any(segment => ContainsFolderName(segment, CameraFolderNames)))
            return 5;
        if (segments.Any(UserFolderNames.Contains))
            return 4;
        if (segments.Any(LowPriorityFolderNames.Contains))
            return 2;
        if (segments.Any(LastPriorityFolderNames.Contains))
            return 1;
        return 3;
    }

    private static bool ContainsFolderName(string segment, IReadOnlySet<string> folderNames) =>
        folderNames.Any(folderName => segment.Contains(folderName, StringComparison.OrdinalIgnoreCase));
}
