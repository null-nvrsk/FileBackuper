using FileBackuper.Core;

namespace FileBackuper.Core.Tests.State;

public class StatePathsTests
{
    [Fact]
    public void ResolveStateDirectory_ReturnsConfiguredAbsolutePath()
    {
        string configuredDirectory = Path.Combine(Path.GetTempPath(), "FileBackuper.Tests", "state");

        string stateDirectory = StatePaths.ResolveStateDirectory(configuredDirectory);

        Assert.Equal(Path.GetFullPath(configuredDirectory), stateDirectory);
    }

    [Fact]
    public void ResolveStateDirectory_ReturnsProgramDataPathWhenSettingIsEmpty()
    {
        string stateDirectory = StatePaths.ResolveStateDirectory(string.Empty);
        string expectedDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FileBackuper",
            "state");

        Assert.Equal(expectedDirectory, stateDirectory);
    }
}
