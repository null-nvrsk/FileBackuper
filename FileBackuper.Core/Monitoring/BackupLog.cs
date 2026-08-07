using System.Diagnostics;

namespace FileBackuper.Core;

public static class BackupLog
{
    public static void Info(string message) =>
        Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] {message}");

    public static void Warning(string message) =>
        Trace.WriteLine($"[{Stat.GetCurrentScanTimeAsString()}] Предупреждение: {message}");

    public static void Raw(string message) => Trace.WriteLine(message);

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
