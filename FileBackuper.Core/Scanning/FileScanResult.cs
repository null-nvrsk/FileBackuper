namespace FileBackuper.Core;

public sealed record FileScanResult(List<FileInfo> Files, int CloudFilesSkipped);
