using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Classification;

public class MediaAnalysisStatisticsTests
{
    [Fact]
    public void Sum_AddsCountsAndDurationsFromAllDisks()
    {
        MediaAnalysisStatistics result = MediaAnalysisStatistics.Sum(new[]
        {
            new MediaAnalysisStatistics(10, TimeSpan.FromSeconds(2), 3, TimeSpan.FromSeconds(1)),
            new MediaAnalysisStatistics(20, TimeSpan.FromSeconds(4), 7, TimeSpan.FromSeconds(3))
        });

        Assert.Equal(30, result.ExifFilesAnalyzed);
        Assert.Equal(TimeSpan.FromSeconds(6), result.ExifAnalysisDuration);
        Assert.Equal(10, result.ExtensionlessFilesAnalyzed);
        Assert.Equal(TimeSpan.FromSeconds(4), result.ExtensionlessAnalysisDuration);
    }
}
