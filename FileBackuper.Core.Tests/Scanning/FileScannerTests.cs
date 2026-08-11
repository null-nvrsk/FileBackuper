using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Scanning;

public class FileScannerTests
{
    [Fact]
    public void Scan_FindsSupportedFileInNestedDirectory()
    {
        using TestWorkspace workspace = new();
        FileInfo expectedFile = workspace.CreateFile(Path.Combine("nested", "photo.jpg"), 10_000);

        List<FileInfo> files = FileScanner.Scan(workspace.RootDirectory, CancellationToken.None);

        Assert.Contains(files, file => file.FullName == expectedFile.FullName);
    }

    [Fact]
    public void ScanWithStatistics_ReturnsCloudSkipCountSeparately()
    {
        using TestWorkspace workspace = new();
        workspace.CreateFile("photo.jpg", 10_000);
        CloudFileState.Configure(CloudFileMode.FastSkip);

        FileScanResult result = FileScanner.ScanWithStatistics(workspace.RootDirectory, CancellationToken.None);

        Assert.Single(result.Files);
        Assert.Equal(0, result.CloudFilesSkipped);
    }

    [Fact]
    public void Scan_SkipsDirectoryConfiguredByName()
    {
        using TestWorkspace workspace = new();
        workspace.CreateFile(Path.Combine("AppData", "photo.jpg"), 10_000);

        List<FileInfo> files = FileScanner.Scan(workspace.RootDirectory, CancellationToken.None,
            new[] { "AppData" });

        Assert.Empty(files);
    }

    [Fact]
    public void Scan_DoesNotApplySizeOrVideoBlacklistRules()
    {
        using TestWorkspace workspace = new();
        FileInfo smallImage = workspace.CreateFile("small.jpg", 9_999);
        FileInfo blacklistedVideo = workspace.CreateFile("Movie.REMUX.1080p.mp4", 10_000);

        List<FileInfo> files = FileScanner.Scan(workspace.RootDirectory, CancellationToken.None);

        Assert.Contains(files, file => file.FullName == smallImage.FullName);
        Assert.Contains(files, file => file.FullName == blacklistedVideo.FullName);
    }

}
