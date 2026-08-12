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
