using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace FileBackuper.Core;

public sealed class ExifMetadataReader : IExifMetadataReader
{
    public ExifMetadata Read(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            IReadOnlyList<MetadataExtractor.Directory> directories =
                ImageMetadataReader.ReadMetadata(file.FullName);
            ExifIfd0Directory? ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            ExifSubIfdDirectory? subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            string? make = Normalize(ifd0?.GetDescription(ExifDirectoryBase.TagMake));
            string? model = Normalize(ifd0?.GetDescription(ExifDirectoryBase.TagModel));
            DateTime? dateTimeOriginal = null;
            if (subIfd is not null &&
                subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime parsedDateTime))
            {
                dateTimeOriginal = parsedDateTime;
            }

            return new ExifMetadata(make, model, dateTimeOriginal);
        }
        catch (Exception exception) when (exception is ImageProcessingException or IOException or
                                           UnauthorizedAccessException or ArgumentException)
        {
            BackupLog.Verbose($"EXIF read failed | File={file.FullName} | " +
                $"Error={BackupLog.GetExceptionDescription(exception)}");
            return ExifMetadata.Empty;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('\0').Trim();
}
