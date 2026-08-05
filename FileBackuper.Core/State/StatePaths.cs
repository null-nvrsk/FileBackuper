namespace FileBackuper.Core;

public static class StatePaths
{
    public static string ResolveStateDirectory(string? configuredStateDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredStateDirectory))
            return Path.GetFullPath(configuredStateDirectory, AppContext.BaseDirectory);

        string commonApplicationDataDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData);

        return Path.Combine(commonApplicationDataDirectory, "FileBackuper", "state");
    }

    public static string EnsureStateDirectory(string? configuredStateDirectory)
    {
        string stateDirectory = ResolveStateDirectory(configuredStateDirectory);
        Directory.CreateDirectory(stateDirectory);
        return stateDirectory;
    }
}
