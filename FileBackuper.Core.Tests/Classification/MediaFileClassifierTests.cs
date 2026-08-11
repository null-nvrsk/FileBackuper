using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Classification;

public class MediaFileClassifierTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.HEIC")]
    [InlineData("photo.cr3")]
    public void IsImage_ReturnsTrueForSupportedImageExtension(string fileName)
    {
        Assert.True(MediaFileClassifier.IsImage(new FileInfo(fileName)));
    }

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.MOV")]
    [InlineData("video.m2ts")]
    public void IsVideo_ReturnsTrueForSupportedVideoExtension(string fileName)
    {
        Assert.True(MediaFileClassifier.IsVideo(new FileInfo(fileName)));
    }

    [Fact]
    public void GetKindByExtension_ReturnsUnknownForFileWithoutExtension()
    {
        Assert.Equal(MediaKind.Unknown, MediaFileClassifier.GetKindByExtension(new FileInfo("cache-entry")));
    }

    [Fact]
    public void IsImageAndVideo_ReturnFalseForUnsupportedExtension()
    {
        FileInfo textFile = new("document.txt");

        Assert.False(MediaFileClassifier.IsImage(textFile));
        Assert.False(MediaFileClassifier.IsVideo(textFile));
    }
}
