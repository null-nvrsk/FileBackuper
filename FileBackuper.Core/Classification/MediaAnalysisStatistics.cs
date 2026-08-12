namespace FileBackuper.Core;

public sealed record MediaAnalysisStatistics(
    long ExifFilesAnalyzed,
    TimeSpan ExifAnalysisDuration,
    long ExtensionlessFilesAnalyzed,
    TimeSpan ExtensionlessAnalysisDuration)
{
    public static MediaAnalysisStatistics Empty { get; } =
        new(0, TimeSpan.Zero, 0, TimeSpan.Zero);

    public static MediaAnalysisStatistics Sum(IEnumerable<MediaAnalysisStatistics> statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        long exifFiles = 0;
        long extensionlessFiles = 0;
        TimeSpan exifDuration = TimeSpan.Zero;
        TimeSpan extensionlessDuration = TimeSpan.Zero;
        foreach (MediaAnalysisStatistics item in statistics)
        {
            ArgumentNullException.ThrowIfNull(item);
            exifFiles += item.ExifFilesAnalyzed;
            exifDuration += item.ExifAnalysisDuration;
            extensionlessFiles += item.ExtensionlessFilesAnalyzed;
            extensionlessDuration += item.ExtensionlessAnalysisDuration;
        }

        return new MediaAnalysisStatistics(exifFiles, exifDuration,
            extensionlessFiles, extensionlessDuration);
    }
}
