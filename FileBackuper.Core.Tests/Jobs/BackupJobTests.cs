namespace FileBackuper.Core.Tests;

public class BackupJobTests
{
    [Fact]
    public void SetScannedAndSortedFiles_UpdatesManifestAndKeepsQueue()
    {
        using TestWorkspace workspace = new();
        FileInfo firstFile = workspace.CreateFile("first.jpg", 10);
        FileInfo secondFile = workspace.CreateFile("second.mp4", 20);
        BackupFileCandidate firstCandidate = CreateCandidate(firstFile, MediaKind.Image);
        BackupFileCandidate secondCandidate = CreateCandidate(secondFile, MediaKind.Video);
        using VolumeLease lease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1"));

        BackupJob job = new(
            new DriveInfo(workspace.RootDirectory.Root.FullName),
            lease,
            new JobManifest { VolumeId = "volume-1", Status = JobStatus.Scanning });

        job.SetScannedFiles(new[] { firstCandidate, secondCandidate });

        Assert.Equal(JobStatus.Sorting, job.Status);
        Assert.Equal(2, job.Manifest.FilesFound);
        Assert.Equal(30, job.Manifest.TotalBytes);

        job.SetSortedFiles(new[] { secondCandidate, firstCandidate });

        Assert.Equal(JobStatus.Copying, job.Status);
        Assert.Equal(new[] { secondCandidate, firstCandidate }, job.Files);
    }

    [Fact]
    public void MarkFileCopied_UpdatesPerVolumeProgress()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("photo.jpg", 10);
        BackupFileCandidate candidate = CreateCandidate(file, MediaKind.Image);
        using VolumeLease lease = Assert.IsType<VolumeLease>(
            VolumeLease.TryAcquire(workspace.RootDirectory.FullName, "volume-1"));
        using BackupJob job = new(
            new DriveInfo(workspace.RootDirectory.Root.FullName),
            lease,
            new JobManifest { VolumeId = "volume-1", Status = JobStatus.Copying });

        job.MarkFileCopied(candidate);

        Assert.Equal(1, job.Manifest.FilesCompleted);
        Assert.Equal(10, job.Manifest.CompletedBytes);
        Assert.Equal(file.FullName, job.Manifest.CurrentFile);
    }

    private static BackupFileCandidate CreateCandidate(FileInfo file, MediaKind kind) =>
        new(file, new MediaFileAnalysis
        {
            Kind = kind,
            DetectionSource = MediaDetectionSource.Extension
        });
}
