﻿using System.Diagnostics;

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
        Trace.TraceInformation("Начало сканирования"); // info
        Stat.Start();
        var drives = GetDrivesToScan();
        List<FileInfo> files = new();
        Trace.TraceInformation("Диски:"); // info
        foreach (var drive in drives)
        {
            Trace.TraceInformation($"   {drive.Name}") ; // info

            files.AddRange(RecursiveDirectoryTree(drive.RootDirectory));
        }
        Trace.WriteLine("");// info
        TimeSpan scanTime = Stat.Stop();

        Trace.TraceInformation($"[{Stat.GetCurrentScanTime()}] Время сканирования: {scanTime.ToString()}"); // info
        Trace.TraceInformation($"[{Stat.GetCurrentScanTime()}] Найдено файлов: {files.Count}"); // info
        long totalSize = 0;
        foreach (var fi in files)
        {
            totalSize += fi.Length;
        }
        Trace.TraceInformation($"[{Stat.GetCurrentScanTime()}] Общий размер файлов: {totalSize:N0} байтов"); // info
        Trace.Flush();

        Trace.TraceInformation("Начало сортировки"); // info
        Stat.Start();
 
        files = SmartSort(files);

        scanTime = Stat.Stop();
        Trace.TraceInformation($"[{Stat.GetCurrentScanTime()}] Конец сортировки. Время сортировка: {scanTime.ToString()}"); // info
        Trace.Flush();

        // копируем все файлы
        Stat.Start();

        CopyFiles(files, destionationDir);
        scanTime = Stat.Stop();

        Trace.TraceInformation($"[{Stat.GetCurrentScanTime()}] Время копирования: {scanTime.ToString()}"); // info
        double copySpeed = totalSize / scanTime.TotalSeconds;
        Trace.TraceInformation($"Скорость: {(copySpeed / 1024 / 1024)} Mb/s "); // info
        Trace.TraceInformation($"          {(copySpeed / 1024 / 1024 / 1024 * 60)} Gb/min"); // info
        Trace.Flush();
    }
}