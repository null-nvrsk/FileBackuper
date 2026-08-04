using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FileBackuper.Core;

public static class MediaFileClassifier
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".heic", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".raf", ".orf",
        ".rw2", ".pef", ".dng", ".rwl", ".raw", ".srw", ".x3f"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mpg", ".mov", ".avi", ".mts", ".m2ts", ".3gp", ".webm", ".mxf", ".ts", ".asf"
    };

    private static readonly string[] CameraFileNamePatterns =
    {
        "(img|mov)_\\d{4}\\.(jpe?g|avi)", "\\d{8}_\\d{6}\\.(jpe?g|mp4|mpg|mov|3gp)",
        "mvi_\\d{4}\\.avi", "(img|vid)-\\d{8}-wa\\d{4}\\.(jpe?g|mp4|mpg)",
        "\\d{4}-\\d{2}-\\d{2}\\s\\d{2}.\\d{2}.\\d{2}\\.jpe?g", "dsc.\\d{4}\\.jpe?g",
        "eos\\d{5}\\.jpe?g", "st._\\d{4}\\.jpg", "sany\\d{4}\\.jpg",
        "(photo|video)_\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2}.*\\.(jpe?g|mp4|mpg)",
        "\\d{4}-\\d{2}-\\d{2}\\s\\d{2}\\.\\d{2}\\.\\d{2}\\.(jpe?g|mp4|mpg)",
        "(p|s)\\d{7}\\.(jpe?g|mov|avi)", "foto\\s\\d{3}\\.jpg", "imag\\d{4}\\.(jpg|asf)",
        "\\d{4}\\.(jpg|asf)", "video\\d{4}\\.3gp", "m2u\\d{5}\\.mpg"
    };

    public static bool IsImage(FileInfo file) => ImageExtensions.Contains(file.Extension);
    public static bool IsVideo(FileInfo file) => VideoExtensions.Contains(file.Extension);
    public static bool HasCameraFileNamePattern(FileInfo file) =>
        CameraFileNamePatterns.Any(pattern => Regex.IsMatch(file.Name, pattern, RegexOptions.IgnoreCase));

    public static bool IsJpegByContent(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read);
            if (stream.Length < 4) return false;
            int first = stream.ReadByte();
            int second = stream.ReadByte();
            stream.Seek(-2, SeekOrigin.End);
            return first == 0xFF && second == 0xD8 && stream.ReadByte() == 0xFF && stream.ReadByte() == 0xD9;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Trace.TraceInformation($"Error reading file {filePath}: {exception.Message}");
            return false;
        }
    }

    public static bool IsHeicByContent(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            byte[] buffer = new byte[12];
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read);
            if (stream.Length < buffer.Length || stream.Read(buffer, 0, buffer.Length) != buffer.Length) return false;
            if (buffer[4] != 'f' || buffer[5] != 't' || buffer[6] != 'y' || buffer[7] != 'p') return false;
            return Encoding.ASCII.GetString(buffer, 8, 4) is "heic" or "heix" or "hevc" or "mif1" or "msf1";
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Trace.TraceInformation($"Error reading file {filePath}: {exception.Message}");
            return false;
        }
    }
}
