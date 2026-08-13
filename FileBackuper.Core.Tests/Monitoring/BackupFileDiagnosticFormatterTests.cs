using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Monitoring;

public class BackupFileDiagnosticFormatterTests
{
    private readonly BackupFileDiagnosticFormatter formatter = new(new FileSizeGroupService(new[]
    {
        new FileSizeGroupOptions { Name = "Small", MinBytes = 10_000, MaxBytes = 99_999 },
        new FileSizeGroupOptions { Name = "Large", MinBytes = 100_000, MaxBytes = 1_000_000 }
    }));

    [Fact]
    public void Format_WritesAllAnalysisFieldsOnOneDelimitedLine()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile(Path.Combine("DCIM", "IMG_0001.jpg"), 20_000);
        MediaFileAnalysis analysis = new()
        {
            Kind = MediaKind.Image,
            DetectionSource = MediaDetectionSource.Extension,
            DetectedExtension = ".jpg",
            CameraEvidence = CameraEvidence.PatternAndExif,
            HasCameraExif = true,
            CameraMake = "Canon",
            CameraModel = "EOS R",
            MatchedCameraFileNamePattern = "IMG_\\d+"
        };

        string result = formatter.Format(file, analysis);

        Assert.DoesNotContain('\r', result);
        Assert.DoesNotContain('\n', result);
        Assert.Equal(14, result.Split(" | ").Length);
        Assert.Contains("SizeGroup=Small | Kind=Image | Detection=Extension", result);
        Assert.Contains("CameraEvidence=PatternAndExif | HasExif=True", result);
        Assert.Contains("CameraMake=Canon | CameraModel=EOS R", result);
        Assert.Contains("FolderPriority=40 | Decision=Include", result);
    }

    [Fact]
    public void Format_HandlesSkippedOutOfRangeFileAndEscapesSeparatorInsidePattern()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("movie.mp4", 9_999);
        MediaFileAnalysis analysis = new()
        {
            Kind = MediaKind.Video,
            DetectionSource = MediaDetectionSource.Extension,
            MatchedVideoBlacklistPattern = "first | second",
            SkipReason = MediaSkipReasons.SizeOutOfRange
        };

        string result = formatter.Format(file, analysis);

        Assert.Contains("SizeGroup=-", result);
        Assert.Contains("BlacklistPattern=first \\| second", result);
        Assert.EndsWith("Decision=Skip:SizeOutOfRange", result);
        Assert.Equal(14, result.Split(" | ").Length);
    }

    [Fact]
    public void Format_OmitsExifFieldsWhenExifAnalysisIsDisabled()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("IMG_0001.jpg", 20_000);
        BackupFileDiagnosticFormatter formatterWithoutExif = new(new FileSizeGroupService(new[]
        {
            new FileSizeGroupOptions { Name = "Small", MinBytes = 10_000, MaxBytes = 99_999 }
        }), includeExifFields: false);
        MediaFileAnalysis analysis = new()
        {
            Kind = MediaKind.Image,
            CameraEvidence = CameraEvidence.Pattern,
            HasCameraExif = true,
            CameraMake = "Canon",
            CameraModel = "EOS R"
        };

        string result = formatterWithoutExif.Format(file, analysis);

        Assert.DoesNotContain("HasExif=", result);
        Assert.DoesNotContain("CameraMake=", result);
        Assert.DoesNotContain("CameraModel=", result);
        Assert.Equal(11, result.Split(" | ").Length);
    }

    [Fact]
    public void Format_OmitsSignatureFieldsWhenExtensionlessSearchIsDisabled()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("cache-file", 20_000);
        BackupFileDiagnosticFormatter formatterWithoutSignatures = new(new FileSizeGroupService(new[]
        {
            new FileSizeGroupOptions { Name = "Small", MinBytes = 10_000, MaxBytes = 99_999 }
        }), includeSignatureFields: false);
        MediaFileAnalysis analysis = new()
        {
            Kind = MediaKind.Image,
            DetectionSource = MediaDetectionSource.Signature,
            DetectedExtension = ".webp"
        };

        string result = formatterWithoutSignatures.Format(file, analysis);

        Assert.DoesNotContain("Detection=", result);
        Assert.DoesNotContain("Format=", result);
        Assert.DoesNotContain("DetectedExtension=", result);
        Assert.Equal(12, result.Split(" | ").Length);
    }
}
