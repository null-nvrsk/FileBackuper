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

        Stat.AddFileToCompletedStat(secondFile);

        Assert.Equal(100, Stat.GetPercentageOfCompletion());
        Stat.Reset();
    }
}
