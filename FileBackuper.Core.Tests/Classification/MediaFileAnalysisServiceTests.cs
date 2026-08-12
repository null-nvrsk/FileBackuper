using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Classification;

public class MediaFileAnalysisServiceTests
{
    [Fact]
    public void Analyze_CombinesCameraPatternAndExifEvidence()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("IMG_0001.jpg", 20_000);
        StubExifMetadataReader exifReader = new(new ExifMetadata("Canon", "EOS R", null));
        MediaFileAnalysisService service = CreateService(workspace, "^IMG_\\d+", "remux", exifReader);

        MediaFileAnalysis result = service.Analyze(file, allowSignatureDetection: false);

        Assert.Equal(MediaKind.Image, result.Kind);
        Assert.Equal(MediaDetectionSource.Extension, result.DetectionSource);
        Assert.Equal(CameraEvidence.PatternAndExif, result.CameraEvidence);
        Assert.Equal("Canon", result.CameraMake);
        Assert.True(result.ExifAnalysisAttempted);
        Assert.True(result.ExifAnalysisDuration >= TimeSpan.Zero);
        Assert.False(result.SignatureAnalysisAttempted);
        Assert.False(result.IsSkipped);
    }

    [Fact]
    public void Analyze_UsesExifWithoutFileNamePattern()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("holiday.jpg", 20_000);
        MediaFileAnalysisService service = CreateService(workspace, "^IMG_", "remux",
            new StubExifMetadataReader(new ExifMetadata("Nikon", null, null)));

        MediaFileAnalysis result = service.Analyze(file, allowSignatureDetection: false);

        Assert.Equal(CameraEvidence.Exif, result.CameraEvidence);
    }

    [Fact]
    public void Analyze_UsesCameraPatternForVideoWithoutReadingExif()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("VID_0001.mp4", 20_000);
        StubExifMetadataReader exifReader = new(new ExifMetadata("Canon", null, null));
        MediaFileAnalysisService service = CreateService(workspace, "^VID_\\d+", "remux", exifReader);

        MediaFileAnalysis result = service.Analyze(file, allowSignatureDetection: false);

        Assert.Equal(MediaKind.Video, result.Kind);
        Assert.Equal(CameraEvidence.Pattern, result.CameraEvidence);
        Assert.Equal(0, exifReader.ReadCount);
    }

    [Fact]
    public void Analyze_DetectsExtensionlessMediaOnlyWhenSignatureDetectionIsAllowed()
    {
        using TestWorkspace workspace = new();
        string path = Path.Combine(workspace.RootDirectory.FullName, "cache-entry");
        byte[] content = new byte[20_000];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;
        File.WriteAllBytes(path, content);
        FileInfo file = new(path);
        MediaFileAnalysisService service = CreateService(workspace, "^IMG_", "remux",
            new StubExifMetadataReader(ExifMetadata.Empty));

        MediaFileAnalysis denied = service.Analyze(file, allowSignatureDetection: false);
        MediaFileAnalysis allowed = service.Analyze(file, allowSignatureDetection: true);

        Assert.Equal(MediaSkipReasons.UnsupportedMediaType, denied.SkipReason);
        Assert.Equal(MediaKind.Image, allowed.Kind);
        Assert.Equal(MediaDetectionSource.Signature, allowed.DetectionSource);
        Assert.Equal(".jpg", allowed.DetectedExtension);
        Assert.True(allowed.SignatureAnalysisAttempted);
        Assert.True(allowed.SignatureAnalysisDuration >= TimeSpan.Zero);
        Assert.True(allowed.ExifAnalysisAttempted);
    }

    [Fact]
    public void Analyze_SkipsBlacklistedVideoUsingRegex()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("Movie.REMUX.1080p.mp4", 20_000);
        MediaFileAnalysisService service = CreateService(workspace, "^VID_", "remux|web[-_. ]?dl",
            new StubExifMetadataReader(ExifMetadata.Empty));

        MediaFileAnalysis result = service.Analyze(file, allowSignatureDetection: false);

        Assert.Equal(MediaSkipReasons.VideoBlacklist, result.SkipReason);
        Assert.Equal("remux|web[-_. ]?dl", result.MatchedVideoBlacklistPattern);
    }

    [Fact]
    public void Analyze_CameraPatternProtectsVideoFromGenericBlacklistMatch()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("VID_20240101_1080p.mp4", 20_000);
        MediaFileAnalysisService service = CreateService(workspace, "^VID_", "1080p",
            new StubExifMetadataReader(ExifMetadata.Empty));

        MediaFileAnalysis result = service.Analyze(file, allowSignatureDetection: false);

        Assert.False(result.IsSkipped);
        Assert.Equal(CameraEvidence.Pattern, result.CameraEvidence);
        Assert.Equal("1080p", result.MatchedVideoBlacklistPattern);
    }

    [Fact]
    public void Analyze_SkipsOutOfRangeFileBeforeReadingExif()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("small.jpg", 9_999);
        StubExifMetadataReader exifReader = new(new ExifMetadata("Canon", null, null));
        MediaFileAnalysisService service = CreateService(workspace, "small", "remux", exifReader);

        MediaFileAnalysis result = service.Analyze(file, allowSignatureDetection: false);

        Assert.Equal(MediaSkipReasons.SizeOutOfRange, result.SkipReason);
        Assert.Equal(0, exifReader.ReadCount);
    }

    private static MediaFileAnalysisService CreateService(TestWorkspace workspace, string cameraPattern,
        string blacklistPattern, IExifMetadataReader exifReader)
    {
        string cameraPatternPath = Path.Combine(workspace.RootDirectory.FullName,
            Guid.NewGuid().ToString("N") + "-camera.txt");
        string blacklistPatternPath = Path.Combine(workspace.RootDirectory.FullName,
            Guid.NewGuid().ToString("N") + "-blacklist.txt");
        File.WriteAllText(cameraPatternPath, cameraPattern);
        File.WriteAllText(blacklistPatternPath, blacklistPattern);

        return new MediaFileAnalysisService(10_000, 4_000_000_000,
            RegexPatternSet.Load(cameraPatternPath), RegexPatternSet.Load(blacklistPatternPath), exifReader);
    }

    private sealed class StubExifMetadataReader : IExifMetadataReader
    {
        private readonly ExifMetadata metadata;

        public StubExifMetadataReader(ExifMetadata metadata)
        {
            this.metadata = metadata;
        }

        public int ReadCount { get; private set; }

        public ExifMetadata Read(FileInfo file)
        {
            ReadCount++;
            return metadata;
        }
    }
}
