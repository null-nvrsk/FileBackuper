using System.Buffers.Binary;
using System.Text;
using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Classification;

public class ExifMetadataReaderTests
{
    [Fact]
    public void Read_ReturnsCameraMakeModelAndOriginalDate()
    {
        using TestWorkspace workspace = new();
        string path = Path.Combine(workspace.RootDirectory.FullName, "photo.jpg");
        File.WriteAllBytes(path, CreateJpegWithExif());
        ExifMetadataReader reader = new();

        ExifMetadata metadata = reader.Read(new FileInfo(path));

        Assert.Equal("Canon", metadata.CameraMake);
        Assert.Equal("EOS R", metadata.CameraModel);
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), metadata.DateTimeOriginal);
        Assert.True(metadata.HasCameraInfo);
    }

    [Fact]
    public void Read_ReturnsEmptyMetadataForInvalidImage()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("invalid.jpg", 100);

        ExifMetadata metadata = new ExifMetadataReader().Read(file);

        Assert.Equal(ExifMetadata.Empty, metadata);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("unknown", "N/A", false)]
    [InlineData("Canon", null, true)]
    [InlineData(null, "EOS R", true)]
    public void HasCameraInfo_RequiresMeaningfulMakeOrModel(string? make, string? model, bool expected)
    {
        Assert.Equal(expected, new ExifMetadata(make, model, null).HasCameraInfo);
    }

    private static byte[] CreateJpegWithExif()
    {
        byte[] tiff = new byte[100];
        tiff[0] = (byte)'I';
        tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4), 8);

        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(8), 3);
        WriteIfdEntry(tiff, 10, 0x010F, 2, 6, 50);
        WriteIfdEntry(tiff, 22, 0x0110, 2, 6, 56);
        WriteIfdEntry(tiff, 34, 0x8769, 4, 1, 62);
        Encoding.ASCII.GetBytes("Canon\0").CopyTo(tiff, 50);
        Encoding.ASCII.GetBytes("EOS R\0").CopyTo(tiff, 56);

        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(62), 1);
        WriteIfdEntry(tiff, 64, 0x9003, 2, 20, 80);
        Encoding.ASCII.GetBytes("2024:01:02 03:04:05\0").CopyTo(tiff, 80);

        byte[] exifPayload = new byte[6 + tiff.Length];
        Encoding.ASCII.GetBytes("Exif\0\0").CopyTo(exifPayload, 0);
        tiff.CopyTo(exifPayload, 6);

        byte[] jpeg = new byte[2 + 2 + 2 + exifPayload.Length + 2];
        jpeg[0] = 0xFF;
        jpeg[1] = 0xD8;
        jpeg[2] = 0xFF;
        jpeg[3] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(4), (ushort)(exifPayload.Length + 2));
        exifPayload.CopyTo(jpeg, 6);
        jpeg[^2] = 0xFF;
        jpeg[^1] = 0xD9;
        return jpeg;
    }

    private static void WriteIfdEntry(byte[] target, int offset, ushort tag, ushort type, uint count, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset + 2), type);
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset + 4), count);
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset + 8), value);
    }
}
