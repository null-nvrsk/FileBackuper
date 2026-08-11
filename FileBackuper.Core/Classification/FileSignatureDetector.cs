using System.Buffers.Binary;
using System.Text;

namespace FileBackuper.Core;

public sealed class FileSignatureDetector
{
    private const int MaximumHeaderSize = 64 * 1024;
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] Gif87aSignature = Encoding.ASCII.GetBytes("GIF87a");
    private static readonly byte[] Gif89aSignature = Encoding.ASCII.GetBytes("GIF89a");
    private static readonly byte[] RiffSignature = Encoding.ASCII.GetBytes("RIFF");
    private static readonly byte[] WebPSignature = Encoding.ASCII.GetBytes("WEBP");
    private static readonly byte[] AviSignature = Encoding.ASCII.GetBytes("AVI ");
    private static readonly byte[] FileTypeBoxSignature = Encoding.ASCII.GetBytes("ftyp");
    private static readonly byte[] EbmlSignature = { 0x1A, 0x45, 0xDF, 0xA3 };

    private static readonly HashSet<string> HeicBrands = new(StringComparer.Ordinal)
    {
        "heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1"
    };

    private static readonly HashSet<string> AvifBrands = new(StringComparer.Ordinal)
    {
        "avif", "avis"
    };

    private static readonly HashSet<string> Mp4Brands = new(StringComparer.Ordinal)
    {
        "isom", "iso2", "iso3", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1",
        "M4V ", "MSNV", "dash", "3gp4", "3gp5", "3g2a"
    };

    public FileSignatureResult? Detect(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            using FileStream stream = new(file.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            int headerLength = (int)Math.Min(stream.Length, MaximumHeaderSize);
            if (headerLength == 0)
                return null;

            byte[] header = new byte[headerLength];
            int totalRead = 0;
            while (totalRead < header.Length)
            {
                int read = stream.Read(header, totalRead, header.Length - totalRead);
                if (read == 0)
                    break;
                totalRead += read;
            }

            return Detect(header.AsSpan(0, totalRead));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            BackupLog.Warning($"Could not read file signature: {file.FullName}. " +
                BackupLog.GetExceptionDescription(exception));
            return null;
        }
    }

    public FileSignatureResult? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return new FileSignatureResult(MediaKind.Image, MediaFileFormat.Jpeg, ".jpg");

        if (header.Length >= 8 && header[..8].SequenceEqual(PngSignature))
            return new FileSignatureResult(MediaKind.Image, MediaFileFormat.Png, ".png");

        if (header.Length >= 6 &&
            (header[..6].SequenceEqual(Gif87aSignature) || header[..6].SequenceEqual(Gif89aSignature)))
            return new FileSignatureResult(MediaKind.Image, MediaFileFormat.Gif, ".gif");

        if (header.Length >= 12 && header[..4].SequenceEqual(RiffSignature))
        {
            if (header.Slice(8, 4).SequenceEqual(WebPSignature))
                return new FileSignatureResult(MediaKind.Image, MediaFileFormat.WebP, ".webp");
            if (header.Slice(8, 4).SequenceEqual(AviSignature))
                return new FileSignatureResult(MediaKind.Video, MediaFileFormat.Avi, ".avi");
        }

        FileSignatureResult? isoBaseMediaResult = DetectIsoBaseMediaFormat(header);
        if (isoBaseMediaResult is not null)
            return isoBaseMediaResult;

        if (header.Length >= 4 &&
            header[..4].SequenceEqual(EbmlSignature))
        {
            bool isWebM = ContainsAsciiIgnoreCase(header, "webm");
            return isWebM
                ? new FileSignatureResult(MediaKind.Video, MediaFileFormat.WebM, ".webm")
                : new FileSignatureResult(MediaKind.Video, MediaFileFormat.Matroska, ".mkv");
        }

        return null;
    }

    private static FileSignatureResult? DetectIsoBaseMediaFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12 || !header.Slice(4, 4).SequenceEqual(FileTypeBoxSignature))
            return null;

        uint declaredBoxSize = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
        int boxEnd = declaredBoxSize is >= 16 and <= MaximumHeaderSize
            ? Math.Min((int)declaredBoxSize, header.Length)
            : header.Length;

        List<string> brands = new() { Encoding.ASCII.GetString(header.Slice(8, 4)) };
        for (int offset = 16; offset + 4 <= boxEnd; offset += 4)
            brands.Add(Encoding.ASCII.GetString(header.Slice(offset, 4)));

        if (brands.Any(AvifBrands.Contains))
            return new FileSignatureResult(MediaKind.Image, MediaFileFormat.Avif, ".avif");
        if (brands.Any(HeicBrands.Contains))
            return new FileSignatureResult(MediaKind.Image, MediaFileFormat.Heic, ".heic");
        if (brands.Contains("qt  ", StringComparer.Ordinal))
            return new FileSignatureResult(MediaKind.Video, MediaFileFormat.QuickTime, ".mov");
        if (brands.Any(Mp4Brands.Contains))
            return new FileSignatureResult(MediaKind.Video, MediaFileFormat.Mp4, ".mp4");

        return null;
    }

    private static bool ContainsAsciiIgnoreCase(ReadOnlySpan<byte> value, string text)
    {
        byte[] expected = Encoding.ASCII.GetBytes(text);
        for (int offset = 0; offset + expected.Length <= value.Length; offset++)
        {
            bool matches = true;
            for (int index = 0; index < expected.Length; index++)
            {
                byte actual = value[offset + index];
                if (actual >= 'A' && actual <= 'Z')
                    actual = (byte)(actual + ('a' - 'A'));
                if (actual != expected[index])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return true;
        }

        return false;
    }
}
