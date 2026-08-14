using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Prioritization;

public class FolderPriorityServiceTests
{
    [Theory]
    [InlineData("DCIM", 5)]
    [InlineData("Camera", 5)]
    [InlineData("Documents", 4)]
    [InlineData("Other", 3)]
    [InlineData("Temp", 2)]
    [InlineData("Downloads", 1)]
    public void GetPriority_ReturnsPriorityForDirectorySegment(string directoryName, int expectedPriority)
    {
        string root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        FileInfo file = new(Path.Combine(root, directoryName, "file.jpg"));

        int priority = new FolderPriorityService().GetPriority(file);

        Assert.Equal(expectedPriority, priority);
    }
}
