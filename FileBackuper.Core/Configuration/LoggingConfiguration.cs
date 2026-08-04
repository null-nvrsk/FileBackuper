
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace FileBackuper.Core;

public static class LoggingConfiguration
{
    public static void Configure(string logFolder)
    {
        Trace.Listeners.Add(
            new TextWriterTraceListener(
            File.CreateText(Path.Combine(logFolder, "log-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"))
            )
        );

#if DEBUG
        Trace.AutoFlush = true;
#endif

        ConfigurationBuilder builder = new();

        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        IConfigurationRoot configuration = builder.Build();
        TraceSwitch ts = new(displayName: "FileBackuperSwitch",
                             description: "This switch is set via a JSON cinfig.");

        configuration.GetSection("FileBackuperSwitch").Bind(ts);
    }
}
