namespace FileBackuper.Core;

public static class MediaSkipReasons
{
    public const string SizeOutOfRange = nameof(SizeOutOfRange);
    public const string UnsupportedMediaType = nameof(UnsupportedMediaType);
    public const string VideoBlacklist = nameof(VideoBlacklist);
    public const string FileUnavailable = nameof(FileUnavailable);
}
