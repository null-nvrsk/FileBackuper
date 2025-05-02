using System.Diagnostics;

using static FileBackuper.FileBackuperLib;

namespace FileBackuper;

internal class Program
{
    static void Main(string[] args)
    {
        // запуск только одного экземпляра приложения
        if (System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName).Length > 1)
            return;

        // создаем новую папку
        string destionationDir = CreateDestinationDir();

        // настраиваем логирование в эту папку
        TraceLoader.LoadSettings(destionationDir);

        // сканируем все диски
        Trace.TraceInformation("Начало сканирования");
        Stat.Start();
        var drives = GetDrivesToScan();
        List<FileInfo> files = new();
        Trace.TraceInformation("Диски:"); 
        foreach (var drive in drives)
        {
            Trace.TraceInformation($"   {drive.Name}") ;

            files.AddRange(RecursiveDirectoryTree(drive.RootDirectory));
        }
        Trace.WriteLine("");
        TimeSpan scanTime = Stat.Stop();

        Trace.TraceInformation($"[{Stat.GetCurrentScanTimeAsString()}] Время сканирования: {scanTime:hh\\:mm\\:ss\\.ff}");
        Trace.TraceInformation($"[{Stat.GetCurrentScanTimeAsString()}] Найдено файлов: {files.Count}");
        long totalSize = 0;
        foreach (var fi in files)
        {
            totalSize += fi.Length;
        }
        Trace.TraceInformation($"[{Stat.GetCurrentScanTimeAsString()}] Общий размер файлов: {totalSize:N0} байтов");
        Trace.Flush();

        Trace.TraceInformation("Начало сортировки"); 
        Stat.Start();
 
        files = SmartSort(files);

        scanTime = Stat.Stop();
        Trace.TraceInformation($"[{Stat.GetCurrentScanTimeAsString()}] Конец сортировки. Время сортировка: {scanTime:hh\\:mm\\:ss\\.ff}");
        Trace.Flush();

        // копируем все файлы
        Stat.Start();

        CopyFiles(files, destionationDir);
        scanTime = Stat.Stop();

        Trace.TraceInformation($"[{Stat.GetCurrentScanTimeAsString()}] Время копирования: {scanTime.ToString()}"); 
        double copySpeed = totalSize / scanTime.TotalSeconds;
        Trace.TraceInformation($"Скорость: {(copySpeed / 1024 / 1024):F2} Mb/s "); 
        Trace.TraceInformation($"          {(copySpeed / 1024 / 1024 / 1024 * 60):F2} Gb/min");
        Trace.Flush();
    }
}