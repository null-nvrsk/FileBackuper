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
    public void ShouldSkipFile_ReturnsTrueForFileSmallerThanMinimumSize()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("photo.jpg", 9_999);

        Assert.True(FileScanner.ShouldSkipFile(file));
    }

    [Fact]
    public void ShouldSkipFile_ReturnsTrueForBlacklistedVideoName()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("Film.Remux.1080p.mp4", 10_000);

        Assert.True(FileScanner.ShouldSkipFile(file));
    }

    [Fact]
    public void ShouldSkipFile_ReturnsFalseForSupportedImageWithinSizeLimit()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("photo.jpg", 10_000);

        Assert.False(FileScanner.ShouldSkipFile(file));
    }
}
