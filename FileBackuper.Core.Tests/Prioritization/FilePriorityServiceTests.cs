using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Prioritization;

public class FilePriorityServiceTests
{
    [Fact]
    public void CompareByBackupPriority_PlacesImageBeforeVideo()
    {
        using TestWorkspace workspace = new();
        FileInfo image = workspace.CreateFile("photo.jpg", 20_000);
        FileInfo video = workspace.CreateFile("video.mp4", 20_000);

        int result = FilePriorityService.CompareByBackupPriority(image, video);

        Assert.True(result < 0);
    }

    [Fact]
    public void OrderByBackupPriority_PutsCameraPatternBeforeOtherImageWithSameSize()
    {
        using TestWorkspace workspace = new();
        FileInfo ordinaryImage = workspace.CreateFile("photo.jpg", 100_000);
        FileInfo cameraImage = workspace.CreateFile("IMG_0001.jpg", 100_000);

        List<FileInfo> orderedFiles = FilePriorityService.OrderByBackupPriority(
            new[] { ordinaryImage, cameraImage }, CancellationToken.None);

        Assert.Equal(cameraImage.FullName, orderedFiles[0].FullName);
    }

    [Fact]
    public void OrderByBackupPriority_PutsImageBeforeVideo()
    {
        using TestWorkspace workspace = new();
        FileInfo video = workspace.CreateFile("clip.mp4", 100_000);
        FileInfo image = workspace.CreateFile("photo.jpg", 100_000);

        List<FileInfo> orderedFiles = FilePriorityService.OrderByBackupPriority(
            new[] { video, image }, CancellationToken.None);

        Assert.Equal(image.FullName, orderedFiles[0].FullName);
    }
}
