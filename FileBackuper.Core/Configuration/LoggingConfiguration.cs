
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace FileBackuper.Core;

public static class LoggingConfiguration
{
    public static BackupOptions LoadBackupOptions()
    {
        return BuildConfiguration().GetSection("Backup").Get<BackupOptions>() ?? new BackupOptions();
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
