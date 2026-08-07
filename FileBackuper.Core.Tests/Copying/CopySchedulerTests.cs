namespace FileBackuper.Core.Tests;

public class CopySchedulerTests
{
    [Fact]
    public void TryTakeNext_SelectsHighestPriorityHeadAcrossJobQueues()
    {
        using TestWorkspace workspace = new();
        FileInfo image = workspace.CreateFile("photo.jpg", 20_000);
        FileInfo video = workspace.CreateFile("video.mp4", 20_000);
        using VolumeLease imageLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "image-volume"));
        using VolumeLease videoLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "video-volume"));
        DriveInfo drive = new(workspace.RootDirectory.Root.FullName);
        BackupJob imageJob = CreateCopyingJob(drive, imageLease, "image-volume", image);
        BackupJob videoJob = CreateCopyingJob(drive, videoLease, "video-volume", video);
        CopyScheduler scheduler = new(new JobManifestStore(workspace.RootDirectory.FullName));
        scheduler.Enqueue(videoJob);
        scheduler.Enqueue(imageJob);

        bool found = scheduler.TryTakeNext(out ScheduledFile scheduledFile);

        Assert.True(found);
        Assert.Same(image, scheduledFile.File);
        Assert.Same(imageJob, scheduledFile.Job);
    }

    [Fact]
    public async Task CopyAvailableAsync_SavesProgressForTheSourceVolumeAfterEachFile()
    {
        using TestWorkspace workspace = new();
        FileInfo sourceFile = workspace.CreateFile("photo.jpg", 20_000);
        using VolumeLease lease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1"));
        DriveInfo drive = new(workspace.RootDirectory.Root.FullName);
        BackupJob job = CreateCopyingJob(drive, lease, "volume-1", sourceFile);
        JobManifestStore store = new(workspace.RootDirectory.FullName);
        CopyScheduler scheduler = new(store);
        scheduler.Enqueue(job);

        await scheduler.CopyAvailableAsync(Path.Combine(workspace.RootDirectory.FullName, "destination"),
            CancellationToken.None);

        JobManifest manifest = Assert.IsType<JobManifest>(store.Read("volume-1"));
        Assert.Equal(JobStatus.Completed, manifest.Status);
        Assert.Equal(1, manifest.FilesCompleted);
        Assert.Equal(sourceFile.Length, manifest.CompletedBytes);
        Assert.Equal(sourceFile.FullName, manifest.CurrentFile);
        Assert.Equal(0, scheduler.PendingFileCount);
        Assert.False(await scheduler.CopyNextAsync(workspace.RootDirectory.FullName, CancellationToken.None));
    }

    [Fact]
    public async Task NewJobAddedDuringCopying_IsSelectedByPriorityForTheNextFile()
    {
        using TestWorkspace workspace = new();
        FileInfo firstVideo = workspace.CreateFile("first-video.mp4", 20_000);
        FileInfo newImage = workspace.CreateFile("new-image.jpg", 20_000);
        using VolumeLease firstLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "first-volume"));
        using VolumeLease newLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "new-volume"));
        DriveInfo drive = new(workspace.RootDirectory.Root.FullName);
        BackupJob firstJob = CreateCopyingJob(drive, firstLease, "first-volume", firstVideo);
        BackupJob newJob = CreateCopyingJob(drive, newLease, "new-volume", newImage);
        CopyScheduler scheduler = new(new JobManifestStore(workspace.RootDirectory.FullName));
        scheduler.Enqueue(firstJob);

        Assert.True(await scheduler.CopyNextAsync(Path.Combine(workspace.RootDirectory.FullName, "destination"),
            CancellationToken.None));
        scheduler.Enqueue(newJob);

        Assert.True(scheduler.TryTakeNext(out ScheduledFile scheduledFile));
        Assert.Same(newImage, scheduledFile.File);
        Assert.Same(newJob, scheduledFile.Job);
    }

    [Fact]
    public async Task ExistingTarget_IsRemovedFromTotalAndNotCountedAsCopied()
    {
        using TestWorkspace workspace = new();
        FileInfo sourceFile = workspace.CreateFile("existing.jpg", 20_000);
        string destination = Path.Combine(workspace.RootDirectory.FullName, "destination");
        Assert.False(FileCopier.CopyFile(sourceFile, destination, CancellationToken.None));
        using VolumeLease lease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "existing-volume"));
        BackupJob job = CreateCopyingJob(new DriveInfo(workspace.RootDirectory.Root.FullName), lease,
            "existing-volume", sourceFile);
        JobManifestStore store = new(workspace.RootDirectory.FullName);
        CopyScheduler scheduler = new(store);
        Stat.Reset();
        Stat.AddFilesToTotalStat(new[] { sourceFile });
        scheduler.Enqueue(job);

        Assert.True(await scheduler.CopyNextAsync(destination, CancellationToken.None));

        StatSnapshot snapshot = Stat.GetSnapshot();
        Assert.Equal(0, snapshot.TotalFileCount);
        Assert.Equal(0, snapshot.TotalSize);
        Assert.Equal(0, snapshot.CompletedFileCount);
        Assert.Equal(0, snapshot.CompletedSize);
        JobManifest manifest = Assert.IsType<JobManifest>(store.Read("existing-volume"));
        Assert.Equal(JobStatus.Completed, manifest.Status);
        Assert.Equal(0, manifest.FilesFound);
        Assert.Equal(0, manifest.TotalBytes);
        Assert.Equal(0, manifest.FilesCompleted);
        Assert.Equal(0, manifest.CompletedBytes);
        Stat.Reset();
    }

    private static BackupJob CreateCopyingJob(DriveInfo drive, VolumeLease lease, string volumeId, FileInfo file)
    {
        BackupJob job = new(drive, lease, new JobManifest { VolumeId = volumeId, Status = JobStatus.Scanning });
        job.SetScannedFiles(new[] { file });
        job.SetSortedFiles(new[] { file });
        return job;
    }
}
