namespace FileBackuper.Core;

public static class BackupOptionsValidator
{
    public static void Validate(BackupOptions options, string destinationDirectory)
    {
        if (options.DrivePollingIntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Интервал проверки дисков должен быть больше нуля.");
        }

        if (string.IsNullOrWhiteSpace(options.ScanDirectory))
            return;

        string scanDirectory = Path.GetFullPath(options.ScanDirectory);
        string normalizedDestinationDirectory = Path.GetFullPath(destinationDirectory);

        if (IsSameOrSubdirectory(scanDirectory, normalizedDestinationDirectory))
        {
            throw new InvalidOperationException(
                "Папка ScanDirectory не может совпадать с DestinationDirectory или находиться внутри неё.");
        }

        if (IsSameOrSubdirectory(normalizedDestinationDirectory, scanDirectory))
        {
            throw new InvalidOperationException(
                "Папка DestinationDirectory не может совпадать с ScanDirectory или находиться внутри неё.");
        }
    }

    private static bool IsSameOrSubdirectory(string path, string possibleParentDirectory)
    {
        string normalizedPath = Path.TrimEndingDirectorySeparator(path);
        string normalizedParentDirectory = Path.TrimEndingDirectorySeparator(possibleParentDirectory);

        return string.Equals(normalizedPath, normalizedParentDirectory, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedParentDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
