using System.Diagnostics;

namespace FileBackuper.Core;

public class StatFile
{
    private string filename = string.Empty;

    public string GenerateNewFile(int percent, GroupType group, long fileSize, TimeSpan? imageEndTime,
        TimeSpan fullEndTime, TimeSpan scanTime)
    {
        if (filename != string.Empty)
        {
            try { File.Delete(filename); }
            catch (Exception exception) { Trace.TraceWarning(exception.Message); }
        }

        string root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
        string percentPart = percent.ToString("00");
        string groupPart = group == GroupType.Image ? "a" : "b";
        double fileSizeMb = (double)fileSize / 1024 / 1024;
        string sizePart = fileSizeMb <= 0.2 ? "0.2" :
            fileSizeMb <= 10 ? fileSizeMb.ToString("0.0").Replace(',', '.') : fileSizeMb.ToString("0");
        string imageTimePart = imageEndTime?.ToString("hhmmss") ?? "000000";
        string fullTimePart = fullEndTime.ToString("hhmmss");
        string scanTimePart = scanTime.ToString("hhmmss");

        filename = root + percentPart + groupPart + sizePart + "-" + imageTimePart + "-" +
            fullTimePart + "-" + scanTimePart + ".tmp";
        try { File.Create(filename).Close(); }
        catch (Exception exception) { Trace.TraceWarning(exception.Message); }

        Console.WriteLine(filename);
        return filename;
    }

    public void CloseFile()
    {
        if (filename == string.Empty) return;
        try { File.Delete(filename); }
        catch (Exception exception) { Trace.TraceWarning(exception.Message); }
    }
}
