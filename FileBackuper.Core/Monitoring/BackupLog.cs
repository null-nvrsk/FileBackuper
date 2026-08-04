using System.Diagnostics;

namespace FileBackuper.Core;

public static class BackupLog
{
    public static void Info(string message) =>
        Trace.TraceInformation($"[{Stat.GetCurrentScanTimeAsString()}] {message}");

    public static void Warning(string message) =>
        Trace.TraceWarning($"[{Stat.GetCurrentScanTimeAsString()}] {message}");

    public static void Flush() => Trace.Flush();
}
