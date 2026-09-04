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

    [Theory]
    [InlineData("document.txt")]
    [InlineData("backup.zip")]
    [InlineData("image.png")]
    [InlineData("video.mkv")]
    [InlineData("audio.mp3")]
    public void GetKindByExtension_ReturnsUnknownForCataloguedButDisabledExtension(string fileName)
    {
        Assert.Equal(MediaKind.Unknown, MediaFileClassifier.GetKindByExtension(new FileInfo(fileName)));
    }

    [Fact]
    public void FileTypeSelection_UsesIndividualExtensionsWhenCategoryIsDisabled()
    {
        FileCategoryOptions category = new()
        {
            Name = "Documents",
            Kind = MediaKind.Document,
            Enabled = false,
            Extensions = new() { "txt", "pdf", "docx" },
            EnabledExtensions = new() { ".PDF" }
        };
        FileTypeSelection selection = new(new[] { category });

        Assert.Equal(MediaKind.Document,
            MediaFileClassifier.GetKindByExtension(new FileInfo("selected.pdf"), selection));
        Assert.Equal(MediaKind.Unknown,
            MediaFileClassifier.GetKindByExtension(new FileInfo("not-selected.txt"), selection));
    }

    [Fact]
    public void FileTypeSelection_EnablesWholeCategoryRegardlessOfIndividualList()
    {
        FileCategoryOptions category = new()
        {
            Name = "Archives",
            Kind = MediaKind.Archive,
            Enabled = true,
            Extensions = new() { "zip", "rar" },
            EnabledExtensions = new() { "zip" }
        };
        FileTypeSelection selection = new(new[] { category });

        Assert.Equal(MediaKind.Archive,
            MediaFileClassifier.GetKindByExtension(new FileInfo("backup.rar"), selection));
    }

    [Fact]
    public void GetKindByExtension_ReturnsUnknownForFileWithoutExtension()
    {
        Assert.Equal(MediaKind.Unknown, MediaFileClassifier.GetKindByExtension(new FileInfo("cache-entry")));
    }

    [Fact]
    public void IsImageAndVideo_ReturnFalseForDocumentExtension()
    {
        FileInfo textFile = new("document.txt");

        Assert.False(MediaFileClassifier.IsImage(textFile));
        Assert.False(MediaFileClassifier.IsVideo(textFile));
    }
}
