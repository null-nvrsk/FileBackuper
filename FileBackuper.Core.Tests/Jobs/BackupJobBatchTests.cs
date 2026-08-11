namespace FileBackuper.Core.Tests;

public class BackupJobBatchTests
{
    [Fact]
    public void SetSortedFiles_CreatesOneQueueAndPreservesSourceJob()
    {
        using TestWorkspace workspace = new();
        FileInfo firstFile = workspace.CreateFile("first.jpg", 10);
        FileInfo secondFile = workspace.CreateFile("second.mp4", 20);
        BackupFileCandidate firstCandidate = CreateCandidate(firstFile, MediaKind.Image);
        BackupFileCandidate secondCandidate = CreateCandidate(secondFile, MediaKind.Video);
        using VolumeLease firstLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1"));
        using VolumeLease secondLease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-2"));
        DriveInfo drive = new(workspace.RootDirectory.Root.FullName);
        BackupJob firstJob = new(drive, firstLease, new JobManifest { VolumeId = "volume-1", Status = JobStatus.Scanning });
        BackupJob secondJob = new(drive, secondLease, new JobManifest { VolumeId = "volume-2", Status = JobStatus.Scanning });
        firstJob.SetScannedFiles(new[] { firstCandidate });
        secondJob.SetScannedFiles(new[] { secondCandidate });
        BackupJobBatch batch = new(new[] { firstJob, secondJob });

        batch.CollectScannedFiles();
        batch.SetSortedFiles(new[] { secondCandidate, firstCandidate });

        Assert.Equal(new[] { secondCandidate, firstCandidate }, batch.Files);
        Assert.Same(secondJob, batch.GetSourceJob(secondCandidate));
        Assert.Same(firstJob, batch.GetSourceJob(firstCandidate));
        Assert.Equal(JobStatus.Copying, firstJob.Status);
        Assert.Equal(JobStatus.Copying, secondJob.Status);
    }

    private static BackupFileCandidate CreateCandidate(FileInfo file, MediaKind kind) =>
        new(file, new MediaFileAnalysis
        {
            Kind = kind,
            DetectionSource = MediaDetectionSource.Extension
        });
}
