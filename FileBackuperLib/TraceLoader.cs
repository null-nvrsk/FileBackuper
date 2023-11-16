
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace FileBackuper;

public static class TraceLoader // TODO: rename to Tracer
{
    public static void LoadSettings(string logFolder)
    {
        Trace.Listeners.Add(
            new TextWriterTraceListener(
            File.CreateText(Path.Combine(logFolder, "log.txt"))
            )
        );

        //Trace.AutoFlush = true; // TODO: вкючать AutoFlush (отключать кеширвание) на время отладки
        Trace.AutoFlush = false; // TODO: вкючать AutoFlush (отключать кеширвание) на время отладки

        ConfigurationBuilder builder = new();

        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        IConfigurationRoot configuration = builder.Build();
        TraceSwitch ts = new(displayName: "FileBackuperSwitch",
                             description: "This switch is set via a JSON cinfig.");

        configuration.GetSection("FileBackuperSwitch").Bind(ts);
    }
}
