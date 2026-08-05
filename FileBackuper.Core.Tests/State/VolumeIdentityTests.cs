using FileBackuper.Core;

namespace FileBackuper.Core.Tests.State;

public class VolumeIdentityTests
{
    [Fact]
    public void GetVolumeId_ReturnsWindowsVolumeGuidForReadyDrive()
    {
        string applicationDriveRoot = Path.GetPathRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Не удалось определить диск тестового процесса.");
        DriveInfo drive = new(applicationDriveRoot);

        string volumeId = VolumeIdentity.GetVolumeId(drive);

        Assert.StartsWith("\\\\?\\Volume{", volumeId, StringComparison.Ordinal);
        Assert.EndsWith("}\\", volumeId, StringComparison.Ordinal);
    }
}
