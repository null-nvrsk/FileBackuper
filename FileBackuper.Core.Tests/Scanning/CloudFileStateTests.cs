using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Scanning;

public class CloudFileStateTests
{
    [Fact]
    public void IsContentAvailableLocally_ReturnsTrueForOrdinaryFile()
    {
        Assert.True(CloudFileState.IsContentAvailableLocally(0));
    }

    [Fact]
    public void IsContentAvailableLocally_ReturnsTrueForFullyHydratedPlaceholder()
    {
        const uint placeholderAndInSync = 0x00000001 | 0x00000008;

        Assert.True(CloudFileState.IsContentAvailableLocally(placeholderAndInSync));
    }

    [Theory]
    [InlineData(0x00000001u | 0x00000010u)]
    [InlineData(0x00000001u | 0x00000010u | 0x00000020u)]
    [InlineData(0xffffffffu)]
    public void IsContentAvailableLocally_ReturnsFalseWhenPlaceholderContentIsNotReady(uint state)
    {
        Assert.False(CloudFileState.IsContentAvailableLocally(state));
    }

    [Theory]
    [InlineData(10_000, 0, false)]
    [InlineData(10_000, 9_999, false)]
    [InlineData(10_000, 10_000, true)]
    [InlineData(10_000, 12_288, true)]
    [InlineData(0, 0, true)]
    public void IsPlaceholderContentFullyOnDisk_ComparesHydratedDataWithLogicalLength(
        long fileLength, long onDiskDataSize, bool expected)
    {
        Assert.Equal(expected,
            CloudFileState.IsPlaceholderContentFullyOnDisk(fileLength, onDiskDataSize));
    }

    [Theory]
    [InlineData(10_000, 10_000, 1, true)]
    [InlineData(10_000, 10_000, 0, false)]
    [InlineData(10_000, 10_000, 2, false)]
    [InlineData(10_000, 9_999, 1, false)]
    public void IsPinnedPlaceholderContentFullyOnDisk_RequiresPinnedAndCompleteContent(
        long fileLength, long onDiskDataSize, uint pinState, bool expected)
    {
        Assert.Equal(expected,
            CloudFileState.IsPinnedPlaceholderContentFullyOnDisk(fileLength, onDiskDataSize, pinState));
    }

    [Theory]
    [InlineData(@"D:\Yandex.Disk\photo.jpg", @"\\?\D:\Yandex.Disk\photo.jpg")]
    [InlineData(@"\\server\share\photo.jpg", @"\\?\UNC\server\share\photo.jpg")]
    [InlineData(@"\\?\D:\photo.jpg", @"\\?\D:\photo.jpg")]
    public void GetExtendedPath_ReturnsWin32LongPath(string path, string expected)
    {
        Assert.Equal(expected, CloudFileState.GetExtendedPath(path));
    }
}
