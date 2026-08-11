using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Classification;

public class RegexPatternSetTests
{
    [Fact]
    public void Load_IgnoresCommentsEmptyLinesAndInvalidRegex()
    {
        using TestWorkspace workspace = new();
        string filePath = Path.Combine(workspace.RootDirectory.FullName, "patterns.txt");
        File.WriteAllLines(filePath, new[]
        {
            "# comment",
            "",
            "^IMG_\\d+\\.JPG$",
            "[invalid"
        });

        RegexPatternSet patterns = RegexPatternSet.Load(filePath);

        Assert.Equal(1, patterns.Count);
        Assert.True(patterns.IsMatch("img_1234.jpg"));
        Assert.False(patterns.IsMatch("video.mp4"));
    }

    [Fact]
    public void FindMatchingPattern_ReturnsRegexUsedForTheDecision()
    {
        using TestWorkspace workspace = new();
        string filePath = Path.Combine(workspace.RootDirectory.FullName, "patterns.txt");
        File.WriteAllLines(filePath, new[] { "remux", "web[-_. ]?dl" });
        RegexPatternSet patterns = RegexPatternSet.Load(filePath);

        string? matchingPattern = patterns.FindMatchingPattern("Movie.WEB-DL.1080p.mp4");

        Assert.Equal("web[-_. ]?dl", matchingPattern);
    }

    [Fact]
    public void Load_ReturnsEmptySetWhenFileDoesNotExist()
    {
        using TestWorkspace workspace = new();

        RegexPatternSet patterns = RegexPatternSet.Load(
            Path.Combine(workspace.RootDirectory.FullName, "missing.txt"));

        Assert.Equal(0, patterns.Count);
        Assert.False(patterns.IsMatch("IMG_0001.JPG"));
    }
}
