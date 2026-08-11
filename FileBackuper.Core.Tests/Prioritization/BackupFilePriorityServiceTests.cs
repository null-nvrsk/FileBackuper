using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Prioritization;

public class BackupFilePriorityServiceTests
{
    private readonly BackupFilePriorityService service = new(new FileSizeGroupService(new[]
    {
        new FileSizeGroupOptions { Name = "Small", MinBytes = 10_000, MaxBytes = 99_999 },
        new FileSizeGroupOptions { Name = "Large", MinBytes = 100_000, MaxBytes = 1_000_000 }
    }));

    [Fact]
    public void L1_SizeGroupHasPriorityOverAllOtherLevels()
    {
        using TestWorkspace workspace = new();
        BackupFileCandidate small = CreateCandidate(workspace.CreateFile("small.jpg", 20_000),
            MediaDetectionSource.Signature, CameraEvidence.None, MediaKind.Image);
        BackupFileCandidate large = CreateCandidate(workspace.CreateFile(Path.Combine("DCIM", "large.jpg"), 200_000),
            MediaDetectionSource.Extension, CameraEvidence.PatternAndExif, MediaKind.Image);

        Assert.True(service.CompareByBackupPriority(small, large) < 0);
    }

    [Fact]
    public void L2_ExtensionHasPriorityOverSignatureAndMediaKindsAreEqual()
    {
        using TestWorkspace workspace = new();
        BackupFileCandidate extensionVideo = CreateCandidate(workspace.CreateFile("z-video.mp4", 20_000),
            MediaDetectionSource.Extension, CameraEvidence.None, MediaKind.Video);
        BackupFileCandidate signatureImage = CreateCandidate(workspace.CreateFile("a-cache-entry", 20_000),
            MediaDetectionSource.Signature, CameraEvidence.PatternAndExif, MediaKind.Image);

        Assert.True(service.CompareByBackupPriority(extensionVideo, signatureImage) < 0);
    }

    [Fact]
    public void L3_CameraEvidenceUsesConfiguredEvidenceOrder()
    {
        using TestWorkspace workspace = new();
        BackupFileCandidate none = CreateCandidate(workspace.CreateFile("d.jpg", 20_000),
            MediaDetectionSource.Extension, CameraEvidence.None);
        BackupFileCandidate pattern = CreateCandidate(workspace.CreateFile("c.jpg", 20_000),
            MediaDetectionSource.Extension, CameraEvidence.Pattern);
        BackupFileCandidate exif = CreateCandidate(workspace.CreateFile("b.jpg", 20_000),
            MediaDetectionSource.Extension, CameraEvidence.Exif);
        BackupFileCandidate both = CreateCandidate(workspace.CreateFile("a.jpg", 20_000),
            MediaDetectionSource.Extension, CameraEvidence.PatternAndExif);

        List<BackupFileCandidate> ordered = service.OrderByBackupPriority(
            new[] { none, pattern, exif, both }, CancellationToken.None);

        Assert.Equal(new[] { both, exif, pattern, none }, ordered);
    }

    [Fact]
    public void L4_CameraFolderHasPriorityOverDownloads()
    {
        using TestWorkspace workspace = new();
        BackupFileCandidate downloads = CreateCandidate(
            workspace.CreateFile(Path.Combine("Downloads", "a.jpg"), 20_000),
            MediaDetectionSource.Extension, CameraEvidence.None);
        BackupFileCandidate camera = CreateCandidate(
            workspace.CreateFile(Path.Combine("DCIM", "z.jpg"), 20_000),
            MediaDetectionSource.Extension, CameraEvidence.None);

        Assert.True(service.CompareByBackupPriority(camera, downloads) < 0);
    }

    [Fact]
    public void L5_FullPathProvidesStableOrder()
    {
        using TestWorkspace workspace = new();
        BackupFileCandidate second = CreateCandidate(workspace.CreateFile("b.jpg", 20_000),
            MediaDetectionSource.Extension, CameraEvidence.None);
        BackupFileCandidate first = CreateCandidate(workspace.CreateFile("a.jpg", 20_000),
            MediaDetectionSource.Extension, CameraEvidence.None);

        List<BackupFileCandidate> ordered = service.OrderByBackupPriority(
            new[] { second, first }, CancellationToken.None);

        Assert.Equal(new[] { first, second }, ordered);
    }

    private static BackupFileCandidate CreateCandidate(FileInfo file, MediaDetectionSource detectionSource,
        CameraEvidence cameraEvidence, MediaKind kind = MediaKind.Image) =>
        new(file, new MediaFileAnalysis
        {
            Kind = kind,
            DetectionSource = detectionSource,
            CameraEvidence = cameraEvidence,
            DetectedExtension = detectionSource == MediaDetectionSource.Extension ? file.Extension : ".jpg"
        });
}
