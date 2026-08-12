namespace FileBackuper.Core;

public sealed class MediaFileAnalysisService
{
    private readonly long minFileSizeBytes;
    private readonly long maxFileSizeBytes;
    private readonly RegexPatternSet cameraFileNamePatterns;
    private readonly RegexPatternSet videoBlacklistPatterns;
    private readonly IExifMetadataReader exifMetadataReader;
    private readonly FileSignatureDetector signatureDetector;
    private readonly bool enableExifAnalysis;

    public MediaFileAnalysisService(long minFileSizeBytes, long maxFileSizeBytes,
        RegexPatternSet cameraFileNamePatterns, RegexPatternSet videoBlacklistPatterns,
        IExifMetadataReader? exifMetadataReader = null, FileSignatureDetector? signatureDetector = null,
        bool enableExifAnalysis = true)
    {
        if (minFileSizeBytes < 0 || maxFileSizeBytes < minFileSizeBytes)
            throw new ArgumentException("The media file size range is invalid.");
        ArgumentNullException.ThrowIfNull(cameraFileNamePatterns);
        ArgumentNullException.ThrowIfNull(videoBlacklistPatterns);

        this.minFileSizeBytes = minFileSizeBytes;
        this.maxFileSizeBytes = maxFileSizeBytes;
        this.cameraFileNamePatterns = cameraFileNamePatterns;
        this.videoBlacklistPatterns = videoBlacklistPatterns;
        this.exifMetadataReader = exifMetadataReader ?? new ExifMetadataReader();
        this.signatureDetector = signatureDetector ?? new FileSignatureDetector();
        this.enableExifAnalysis = enableExifAnalysis;
    }

    public MediaFileAnalysis Analyze(FileInfo file, bool allowSignatureDetection)
    {
        ArgumentNullException.ThrowIfNull(file);

        long fileSize;
        try
        {
            fileSize = file.Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            BackupLog.Verbose($"Analysis skipped | Reason={MediaSkipReasons.FileUnavailable} | " +
                $"File={file.FullName} | Error={BackupLog.GetExceptionDescription(exception)}");
            return new MediaFileAnalysis { SkipReason = MediaSkipReasons.FileUnavailable };
        }

        if (fileSize < minFileSizeBytes || fileSize > maxFileSizeBytes)
        {
            return new MediaFileAnalysis
            {
                FileSizeBytes = fileSize,
                SkipReason = MediaSkipReasons.SizeOutOfRange
            };
        }

        MediaKind kind = MediaFileClassifier.GetKindByExtension(file);
        MediaDetectionSource detectionSource = kind == MediaKind.Unknown
            ? MediaDetectionSource.None
            : MediaDetectionSource.Extension;
        string? detectedExtension = detectionSource == MediaDetectionSource.Extension
            ? file.Extension.ToLowerInvariant()
            : null;
        bool signatureAnalysisAttempted = false;
        TimeSpan signatureAnalysisDuration = TimeSpan.Zero;

        if (kind == MediaKind.Unknown && allowSignatureDetection)
        {
            signatureAnalysisAttempted = true;
            long signatureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            FileSignatureResult? signature;
            try
            {
                signature = signatureDetector.Detect(file);
            }
            finally
            {
                signatureAnalysisDuration = GetElapsedTime(signatureStarted);
            }

            if (signature is not null)
            {
                kind = signature.Kind;
                detectionSource = MediaDetectionSource.Signature;
                detectedExtension = signature.DetectedExtension;
            }
        }

        if (kind == MediaKind.Unknown)
        {
            return new MediaFileAnalysis
            {
                FileSizeBytes = fileSize,
                SignatureAnalysisAttempted = signatureAnalysisAttempted,
                SignatureAnalysisDuration = signatureAnalysisDuration,
                Kind = MediaKind.Unknown,
                DetectionSource = MediaDetectionSource.None,
                SkipReason = MediaSkipReasons.UnsupportedMediaType
            };
        }

        string? matchedCameraPattern = cameraFileNamePatterns.FindMatchingPattern(file.Name);
        bool exifAnalysisAttempted = kind == MediaKind.Image && enableExifAnalysis;
        TimeSpan exifAnalysisDuration = TimeSpan.Zero;
        ExifMetadata exifMetadata = ExifMetadata.Empty;
        if (exifAnalysisAttempted)
        {
            long exifStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                exifMetadata = exifMetadataReader.Read(file);
            }
            finally
            {
                exifAnalysisDuration = GetElapsedTime(exifStarted);
            }
        }
        bool hasCameraExif = exifMetadata.HasCameraInfo;
        CameraEvidence cameraEvidence = GetCameraEvidence(matchedCameraPattern is not null, hasCameraExif);

        string? matchedBlacklistPattern = kind == MediaKind.Video
            ? videoBlacklistPatterns.FindMatchingPattern(file.Name)
            : null;
        string? skipReason = matchedBlacklistPattern is not null && matchedCameraPattern is null
            ? MediaSkipReasons.VideoBlacklist
            : null;

        return new MediaFileAnalysis
        {
            FileSizeBytes = fileSize,
            ExifAnalysisAttempted = exifAnalysisAttempted,
            ExifAnalysisDuration = exifAnalysisDuration,
            SignatureAnalysisAttempted = signatureAnalysisAttempted,
            SignatureAnalysisDuration = signatureAnalysisDuration,
            Kind = kind,
            DetectionSource = detectionSource,
            CameraEvidence = cameraEvidence,
            HasCameraExif = hasCameraExif,
            CameraMake = exifMetadata.CameraMake,
            CameraModel = exifMetadata.CameraModel,
            DetectedExtension = detectedExtension,
            MatchedCameraFileNamePattern = matchedCameraPattern,
            MatchedVideoBlacklistPattern = matchedBlacklistPattern,
            SkipReason = skipReason
        };
    }

    private static CameraEvidence GetCameraEvidence(bool hasPattern, bool hasExif) => (hasPattern, hasExif) switch
    {
        (true, true) => CameraEvidence.PatternAndExif,
        (false, true) => CameraEvidence.Exif,
        (true, false) => CameraEvidence.Pattern,
        _ => CameraEvidence.None
    };

    private static TimeSpan GetElapsedTime(long startedTimestamp) => TimeSpan.FromSeconds(
        (double)(System.Diagnostics.Stopwatch.GetTimestamp() - startedTimestamp) /
        System.Diagnostics.Stopwatch.Frequency);
}
