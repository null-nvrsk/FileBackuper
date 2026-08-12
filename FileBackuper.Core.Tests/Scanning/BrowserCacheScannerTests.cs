using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Scanning;

public class BrowserCacheScannerTests
{
    [Fact]
    public void Scan_FindsOnlyExtensionlessMediaInsideSupportedBrowserCaches()
    {
        using TestWorkspace workspace = new();
        FileInfo edgeFile = CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local",
            "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data", "f_000001"),
            jpeg: true);
        FileInfo chromeFile = CreateCacheFile(workspace, Path.Combine("Users", "Bob", "AppData", "Local",
            "Google", "Chrome", "User Data", "Default", "Cache", "Cache_Data", "f_000002"),
            jpeg: true);
        CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local", "Microsoft", "Edge",
            "User Data", "Default", "Cache", "Cache_Data", "known.jpg"), jpeg: true);
        CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local", "Microsoft", "Edge",
            "User Data", "Default", "Cache", "Cache_Data", "unknown"), jpeg: false);
        CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local", "Other", "Cache_Data",
            "outside-supported-cache"), jpeg: true);
        BrowserCacheScanner scanner = new();

        BrowserCacheScanResult scanResult = scanner.ScanWithStatistics(
            workspace.RootDirectory, 10_000, 20_000, CancellationToken.None);
        List<FileInfo> result = scanResult.Files;

        Assert.Equal(2, result.Count);
        Assert.Equal(3, scanResult.SignatureAnalysisCount);
        Assert.True(scanResult.SignatureAnalysisDuration >= TimeSpan.Zero);
        Assert.Contains(result, file => file.FullName == edgeFile.FullName);
        Assert.Contains(result, file => file.FullName == chromeFile.FullName);
    }

    [Fact]
    public void Scan_AppliesConfiguredFileSizeRangeBeforeSignatureDetection()
    {
        using TestWorkspace workspace = new();
        CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local", "Google", "Chrome",
            "User Data", "Default", "Cache", "Cache_Data", "too-small"), jpeg: true, size: 9_999);
        BrowserCacheScanner scanner = new();

        List<FileInfo> result = scanner.Scan(workspace.RootDirectory, 10_000, 20_000, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public void Scan_FindsFirefoxYandexOperaAndOperaGxCaches()
    {
        using TestWorkspace workspace = new();
        FileInfo firefoxFile = CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local",
            "Mozilla", "Firefox", "Profiles", "abcd.default-release", "cache2", "entries", "ABC123"),
            jpeg: true);
        FileInfo yandexDefaultFile = CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local",
            "Yandex", "YandexBrowser", "User Data", "Default", "Cache", "Cache_Data", "f_000001"),
            jpeg: true);
        FileInfo yandexProfileFile = CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local",
            "Yandex", "YandexBrowser", "User Data", "Profile 2", "Cache", "Cache_Data", "f_000002"),
            jpeg: true);
        FileInfo operaFile = CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local",
            "Opera Software", "Opera Stable", "Cache", "Cache_Data", "f_000003"), jpeg: true);
        FileInfo operaGxFile = CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local",
            "Opera Software", "Opera GX Stable", "Cache", "Cache_Data", "f_000004"), jpeg: true);
        BrowserCacheScanner scanner = new();

        BrowserCacheScanResult result = scanner.ScanWithStatistics(
            workspace.RootDirectory, 10_000, 20_000, CancellationToken.None);

        Assert.Equal(5, result.Files.Count);
        Assert.Equal(5, result.SignatureAnalysisCount);
        Assert.Contains(result.Files, file => file.FullName == firefoxFile.FullName);
        Assert.Contains(result.Files, file => file.FullName == yandexDefaultFile.FullName);
        Assert.Contains(result.Files, file => file.FullName == yandexProfileFile.FullName);
        Assert.Contains(result.Files, file => file.FullName == operaFile.FullName);
        Assert.Contains(result.Files, file => file.FullName == operaGxFile.FullName);
    }

    [Fact]
    public void Scan_UsesCacheFolderAsFallbackWithoutScanningCacheDataTwice()
    {
        using TestWorkspace workspace = new();
        FileInfo fallbackFile = CreateCacheFile(workspace, Path.Combine("Users", "Alice", "AppData", "Local",
            "Opera Software", "Opera Stable", "Cache", "legacy-entry"), jpeg: true);
        BrowserCacheScanner scanner = new();

        BrowserCacheScanResult result = scanner.ScanWithStatistics(
            workspace.RootDirectory, 10_000, 20_000, CancellationToken.None);

        Assert.Single(result.Files);
        Assert.Equal(fallbackFile.FullName, result.Files[0].FullName);
        Assert.Equal(1, result.SignatureAnalysisCount);
    }

    private static FileInfo CreateCacheFile(TestWorkspace workspace, string relativePath, bool jpeg,
        int size = 10_000)
    {
        string path = Path.Combine(workspace.RootDirectory.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] content = new byte[size];
        if (jpeg)
        {
            content[0] = 0xFF;
            content[1] = 0xD8;
            content[2] = 0xFF;
        }
        File.WriteAllBytes(path, content);
        return new FileInfo(path);
    }
}
