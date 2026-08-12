namespace FileBackuper.Core;

public sealed class BrowserCacheScanner
{
    private static readonly string[][] CacheDirectoryParts =
    {
        new[] { "AppData", "Local", "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data" },
        new[] { "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Cache", "Cache_Data" }
    };

    private readonly FileSignatureDetector signatureDetector;

    public BrowserCacheScanner(FileSignatureDetector? signatureDetector = null)
    {
        this.signatureDetector = signatureDetector ?? new FileSignatureDetector();
    }

    public List<FileInfo> Scan(DirectoryInfo volumeRoot, long minFileSizeBytes, long maxFileSizeBytes,
        CancellationToken cancellationToken) =>
        ScanWithStatistics(volumeRoot, minFileSizeBytes, maxFileSizeBytes, cancellationToken).Files;

    public BrowserCacheScanResult ScanWithStatistics(DirectoryInfo volumeRoot, long minFileSizeBytes,
        long maxFileSizeBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(volumeRoot);
        if (minFileSizeBytes < 0 || maxFileSizeBytes < minFileSizeBytes)
            throw new ArgumentException("The browser cache file size range is invalid.");

        List<FileInfo> result = new();
        SignatureStatistics signatureStatistics = new();
        DirectoryInfo usersDirectory = new(Path.Combine(volumeRoot.FullName, "Users"));
        if (!usersDirectory.Exists)
            return new BrowserCacheScanResult(result, 0, TimeSpan.Zero);

        EnumerationOptions profileOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            foreach (DirectoryInfo profileDirectory in usersDirectory.EnumerateDirectories("*", profileOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (string[] cacheParts in CacheDirectoryParts)
                {
                    string cachePath = cacheParts.Aggregate(profileDirectory.FullName, Path.Combine);
                    ScanCacheDirectory(new DirectoryInfo(cachePath), minFileSizeBytes, maxFileSizeBytes,
                        result, signatureStatistics, cancellationToken);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            BackupLog.Warning($"Could not enumerate browser cache profiles in {usersDirectory.FullName}. " +
                BackupLog.GetExceptionDescription(exception));
        }

        return new BrowserCacheScanResult(result, signatureStatistics.Count,
            signatureStatistics.Duration);
    }

    private void ScanCacheDirectory(DirectoryInfo cacheDirectory, long minFileSizeBytes, long maxFileSizeBytes,
        ICollection<FileInfo> result, SignatureStatistics signatureStatistics,
        CancellationToken cancellationToken)
    {
        if (!cacheDirectory.Exists)
            return;

        EnumerationOptions fileOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            foreach (FileInfo file in cacheDirectory.EnumerateFiles("*", fileOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(file.Extension) ||
                    file.Length < minFileSizeBytes || file.Length > maxFileSizeBytes)
                {
                    continue;
                }

                signatureStatistics.Count++;
                long signatureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                FileSignatureResult? signature;
                try
                {
                    signature = signatureDetector.Detect(file);
                }
                finally
                {
                    signatureStatistics.Duration += TimeSpan.FromSeconds(
                        (double)(System.Diagnostics.Stopwatch.GetTimestamp() - signatureStarted) /
                        System.Diagnostics.Stopwatch.Frequency);
                }
                if (signature is null)
                    continue;

                result.Add(file);
                BackupLog.Verbose($"Detection=Signature | Format={signature.Format} | " +
                    $"DetectedExtension={signature.DetectedExtension} | File={file.FullName}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            BackupLog.Warning($"Could not scan browser cache directory {cacheDirectory.FullName}. " +
                BackupLog.GetExceptionDescription(exception));
        }
    }

    private sealed class SignatureStatistics
    {
        public long Count { get; set; }

        public TimeSpan Duration { get; set; }
    }
}

public sealed record BrowserCacheScanResult(
    List<FileInfo> Files,
    long SignatureAnalysisCount,
    TimeSpan SignatureAnalysisDuration);
