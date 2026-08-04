namespace FileBackuper.Core;

public static class BackupPaths
{
    public static string ResolveDestinationDirectory(string? configuredDestinationDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDestinationDirectory))
            return Path.GetFullPath(configuredDestinationDirectory);

        string applicationDrive = Path.GetPathRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Не удалось определить диск приложения.");

        return Path.Combine(applicationDrive, "Temp", Fingerprint.GetMd5Hash());
    }
}
