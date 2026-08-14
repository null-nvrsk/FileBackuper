namespace FileBackuper.Core.Tests;

public class StatTests
{
    [Fact]
    public void RegisteredFilesAndCompletedFiles_UpdateCommonProgress()
    {
        using TestWorkspace workspace = new();
        FileInfo firstFile = workspace.CreateFile("first.jpg", 10);
        FileInfo secondFile = workspace.CreateFile("second.mp4", 20);
        Stat.Reset();

        Stat.AddFilesToTotalStat(new[] { firstFile, secondFile });
        Stat.AddFileToCompletedStat(firstFile);

        Assert.Equal(33, Stat.GetPercentageOfCompletion());
        StatSnapshot partialSnapshot = Stat.GetSnapshot();
        Assert.Equal(2, partialSnapshot.TotalFileCount);
        Assert.Equal(30, partialSnapshot.TotalSize);
        Assert.Equal(1, partialSnapshot.CompletedFileCount);
        Assert.Equal(10, partialSnapshot.CompletedSize);
        Assert.Equal(33, partialSnapshot.Percentage);

        Stat.RemoveFileFromTotalStat(secondFile, secondFile.Length);
        StatSnapshot reducedSnapshot = Stat.GetSnapshot();
        Assert.Equal(1, reducedSnapshot.TotalFileCount);
        Assert.Equal(10, reducedSnapshot.TotalSize);
        Assert.Equal(100, reducedSnapshot.Percentage);

        Stat.Reset();
    }

    [Theory]
    [InlineData(0, "[--------------------]")]
    [InlineData(29, "[+++++---------------]")]
    [InlineData(100, "[++++++++++++++++++++]")]
    public void ProgressBar_UsesFivePercentBlocks(int percentage, string expected)
    {
        Assert.Equal(expected, Stat.BuildProgressBar(percentage));
    }

    [Fact]
    public void ProgressReport_ContainsGroupSummaryTotalsAndTimeBlock()
    {
        using TestWorkspace workspace = new();
        FileInfo file = workspace.CreateFile("photo.jpg", 20_000);
        BackupFileCandidate candidate = new(file, new MediaFileAnalysis
        {
            Kind = MediaKind.Image,
            DetectionSource = MediaDetectionSource.Extension
        });
        Stat.ConfigureSizeGroups(new[]
        {
            new FileSizeGroupOptions { Name = "TestGroup", MinBytes = 10_000, MaxBytes = 30_000 }
        });
        Stat.Reset();
        Stat.Start();
        Stat.StartCopying();
        Stat.AddFilesToTotalStat(new[] { candidate });
        Stat.AddFileToCompletedStat(candidate, TimeSpan.FromSeconds(1));

        string report = Stat.BuildProgressReport();
        string[] reportLines = report.Split(Environment.NewLine);

        Assert.Equal(new string('-', 130), reportLines[0]);
        Assert.Equal(new string('-', 130), reportLines[^1]);
        Assert.Contains("[++++++++++++++++++++] 100% [TestGroup]", report);
        Assert.Matches(@"\[[^\]]+ / [^\]]+\] \[1 / 1\]", report);
        Assert.DoesNotContain("Группа размера", report);
        Assert.Contains("100% [время копирования", report);
        Assert.Contains("[Скопировано", report);
        Assert.Contains("[Файлов: 1 из 1]", report);
        Assert.Contains("[Скорость: —]", report);
        Assert.Contains("Блок времени:", report);
        Assert.Matches(@"примерное время копирования до 100 МБ\s+: выполнено", report);
        string[] timeRows = report.Split(Environment.NewLine)
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .ToArray();
        Assert.Single(timeRows.Select(line => line.IndexOf(" : ", StringComparison.Ordinal)).Distinct());
        string logBlock = Stat.BuildLogProgressBlock(DateTime.Now);
        string[] logLines = logBlock.Split(Environment.NewLine);
        Assert.Equal(new string('-', 130), logLines[0]);
        Assert.Equal(new string('-', 130), logLines[^1]);
        Assert.DoesNotContain("Блок времени", logBlock);
        Assert.DoesNotContain("примерное время", logBlock);
        Stat.Reset();
    }

    [Fact]
    public void ProgressReport_EstimatesUnstartedGroupFromThreePreviousGroupSpeeds()
    {
        using TestWorkspace workspace = new();
        BackupFileCandidate[] candidates =
        {
            CreateCandidate(workspace.CreateFile("first.jpg", 10_000)),
            CreateCandidate(workspace.CreateFile("second.jpg", 20_000)),
            CreateCandidate(workspace.CreateFile("third.jpg", 30_000)),
            CreateCandidate(workspace.CreateFile("planned.jpg", 40_000))
        };
        Stat.ConfigureSizeGroups(new[]
        {
            new FileSizeGroupOptions { Name = "First", MinBytes = 10_000, MaxBytes = 10_000 },
            new FileSizeGroupOptions { Name = "Second", MinBytes = 20_000, MaxBytes = 20_000 },
            new FileSizeGroupOptions { Name = "Third", MinBytes = 30_000, MaxBytes = 30_000 },
            new FileSizeGroupOptions { Name = "Planned", MinBytes = 40_000, MaxBytes = 40_000 }
        });
        Stat.Reset();
        Stat.StartCopying();
        Stat.AddFilesToTotalStat(candidates);
        Stat.AddFileToCompletedStat(candidates[0], TimeSpan.FromSeconds(1));
        Stat.AddFileToCompletedStat(candidates[1], TimeSpan.FromSeconds(1));
        Stat.AddFileToCompletedStat(candidates[2], TimeSpan.FromSeconds(1));

        string warmupReport = Stat.BuildProgressReport();
        string warmupPlannedLine = warmupReport.Split(Environment.NewLine)
            .Single(line => line.Contains("[Planned]", StringComparison.Ordinal));
        Assert.Matches(@"время\s+—, сред\. скорость\s+—$", warmupPlannedLine);

        string report = Stat.BuildProgressReport(DateTime.Now.AddSeconds(61));
        string plannedLine = report.Split(Environment.NewLine)
            .Single(line => line.Contains("[Planned]", StringComparison.Ordinal));
        string totalEtaLine = report.Split(Environment.NewLine)
            .Single(line => line.Contains("примерное время всего", StringComparison.Ordinal));

        Assert.Contains("время 00:00:02", plannedLine);
        Assert.DoesNotMatch(@"сред\. скорость\s+0[,.]00 МБ/с", plannedLine);
        Assert.Matches(@": \d{2}:\d{2}:\d{2} \(через", totalEtaLine);
        Assert.Matches(@"\d+% \[время копирования \d{2}:\d{2}:\d{2}\].*" +
            @"\[Скорость: .+ МБ/с / .+ ГБ/мин\]", report);
        Stat.Reset();
    }

    private static BackupFileCandidate CreateCandidate(FileInfo file) => new(file, new MediaFileAnalysis
    {
        Kind = MediaKind.Image,
        DetectionSource = MediaDetectionSource.Extension
    });
}
