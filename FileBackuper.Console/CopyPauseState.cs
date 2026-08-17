using FileBackuper.Core;

namespace FileBackuper.Console;

internal sealed class CopyPauseState
{
    private int isPaused;

    public bool IsPaused => Volatile.Read(ref isPaused) != 0;

    public void Pause()
    {
        if (Interlocked.CompareExchange(ref isPaused, 1, 0) != 0)
            return;

        Stat.SetPaused(true);
        BackupLog.Info("Копирование приостановлено горячей клавишей Ctrl+Shift+Alt+P. " +
            "Мониторинг и сканирование дисков продолжаются.");
        BackupLog.Flush();
    }

    public void Resume()
    {
        if (Interlocked.CompareExchange(ref isPaused, 0, 1) != 1)
            return;

        Stat.SetPaused(false);
        BackupLog.Info("Копирование продолжено горячей клавишей Ctrl+Shift+Alt+R.");
        BackupLog.Flush();
    }
}
