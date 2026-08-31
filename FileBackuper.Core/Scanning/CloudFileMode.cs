namespace FileBackuper.Core;

public enum CloudFileMode
{
    /// <summary>
    /// Uses file attributes only. Pinned placeholders are treated as locally available
    /// without verifying that their entire contents are on disk.
    /// </summary>
    FastSkip,

    /// <summary>
    /// Uses the Windows Cloud Files API to verify that a pinned placeholder's entire
    /// contents are available locally without hydrating it.
    /// </summary>
    Precise
}
