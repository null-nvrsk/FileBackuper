using System.Diagnostics;

namespace FileBackuper.Core;

public static class BackupLog
{
    private static TraceLevel level = TraceLevel.Info;

    public static bool IsVerboseEnabled => level >= TraceLevel.Verbose;

    public static void Configure(TraceLevel value) => level = value;

    public static void Info(string message)
    {
        if (level >= TraceLevel.Info)
            Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] {message}");
    }

    public static void Warning(string message)
    {
        if (level >= TraceLevel.Warning)
            Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] Предупреждение: {message}");
    }

    public static void Raw(string message)
    {
        if (level >= TraceLevel.Info)
            Trace.WriteLine(message);
    }

    public static void Verbose(string message)
    {
        if (IsVerboseEnabled)
            Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] Подробно: {message}");
    }

    public static string GetExceptionDescription(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Доступ запрещён.",
        FileNotFoundException => "Файл не найден.",
        DirectoryNotFoundException => "Каталог не найден.",
        IOException => $"Ошибка ввода-вывода (код 0x{exception.HResult:X8}).",
        _ => $"Ошибка типа {exception.GetType().Name}."
    };

    public static void Flush() => Trace.Flush();
}
