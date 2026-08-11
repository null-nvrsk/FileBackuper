using System.Text.RegularExpressions;

namespace FileBackuper.Core;

public sealed class RegexPatternSet
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);
    private readonly IReadOnlyList<PatternEntry> entries;

    private RegexPatternSet(IReadOnlyList<PatternEntry> entries)
    {
        this.entries = entries;
    }

    public int Count => entries.Count;

    public static RegexPatternSet Empty { get; } = new(Array.Empty<PatternEntry>());

    public IReadOnlyList<string> Patterns => entries.Select(entry => entry.Text).ToList();

    public static RegexPatternSet Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("The pattern file path cannot be empty.", nameof(filePath));

        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            BackupLog.Warning($"Pattern file was not found: {fullPath}");
            return new RegexPatternSet(Array.Empty<PatternEntry>());
        }

        List<PatternEntry> entries = new();
        try
        {
            int lineNumber = 0;
            foreach (string line in File.ReadLines(fullPath))
            {
                lineNumber++;
                string pattern = line.Trim();
                if (pattern.Length == 0 || pattern.StartsWith('#'))
                    continue;

                try
                {
                    Regex regex = new(pattern,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
                        MatchTimeout);
                    entries.Add(new PatternEntry(pattern, regex));
                }
                catch (ArgumentException exception)
                {
                    BackupLog.Warning(
                        $"Invalid regex in {fullPath} at line {lineNumber}: {pattern}. {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            BackupLog.Warning($"Could not read pattern file {fullPath}. " +
                BackupLog.GetExceptionDescription(exception));
            return new RegexPatternSet(Array.Empty<PatternEntry>());
        }

        return new RegexPatternSet(entries);
    }

    public bool IsMatch(string value) => FindMatchingPattern(value) is not null;

    public string? FindMatchingPattern(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach (PatternEntry entry in entries)
        {
            try
            {
                if (entry.Regex.IsMatch(value))
                    return entry.Text;
            }
            catch (RegexMatchTimeoutException)
            {
                BackupLog.Warning($"Regex timed out and was skipped: {entry.Text}");
            }
        }

        return null;
    }

    private sealed record PatternEntry(string Text, Regex Regex);
}
