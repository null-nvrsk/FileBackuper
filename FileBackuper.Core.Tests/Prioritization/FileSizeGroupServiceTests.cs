using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Prioritization;

public class FileSizeGroupServiceTests
{
    private readonly FileSizeGroupService service = new(new[]
    {
        new FileSizeGroupOptions { Name = "Small", MinBytes = 10, MaxBytes = 20 },
        new FileSizeGroupOptions { Name = "Large", MinBytes = 21, MaxBytes = 30 }
    });

    [Theory]
    [InlineData(10, 0, "Small")]
    [InlineData(20, 0, "Small")]
    [InlineData(21, 1, "Large")]
    [InlineData(30, 1, "Large")]
    public void GetGroup_ReturnsConfiguredGroupForBoundaryValues(long size, int expectedIndex,
        string expectedName)
    {
        Assert.Equal(expectedIndex, service.GetGroupIndex(size));
        Assert.Equal(expectedName, service.GetGroup(size).Name);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(31)]
    public void GetGroupIndex_ThrowsWhenSizeIsOutsideConfiguredGroups(long size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetGroupIndex(size));
    }
}
