namespace FileBackuper.Core;

public sealed class BackupFileDiagnosticFormatter
{
    private const string Separator = " | ";
    private const string EmptyValue = "-";
    private readonly FileSizeGroupService sizeGroupService;
    private readonly FolderPriorityService folderPriorityService;

    public BackupFileDiagnosticFormatter(FileSizeGroupService sizeGroupService,
        FolderPriorityService? folderPriorityService = null)
    {
        ArgumentNullException.ThrowIfNull(sizeGroupService);
        this.sizeGroupService = sizeGroupService;
        this.folderPriorityService = folderPriorityService ?? new FolderPriorityService();
    }

    public void Log(FileInfo file, MediaFileAnalysis analysis)
    {
        if (BackupLog.IsVerboseEnabled)
            BackupLog.Verbose(Format(file, analysis));
    }

    public string Format(FileInfo file, MediaFileAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(analysis);

        long? fileSize = analysis.FileSizeBytes ?? TryGetFileSize(file);
        string sizeGroup = fileSize.HasValue && sizeGroupService.TryGetGroup(fileSize.Value, out FileSizeGroupOptions? group)
            ? group!.Name
            : EmptyValue;
        string decision = analysis.IsSkipped
            ? $"Skip:{analysis.SkipReason}"
            : "Include";

        return string.Join(Separator,
            $"File={Escape(file.FullName)}",
            $"SizeBytes={fileSize?.ToString() ?? EmptyValue}",
            $"SizeGroup={Escape(sizeGroup)}",
            $"Kind={analysis.Kind}",
            $"Detection={analysis.DetectionSource}",
            $"DetectedExtension={Escape(analysis.DetectedExtension)}",
            $"CameraEvidence={analysis.CameraEvidence}",
            $"HasExif={analysis.HasCameraExif}",
            $"CameraMake={Escape(analysis.CameraMake)}",
            $"CameraModel={Escape(analysis.CameraModel)}",
            $"CameraPattern={Escape(analysis.MatchedCameraFileNamePattern)}",
            $"BlacklistPattern={Escape(analysis.MatchedVideoBlacklistPattern)}",
            $"FolderPriority={folderPriorityService.GetPriority(file)}",
            $"Decision={Escape(decision)}");
    }

    private static long? TryGetFileSize(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EmptyValue;

        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace(Separator, " \\| ", StringComparison.Ordinal);
    }
}
