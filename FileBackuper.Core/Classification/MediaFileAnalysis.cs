namespace FileBackuper.Core;

public sealed record MediaFileAnalysis
{
    public long? FileSizeBytes { get; init; }

    public bool ExifAnalysisAttempted { get; init; }

    public TimeSpan ExifAnalysisDuration { get; init; }

    public bool SignatureAnalysisAttempted { get; init; }

    public TimeSpan SignatureAnalysisDuration { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    public MediaDetectionSource DetectionSource { get; init; } = MediaDetectionSource.None;

    public CameraEvidence CameraEvidence { get; init; } = CameraEvidence.None;

    public bool HasCameraExif { get; init; }

    public string? CameraMake { get; init; }

    public string? CameraModel { get; init; }

    public string? DetectedExtension { get; init; }

    public string? MatchedCameraFileNamePattern { get; init; }

    public string? MatchedVideoBlacklistPattern { get; init; }

    public string? SkipReason { get; init; }

    public bool HasCameraFileNamePattern =>
        !string.IsNullOrWhiteSpace(MatchedCameraFileNamePattern);

    public bool IsSkipped => !string.IsNullOrWhiteSpace(SkipReason);
}
