using System.Text;
using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Classification;

public class FileSignatureDetectorTests
{
    private readonly FileSignatureDetector detector = new();

    public static IEnumerable<object[]> KnownSignatures()
    {
        yield return new object[]
        {
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, MediaKind.Image, MediaFileFormat.Jpeg, ".jpg"
        };
        yield return new object[]
        {
            CreateRiff("AVI "), MediaKind.Video, MediaFileFormat.Avi, ".avi"
        };
        yield return new object[]
        {
            CreateFileTypeBox("heic"), MediaKind.Image, MediaFileFormat.Heic, ".heic"
        };
        yield return new object[]
        {
            CreateFileTypeBox("mp42"), MediaKind.Video, MediaFileFormat.Mp4, ".mp4"
        };
        yield return new object[]
        {
            CreateFileTypeBox("qt  "), MediaKind.Video, MediaFileFormat.QuickTime, ".mov"
        };
    }

    [Theory]
    [MemberData(nameof(KnownSignatures))]
    public void Detect_ReturnsMediaInformationForKnownSignature(byte[] header, MediaKind expectedKind,
        MediaFileFormat expectedFormat, string expectedExtension)
    {
        FileSignatureResult result = Assert.IsType<FileSignatureResult>(detector.Detect(header));

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedFormat, result.Format);
        Assert.Equal(expectedExtension, result.DetectedExtension);
    }

    [Fact]
    public void Detect_ReturnsNullForUnknownContent()
    {
        Assert.Null(detector.Detect(new byte[] { 1, 2, 3, 4 }));
    }

    public static IEnumerable<object[]> UnsupportedSignatures()
    {
        yield return new object[]
        {
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
        };
        yield return new object[] { Encoding.ASCII.GetBytes("GIF89a") };
        yield return new object[] { CreateRiff("WEBP") };
        yield return new object[] { CreateFileTypeBox("avif") };
        yield return new object[] { CreateEbml("webm") };
        yield return new object[] { CreateEbml("matroska") };
    }

    [Theory]
    [MemberData(nameof(UnsupportedSignatures))]
    public void Detect_ReturnsNullForUnsupportedSignature(byte[] header)
    {
        Assert.Null(detector.Detect(header));
    }

    [Fact]
    public void Detect_ReadsSignatureFromFileWithoutExtension()
    {
        using TestWorkspace workspace = new();
        string path = Path.Combine(workspace.RootDirectory.FullName, "cache-entry");
        File.WriteAllBytes(path, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        FileSignatureResult result = Assert.IsType<FileSignatureResult>(detector.Detect(new FileInfo(path)));

        Assert.Equal(MediaFileFormat.Jpeg, result.Format);
    }

    private static byte[] CreateRiff(string format)
    {
        byte[] result = new byte[12];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(result, 0);
        Encoding.ASCII.GetBytes(format).CopyTo(result, 8);
        return result;
    }

    private static byte[] CreateFileTypeBox(string brand)
    {
        byte[] result = new byte[16];
        result[3] = 16;
        Encoding.ASCII.GetBytes("ftyp").CopyTo(result, 4);
        Encoding.ASCII.GetBytes(brand).CopyTo(result, 8);
        return result;
    }

    private static byte[] CreateEbml(string documentType)
    {
        byte[] documentTypeBytes = Encoding.ASCII.GetBytes(documentType);
        byte[] result = new byte[4 + documentTypeBytes.Length];
        new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }.CopyTo(result, 0);
        documentTypeBytes.CopyTo(result, 4);
        return result;
    }
}
