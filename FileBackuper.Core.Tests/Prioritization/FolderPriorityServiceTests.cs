using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Prioritization;

public class FolderPriorityServiceTests
{
    [Theory]
    [InlineData("MyPrivateFiles", 6)]
    [InlineData("INTIMATE archive", 6)]
    [InlineData("для взрослых", 6)]
    [InlineData("суперсекретно", 6)]
    [InlineData("мои фото", 6)]
    [InlineData("DCIM", 5)]
    [InlineData("Camera", 5)]
    [InlineData("Фотоархив", 5)]
    [InlineData("фото ЗАГС", 5)]
    [InlineData("Documents", 4)]
    [InlineData("Other", 3)]
    [InlineData("Temp", 2)]
    [InlineData("Downloads", 1)]
    [InlineData("My downloads archive", 1)]
    [InlineData("Films 2024", 1)]
    [InlineData("Новые фильмы", 1)]
    [InlineData("Видеокурсы", 1)]
    public void GetPriority_ReturnsPriorityForDirectorySegment(string directoryName, int expectedPriority)
    {
        string root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        FileInfo file = new(Path.Combine(root, directoryName, "file.jpg"));

        int priority = new FolderPriorityService().GetPriority(file);

        Assert.Equal(expectedPriority, priority);
    }
}
