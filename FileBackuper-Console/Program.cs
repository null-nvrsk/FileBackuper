using System.Collections.Generic;
using static System.Console;

using static FileBackuper.FileBackuperLib;

namespace FileBackuper_Console
{
    internal class Program
    {
        static FileBackuper.Stat stat = new();

        static void Main(string[] args)
        {
            // создаем новую папку
            string destionationDir = CreateDestinationDir();

            // сканируем все диски
            stat.Start();
            var drives = GetDrivesToScan();
            List<FileInfo> files = new();
            foreach (var drive in drives)
            {
                WriteLine(drive.Name);

                files.AddRange(RecursiveDirectoryTree(drive.RootDirectory));

            }
            TimeSpan scanTime = stat.Stop();

            WriteLine($"Время сканирования: {scanTime.ToString()}");
            WriteLine($"Найдено файлов: {files.Count}");

            long totalSize = 0;
            foreach (var fi in files)
            {
                totalSize += fi.Length;
            }
            WriteLine($"Общий объем: {totalSize:N0} байт");

            files = SmartSort(files);


            // копируем все файлы
            stat.Start();
            CopyFiles(files, destionationDir);
            scanTime = stat.Stop();

            WriteLine($"Время копирования: {scanTime.ToString()}");
            WriteLine($"Скорость: {totalSize / scanTime.Seconds / 1024 / 1024} Mb/s");
        }
    }
}