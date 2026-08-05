using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Scanning;

public class FileScannerTests
{
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
