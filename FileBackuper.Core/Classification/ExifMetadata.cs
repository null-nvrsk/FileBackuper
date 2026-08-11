namespace FileBackuper.Core;

public sealed record ExifMetadata(
    string? CameraMake,
    string? CameraModel,
    DateTime? DateTimeOriginal)
{
    private static readonly HashSet<string> NonCameraValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "unknown", "n/a", "none"
    };

    public static ExifMetadata Empty { get; } = new(null, null, null);

    public bool HasCameraInfo => IsMeaningful(CameraMake) || IsMeaningful(CameraModel);

    private static bool IsMeaningful(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return !NonCameraValues.Contains(value.Trim());
    }
}
