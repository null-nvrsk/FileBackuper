namespace FileBackuper.Core;

public sealed record FileSignatureResult(
    MediaKind Kind,
    MediaFileFormat Format,
    string DetectedExtension);
