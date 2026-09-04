namespace FileBackuper.Core;

public static class MediaFileClassifier
{
    public static MediaKind GetKindByExtension(FileInfo file, FileTypeSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        return (selection ?? FileTypeSelection.Default).GetKind(file.Extension);
    }

    public static bool IsSupported(FileInfo file, FileTypeSelection? selection = null) =>
        GetKindByExtension(file, selection) != MediaKind.Unknown;

    public static bool IsImage(FileInfo file, FileTypeSelection? selection = null) =>
        GetKindByExtension(file, selection) == MediaKind.Image;

    public static bool IsVideo(FileInfo file, FileTypeSelection? selection = null) =>
        GetKindByExtension(file, selection) == MediaKind.Video;

}
