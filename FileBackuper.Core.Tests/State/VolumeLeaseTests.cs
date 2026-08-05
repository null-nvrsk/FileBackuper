using FileBackuper.Core;

namespace FileBackuper.Core.Tests.State;

public class VolumeLeaseTests
{
    [Fact]
    public void TryAcquire_ReturnsNullWhileAnotherLeaseForSameVolumeIsHeld()
    {
        using TestWorkspace workspace = new();
        using VolumeLease firstLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1"));

        VolumeLease? secondLease = VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1");

        Assert.Null(secondLease);
    }

    [Fact]
    public void TryAcquire_ReturnsLeaseAfterPreviousLeaseIsDisposed()
    {
        using TestWorkspace workspace = new();
        VolumeLease? firstLease = VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1");
        Assert.NotNull(firstLease);
        firstLease.Dispose();

        using VolumeLease secondLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1"));

        Assert.Equal("volume-1", secondLease.VolumeId);
    }
}
