using FileBackuperLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileBackuper;
public class StatFile
{
    string filename = "";


    public String GenerateNewFile(int percent, GroupType group, long fileSize, TimeSpan? imgEndTime, TimeSpan fullEndTime, TimeSpan scanTime)
    {
        
        if (filename != "")
        {
            try
            {
                File.Delete(filename);
            }
            catch (Exception e)
            {
                Trace.TraceWarning(e.Message); // warning
            }
        }             

        string root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
        string fnPercent = percent.ToString("00");
        string fnGroup = (group == GroupType.Image) ? "a" : "b";

        double fileSizeMb = (double)fileSize / 1024 / 1024;
        string fnSizeGroup;
        if (fileSizeMb <= 0.2)
            fnSizeGroup = "0.2";
        else if (fileSizeMb > 0.2 && fileSizeMb <= 10)
            fnSizeGroup = fileSizeMb.ToString("0.0").Replace(',', '.');
        else
            fnSizeGroup = fileSizeMb.ToString("0");

        string fnImgTime = imgEndTime?.ToString("hhmmss");
        string fnFullTime = fullEndTime.ToString("hhmmss");
        string fnScanTime = scanTime.ToString("hhmmss");

        filename = root + fnPercent + fnGroup + fnSizeGroup + "-" + fnImgTime + "-" + fnFullTime + "-" + fnScanTime + ".tmp";
        try
        {
            File.Create(filename).Close();
        }
        catch (Exception e)
        {
            Trace.TraceWarning(e.Message); // warning
        }

        Console.WriteLine(filename);
        return filename;
    }

    public void CloseFile()
    {
        if (filename != "")
        {
            try
            {
                File.Delete(filename);
            }
            catch (Exception e)
            {
                Trace.TraceWarning(e.Message); // warning
            }
        }
    }
}
