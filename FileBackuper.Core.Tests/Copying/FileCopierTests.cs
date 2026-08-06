namespace FileBackuper.Core.Tests;

public class FileCopierTests
{
    [Fact]
    public void CopyFile_CopiesOnceAndThenReportsCurrentTarget()
    {
        using TestWorkspace workspace = new();
        FileInfo sourceFile = workspace.CreateFile("photo.jpg", 10);
        string destinationDirectory = Path.Combine(workspace.RootDirectory.FullName, "destination");

        bool firstResult = FileCopier.CopyFile(sourceFile, destinationDirectory, CancellationToken.None);
        bool secondResult = FileCopier.CopyFile(sourceFile, destinationDirectory, CancellationToken.None);

        Assert.False(firstResult);
        Assert.True(secondResult);
    }
}
