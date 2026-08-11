namespace FileBackuper.Core;

public interface IExifMetadataReader
{
    ExifMetadata Read(FileInfo file);
}
