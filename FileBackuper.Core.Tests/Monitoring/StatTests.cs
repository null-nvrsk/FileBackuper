namespace FileBackuper.Core.Tests;

public class StatTests
{
    [Fact]
    public void RegisteredFilesAndCompletedFiles_UpdateCommonProgress()
    {
        using TestWorkspace workspace = new();
        FileInfo firstFile = workspace.CreateFile("first.jpg", 10);
        FileInfo secondFile = workspace.CreateFile("second.mp4", 20);
        Stat.Reset();

        Stat.AddFilesToTotalStat(new[] { firstFile, secondFile });
        Stat.AddFileToCompletedStat(firstFile);

        Assert.Equal(33, Stat.GetPercentageOfCompletion());
        StatSnapshot partialSnapshot = Stat.GetSnapshot();
        Assert.Equal(2, partialSnapshot.TotalFileCount);
        Assert.Equal(30, partialSnapshot.TotalSize);
        Assert.Equal(1, partialSnapshot.CompletedFileCount);
        Assert.Equal(10, partialSnapshot.CompletedSize);
        Assert.Equal(33, partialSnapshot.Percentage);

        Stat.RemoveFileFromTotalStat(secondFile, secondFile.Length);
        StatSnapshot reducedSnapshot = Stat.GetSnapshot();
        Assert.Equal(1, reducedSnapshot.TotalFileCount);
        Assert.Equal(10, reducedSnapshot.TotalSize);
        Assert.Equal(100, reducedSnapshot.Percentage);

        Stat.Reset();
    }
}
