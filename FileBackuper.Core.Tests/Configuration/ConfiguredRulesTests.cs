using FileBackuper.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileBackuper.Core.Tests.Configuration;

public class ConfiguredRulesTests
{
    [Theory]
    [InlineData("CameraFileNamePatterns.txt")]
    [InlineData("VideoBlacklistPatterns.txt")]
    public void PatternFile_AllActiveLinesAreValidRegex(string fileName)
    {
        string path = GetRulePath(fileName);
        int activeLineCount = File.ReadLines(path)
            .Select(line => line.Trim())
            .Count(line => line.Length > 0 && !line.StartsWith('#'));

        RegexPatternSet patterns = RegexPatternSet.Load(path);

        Assert.Equal(activeLineCount, patterns.Count);
    }

    [Theory]
    [InlineData("IMG_1234.jpg")]
    [InlineData("VID-20240101-WA0001.mp4")]
    [InlineData("20240101_123456.mp4")]
    [InlineData("photo_2023-07-02_22-04-49 (2).jpg")]
    [InlineData("photo_2023-07-02_22-04-49.jpg")]
    [InlineData("WhatsApp Image 2018-11-17 at 17.43.45(1).jpeg")]
    [InlineData("photo_11_2024-07-01_21-21-14.jpg")]
    [InlineData("__IMG-20190222-WA0002.jpg")]
    [InlineData("__IMG_20231019_140907_252.jpg")]
    public void CameraPatternFile_MatchesKnownCameraAndPhoneNames(string fileName)
    {
        RegexPatternSet patterns = RegexPatternSet.Load(GetRulePath("CameraFileNamePatterns.txt"));

        Assert.True(patterns.IsMatch(fileName));
    }

    [Theory]
    [InlineData("Movie.REMUX.1080p.mp4")]
    [InlineData("Series.S02E04.WEB-DL.mp4")]
    [InlineData("Film.BluRay.x265.mkv")]
    [InlineData("Сериал СЕЗОН 2.mp4")]
    [InlineData("Новый ВЕБИНАР.mp4")]
    [InlineData("Developer WEBINAR.mp4")]
    [InlineData("КУРС CSharp.mp4")]
    [InlineData("УРОК 5.mp4")]
    [InlineData("Movie.WEBRIP.mkv")]
    [InlineData("Movie.BDRIP.avi")]
    public void VideoBlacklistPatternFile_MatchesReleaseNames(string fileName)
    {
        RegexPatternSet patterns = RegexPatternSet.Load(GetRulePath("VideoBlacklistPatterns.txt"));

        Assert.True(patterns.IsMatch(fileName));
    }

    [Fact]
    public void AppSettings_ContainsValidBackupConfiguration()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonSerializerOptions serializerOptions = new();
        serializerOptions.Converters.Add(new JsonStringEnumConverter());
        BackupOptions? options = JsonSerializer.Deserialize<BackupOptions>(
            document.RootElement.GetProperty("Backup").GetRawText(), serializerOptions);

        Assert.NotNull(options);
        BackupOptionsValidator.Validate(options, Path.GetTempPath());
        Assert.Contains("AppData", options.SkipDirectoryNames);
        Assert.NotEmpty(options.FileSizeGroups);
    }

    [Fact]
    public void LoadBackupOptions_ReplacesDefaultCollectionsWithConfiguredCollections()
    {
        BackupOptions options = LoggingConfiguration.LoadBackupOptions();

        Assert.Equal(52, options.FileSizeGroups.Count);
        Assert.Equal(options.FileSizeGroups.Count,
            options.FileSizeGroups.Select(group => group.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(5, options.SkipDirectoryNames.Count);
        BackupOptionsValidator.Validate(options, Path.GetTempPath());
    }

    private static string GetRulePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Rules", fileName);
}
