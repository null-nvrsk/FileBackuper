using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Classification;

public class MediaFileAnalysisTests
{
    [Fact]
    public void DerivedProperties_ReflectMatchedPatternAndSkipReason()
    {
        MediaFileAnalysis analysis = new()
        {
            Kind = MediaKind.Image,
            DetectionSource = MediaDetectionSource.Extension,
            CameraEvidence = CameraEvidence.Pattern,
            MatchedCameraFileNamePattern = "^IMG_\\d+",
            SkipReason = "VideoBlacklist"
        };

        Assert.True(analysis.HasCameraFileNamePattern);
        Assert.True(analysis.IsSkipped);
    }

    [Fact]
    public void BackupFileCandidate_KeepsFileAndAnalysisTogether()
    {
        FileInfo file = new("IMG_0001.jpg");
        MediaFileAnalysis analysis = new()
        {
            Kind = MediaKind.Image,
            CameraEvidence = CameraEvidence.PatternAndExif
        };

        BackupFileCandidate candidate = new(file, analysis);

        Assert.Same(file, candidate.File);
        Assert.Same(analysis, candidate.Analysis);
    }
}
