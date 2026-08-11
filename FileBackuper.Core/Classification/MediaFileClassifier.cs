namespace FileBackuper.Core;

public static class MediaFileClassifier
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".heic", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".raf", ".orf",
        ".rw2", ".pef", ".dng", ".rwl", ".raw", ".srw", ".x3f"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mpg", ".mov", ".avi", ".mts", ".m2ts", ".3gp", ".webm", ".mxf", ".ts", ".asf"
    };

    public static MediaKind GetKindByExtension(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (ImageExtensions.Contains(file.Extension))
            return MediaKind.Image;
        if (VideoExtensions.Contains(file.Extension))
            return MediaKind.Video;
        return MediaKind.Unknown;
    }

    public static bool IsImage(FileInfo file) => GetKindByExtension(file) == MediaKind.Image;

    public static bool IsVideo(FileInfo file) => GetKindByExtension(file) == MediaKind.Video;

}
