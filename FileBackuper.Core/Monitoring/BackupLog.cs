using System.Diagnostics;

namespace FileBackuper.Core;

public static class BackupLog
{
    private static readonly object syncRoot = new();
    private static TraceLevel level = TraceLevel.Info;

    public static bool IsVerboseEnabled => level >= TraceLevel.Verbose;

    public static void Configure(TraceLevel value)
    {
        lock (syncRoot)
            level = value;
    }

    public static void Info(string message)
    {
        lock (syncRoot)
        {
            if (level >= TraceLevel.Info)
                WriteInfo(message);
        }
    }

    public static void InfoBlock(params string[] messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        lock (syncRoot)
        {
            if (level < TraceLevel.Info || messages.Length == 0)
                return;

            Trace.WriteLine(string.Empty);
            foreach (string message in messages)
                WriteInfo(message);
        }
    }

    public static void BlankLine()
    {
        lock (syncRoot)
        {
            if (level >= TraceLevel.Info)
                Trace.WriteLine(string.Empty);
        }
    }

    public static void Warning(string message)
    {
        lock (syncRoot)
        {
            if (level >= TraceLevel.Warning)
                Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] Предупреждение: {message}");
        }
    }

    public static void Raw(string message)
    {
        lock (syncRoot)
        {
            if (level >= TraceLevel.Info)
                Trace.WriteLine(message);
        }
    }

    public static void Verbose(string message)
    {
        lock (syncRoot)
        {
            if (IsVerboseEnabled)
                Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] Подробно: {message}");
        }
    }

    public static string GetExceptionDescription(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Доступ запрещён.",
        FileNotFoundException => "Файл не найден.",
        DirectoryNotFoundException => "Каталог не найден.",
        IOException => $"Ошибка ввода-вывода (код 0x{exception.HResult:X8}).",
        _ => $"Ошибка типа {exception.GetType().Name}."
    };

    public static void Flush()
    {
        lock (syncRoot)
            Trace.Flush();
    }

    private static void WriteInfo(string message) =>
        Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] {message}");
}
