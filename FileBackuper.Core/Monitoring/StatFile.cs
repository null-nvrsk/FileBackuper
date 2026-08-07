namespace FileBackuper.Core;

public class StatFile
{
    private string filename = string.Empty;
    private string rootDirectory = Path.GetPathRoot(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Не удалось определить диск приложения.");

    public void SetRootDirectory(string destinationDirectory)
    {
        rootDirectory = Path.GetPathRoot(destinationDirectory)
            ?? throw new ArgumentException("Не удалось определить диск назначения.", nameof(destinationDirectory));
    }

    public string GenerateNewFile(int percent, GroupType group, long fileSize, TimeSpan? imageEndTime,
        TimeSpan fullEndTime, TimeSpan scanTime)
    {
        if (filename != string.Empty)
        {
            try { File.Delete(filename); }
            catch (Exception exception)
            {
                BackupLog.Warning($"Не удалось удалить файл состояния {filename}. " +
                    BackupLog.GetExceptionDescription(exception));
            }
        }

        string percentPart = percent.ToString("00");
        string groupPart = group == GroupType.Image ? "a" : "b";
        double fileSizeMb = (double)fileSize / 1024 / 1024;
        string sizePart = fileSizeMb <= 0.2 ? "0.2" :
            fileSizeMb <= 10 ? fileSizeMb.ToString("0.0").Replace(',', '.') : fileSizeMb.ToString("0");
        string imageTimePart = imageEndTime?.ToString("hhmmss") ?? "000000";
        string fullTimePart = fullEndTime.ToString("hhmmss");
        string scanTimePart = scanTime.ToString("hhmmss");

        filename = Path.Combine(rootDirectory, percentPart + groupPart + sizePart + "-" + imageTimePart + "-" +
            fullTimePart + "-" + scanTimePart + ".tmp");
        try { File.Create(filename).Close(); }
        catch (Exception exception)
        {
            BackupLog.Warning($"Не удалось создать файл состояния {filename}. " +
                BackupLog.GetExceptionDescription(exception));
        }

        Console.WriteLine(filename);
        return filename;
    }

    public void CloseFile()
    {
        if (filename == string.Empty) return;
        try { File.Delete(filename); }
        catch (Exception exception)
        {
            BackupLog.Warning($"Не удалось удалить файл состояния {filename}. " +
                BackupLog.GetExceptionDescription(exception));
        }
    }
}
