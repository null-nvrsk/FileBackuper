namespace FileBackuper.Core;

public sealed class FileTypeSelection
{
    private readonly IReadOnlyDictionary<string, MediaKind> enabledExtensions;

    public FileTypeSelection(IEnumerable<FileCategoryOptions> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        Dictionary<string, MediaKind> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (FileCategoryOptions category in categories)
        {
            IEnumerable<string> selectedExtensions = category.Enabled
                ? category.Extensions
                : category.EnabledExtensions;

            foreach (string extension in selectedExtensions)
                result[NormalizeExtension(extension)] = category.Kind;
        }

        enabledExtensions = result;
    }

    public static FileTypeSelection Default { get; } = new(FileCategoryOptions.CreateDefaults());

    public MediaKind GetKind(string extension) =>
        enabledExtensions.TryGetValue(NormalizeExtension(extension), out MediaKind kind)
            ? kind
            : MediaKind.Unknown;

    public bool IsEnabled(string extension) => GetKind(extension) != MediaKind.Unknown;

    internal static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string normalized = extension.Trim();
        if (!normalized.StartsWith(".", StringComparison.Ordinal))
            normalized = "." + normalized;
        return normalized.ToLowerInvariant();
    }
}
