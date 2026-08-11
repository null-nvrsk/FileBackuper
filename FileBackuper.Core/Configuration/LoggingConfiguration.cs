
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace FileBackuper.Core;

public static class LoggingConfiguration
{
    public static BackupOptions LoadBackupOptions()
    {
        IConfigurationSection section = BuildConfiguration().GetSection("Backup");
        BackupOptions options = new();
        if (!section.Exists())
            return options;

        // ConfigurationBinder adds values to initialized collections instead of replacing them.
        // Clear only collections explicitly present in JSON so omitted settings retain defaults.
        if (section.GetSection(nameof(BackupOptions.FileSizeGroups)).Exists())
            options.FileSizeGroups.Clear();
        if (section.GetSection(nameof(BackupOptions.SkipDirectoryNames)).Exists())
            options.SkipDirectoryNames.Clear();

        section.Bind(options);
        return options;
    }

    public static void Configure(string logFolder)
    {
        Trace.Listeners.Add(
            new TextWriterTraceListener(
            File.CreateText(Path.Combine(logFolder, "log-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"))
            )
        );

//#if DEBUG
//        Trace.AutoFlush = true;
//#endif

        IConfigurationRoot configuration = BuildConfiguration();
        TraceSwitch traceSwitch = new(displayName: "FileBackuperSwitch",
                                      description: "Уровень журналирования FileBackuper.");

        configuration.GetSection("FileBackuperSwitch").Bind(traceSwitch);
        BackupLog.Configure(traceSwitch.Level);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
    }
}
