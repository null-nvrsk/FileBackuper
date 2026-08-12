namespace FileBackuper.Core;

public sealed class BrowserCacheScanner
{
    private static readonly string[][] FixedChromiumProfileParts =
    {
        new[] { "AppData", "Local", "Microsoft", "Edge", "User Data", "Default" },
        new[] { "AppData", "Local", "Google", "Chrome", "User Data", "Default" }
    };

    private static readonly string[][] OperaProfileParts =
    {
        new[] { "AppData", "Local", "Opera Software", "Opera Stable" },
        new[] { "AppData", "Local", "Opera Software", "Opera GX Stable" }
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
                foreach (DirectoryInfo cacheDirectory in EnumerateCacheDirectories(profileDirectory,
                             profileOptions, cancellationToken))
                    ScanCacheDirectory(cacheDirectory, minFileSizeBytes, maxFileSizeBytes,
                        result, signatureStatistics, cancellationToken);
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

    private static IEnumerable<DirectoryInfo> EnumerateCacheDirectories(DirectoryInfo userDirectory,
        EnumerationOptions profileOptions, CancellationToken cancellationToken)
    {
        foreach (string[] profileParts in FixedChromiumProfileParts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo? cacheDirectory = ResolveChromiumCacheDirectory(
                Combine(userDirectory.FullName, profileParts));
            if (cacheDirectory is not null)
                yield return cacheDirectory;
        }

        string yandexUserDataPath = Combine(userDirectory.FullName,
            "AppData", "Local", "Yandex", "YandexBrowser", "User Data");
        foreach (DirectoryInfo profileDirectory in EnumerateChromiumProfiles(
                     new DirectoryInfo(yandexUserDataPath), profileOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo? cacheDirectory = ResolveChromiumCacheDirectory(profileDirectory.FullName);
            if (cacheDirectory is not null)
                yield return cacheDirectory;
        }

        foreach (string[] profileParts in OperaProfileParts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo? cacheDirectory = ResolveChromiumCacheDirectory(
                Combine(userDirectory.FullName, profileParts));
            if (cacheDirectory is not null)
                yield return cacheDirectory;
        }

        DirectoryInfo firefoxProfilesDirectory = new(Combine(userDirectory.FullName,
            "AppData", "Local", "Mozilla", "Firefox", "Profiles"));
        if (!firefoxProfilesDirectory.Exists)
            yield break;

        foreach (DirectoryInfo firefoxProfile in firefoxProfilesDirectory.EnumerateDirectories("*", profileOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo entriesDirectory = new(Path.Combine(firefoxProfile.FullName, "cache2", "entries"));
            if (entriesDirectory.Exists)
                yield return entriesDirectory;
        }
    }

    private static IEnumerable<DirectoryInfo> EnumerateChromiumProfiles(DirectoryInfo userDataDirectory,
        EnumerationOptions profileOptions)
    {
        if (!userDataDirectory.Exists)
            yield break;

        foreach (DirectoryInfo profile in userDataDirectory.EnumerateDirectories("*", profileOptions))
        {
            if (string.Equals(profile.Name, "Default", StringComparison.OrdinalIgnoreCase) ||
                profile.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                yield return profile;
            }
        }
    }

    private static DirectoryInfo? ResolveChromiumCacheDirectory(string profilePath)
    {
        DirectoryInfo cacheDirectory = new(Path.Combine(profilePath, "Cache"));
        if (!cacheDirectory.Exists)
            return null;

        DirectoryInfo cacheDataDirectory = new(Path.Combine(cacheDirectory.FullName, "Cache_Data"));
        return cacheDataDirectory.Exists ? cacheDataDirectory : cacheDirectory;
    }

    private static string Combine(string root, params string[] parts) =>
        parts.Aggregate(root, Path.Combine);

    private static string Combine(string root, IEnumerable<string> parts) =>
        parts.Aggregate(root, Path.Combine);

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
