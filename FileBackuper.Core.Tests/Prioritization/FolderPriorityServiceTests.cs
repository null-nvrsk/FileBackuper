using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Prioritization;

public class FolderPriorityServiceTests
{
    [Theory]
    [InlineData("DCIM", 40)]
    [InlineData("Camera", 40)]
    [InlineData("Documents", 30)]
    [InlineData("Other", 20)]
    [InlineData("Temp", 10)]
    [InlineData("Downloads", 0)]
    public void GetPriority_ReturnsPriorityForDirectorySegment(string directoryName, int expectedPriority)
    {
        string root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        FileInfo file = new(Path.Combine(root, directoryName, "file.jpg"));

        int priority = new FolderPriorityService().GetPriority(file);

        Assert.Equal(expectedPriority, priority);
    }
}
