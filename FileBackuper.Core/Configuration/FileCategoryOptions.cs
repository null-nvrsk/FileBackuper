namespace FileBackuper.Core;

public sealed class FileCategoryOptions
{
    public string Name { get; init; } = string.Empty;

    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    /// <summary>
    /// Includes every extension in the category. When false, only extensions listed in
    /// <see cref="EnabledExtensions"/> are included.
    /// </summary>
    public bool Enabled { get; init; }

    public List<string> Extensions { get; init; } = new();

    public List<string> EnabledExtensions { get; init; } = new();

    public static List<FileCategoryOptions> CreateDefaults() => new()
    {
        new()
        {
            Name = "Images",
            Kind = MediaKind.Image,
            Enabled = false,
            Extensions = new()
            {
                "jpg", "jpeg", "png", "bmp", "gif", "tif", "tiff", "webp", "avif", "heic", "cr2",
                "cr3", "nef", "nrw", "arw", "raf", "orf", "rw2", "pef", "dng", "rwl", "raw",
                "srw", "x3f"
            },
            EnabledExtensions = new()
            {
                "jpg", "jpeg", "heic", "cr2", "cr3", "nef", "nrw", "arw", "raf", "orf", "rw2",
                "pef", "dng", "rwl", "raw", "srw", "x3f"
            }
        },
        new()
        {
            Name = "Videos",
            Kind = MediaKind.Video,
            Enabled = false,
            Extensions = new()
            {
                "mp4", "mpg", "mov", "avi", "mts", "m2ts", "3gp", "webm", "mxf", "ts", "asf",
                "mkv", "m4v", "wmv", "vob"
            },
            EnabledExtensions = new()
            {
                "mp4", "mpg", "mov", "avi", "mts", "m2ts", "3gp", "webm", "mxf", "ts", "asf"
            }
        },
        new()
        {
            Name = "Documents",
            Kind = MediaKind.Document,
            Enabled = false,
            Extensions = new()
            {
                "txt", "doc", "docx", "pdf", "xls", "xlsx", "ppt", "pptx", "rtf", "odt", "ods",
                "odp", "csv", "md", "epub", "fb2", "djvu"
            }
        },
        new()
        {
            Name = "Archives",
            Kind = MediaKind.Archive,
            Enabled = false,
            Extensions = new() { "zip", "rar", "7z" }
        },
        new()
        {
            Name = "Audio",
            Kind = MediaKind.Audio,
            Enabled = false,
            Extensions = new() { "mp3", "wav", "flac" }
        }
    };
}
