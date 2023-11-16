using System.Diagnostics;

using static FileBackuper.FileBackuperLib;

namespace FileBackuper;

internal class Program
{
    static Stat stat = new();

    static void Main(string[] args)
    {
        // создаем новую папку
        string destionationDir = CreateDestinationDir();

        // настраиваем логирование в эту папку
        TraceLoader.LoadSettings(destionationDir);

        // сканируем все диски
        Trace.TraceInformation("Начало сканирования"); // info
        stat.Start();
        var drives = GetDrivesToScan();
        List<FileInfo> files = new();
        Trace.TraceInformation("Диски:"); // info
        foreach (var drive in drives)
        {
            Trace.TraceInformation($"   {drive.Name}") ; // info

            files.AddRange(RecursiveDirectoryTree(drive.RootDirectory));
        }
        Trace.WriteLine("");// info
        TimeSpan scanTime = stat.Stop();

        Trace.TraceInformation($"Время сканирования: {scanTime.ToString()}"); // info
        Trace.TraceInformation($"Найдено файлов: {files.Count}"); // info
        long totalSize = 0;
        foreach (var fi in files)
        {
            totalSize += fi.Length;
        }
        Trace.TraceInformation($"Общий размер файлов: {totalSize:N0} байтов"); // info
        Trace.Flush();

        Trace.TraceInformation("Начало сортировки"); // info
        stat.Start();
 
        files = SmartSort(files);

        scanTime = stat.Stop();
        Trace.TraceInformation($"Конец сортировки. Время сортировка: {scanTime.ToString()}"); // info
        Trace.Flush();

        // копируем все файлы
        stat.Start();

        CopyFiles(files, destionationDir);
        scanTime = stat.Stop();

        Trace.TraceInformation($"Время копирования: {scanTime.ToString()}"); // info
        double copySpeed = totalSize / scanTime.TotalSeconds;
        Trace.TraceInformation($"Скорость: {(copySpeed / 1024 / 1024)} Mb/s "); // info
        Trace.TraceInformation($"          {(copySpeed / 1024 / 1024 / 1024 * 60)} Gb/min"); // info
        Trace.Flush();
    }
}