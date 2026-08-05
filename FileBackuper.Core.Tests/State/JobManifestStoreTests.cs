using FileBackuper.Core;

namespace FileBackuper.Core.Tests.State;

public class JobManifestStoreTests
{
    [Fact]
    public void Read_ReturnsNullWhenManifestDoesNotExist()
    {
        using TestWorkspace workspace = new();
        JobManifestStore store = new(workspace.RootDirectory.FullName);

        JobManifest? manifest = store.Read("volume-1");

        Assert.Null(manifest);
    }

    [Fact]
    public void SaveAndRead_PreservesManifestData()
    {
        using TestWorkspace workspace = new();
        JobManifestStore store = new(workspace.RootDirectory.FullName);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        JobManifest expectedManifest = new()
        {
            VolumeId = "volume-1",
            CurrentDriveLetter = "H:\\",
            Status = JobStatus.Copying,
            OwnerInstanceId = "instance-1",
            OwnerProcessId = 1234,
            StartedUtc = now,
            LastHeartbeatUtc = now,
            DestinationDirectory = "C:\\Temp\\backup",
            FilesFound = 100,
            TotalBytes = 1_000_000,
            FilesCompleted = 25,
            CompletedBytes = 250_000,
            CurrentFile = "H:\\DCIM\\IMG_0001.jpg"
        };

        store.Save(expectedManifest);
        JobManifest actualManifest = Assert.IsType<JobManifest>(store.Read("volume-1"));

        Assert.Equal(expectedManifest.VolumeId, actualManifest.VolumeId);
        Assert.Equal(expectedManifest.Status, actualManifest.Status);
        Assert.Equal(expectedManifest.CurrentDriveLetter, actualManifest.CurrentDriveLetter);
        Assert.Equal(expectedManifest.FilesCompleted, actualManifest.FilesCompleted);
        Assert.Equal(expectedManifest.CurrentFile, actualManifest.CurrentFile);
    }

    [Fact]
    public void Save_ReplacesPreviousManifestAtomically()
    {
        using TestWorkspace workspace = new();
        JobManifestStore store = new(workspace.RootDirectory.FullName);

        store.Save(new JobManifest { VolumeId = "volume-1", Status = JobStatus.Scanning });
        store.Save(new JobManifest { VolumeId = "volume-1", Status = JobStatus.Completed, FilesCompleted = 10 });

        JobManifest manifest = Assert.IsType<JobManifest>(store.Read("volume-1"));
        Assert.Equal(JobStatus.Completed, manifest.Status);
        Assert.Equal(10, manifest.FilesCompleted);
    }
}
