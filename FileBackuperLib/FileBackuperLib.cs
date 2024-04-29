using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace FileBackuper;

public static class FileBackuperLib
{
    public static string CreateDestinationDir()
    {
        string compName = Environment.MachineName;
        Trace.TraceInformation($"Machine name: {compName}"); // info

        string newDir = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory()) +
            "Temp\\" +
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +
            "_" + 
            Environment.MachineName;

        Trace.TraceInformation($"Destination directory: {newDir}"); // info
        if (Directory.CreateDirectory(newDir) != null)
            Directory.SetCurrentDirectory(newDir);

        return newDir;
    }

    //----------------------------------------------------------------------
    /// <summary>
    /// Получить список доступных локальных дисков
    /// </summary>
    /// <returns>Список дисков</returns>
    public static List<DriveInfo> GetDrivesToScan()
    {
        List<DriveInfo> result = new();
        string[] drives = Environment.GetLogicalDrives();

        foreach (string drive in drives)
        {
            DriveInfo di = new DriveInfo(drive);
            if (!di.IsReady)
            {
                Trace.TraceWarning("The drive {0} could not be read", di.Name); // warning
                continue;
            }

            // skip drive where app started
            if (Directory.GetDirectoryRoot(Directory.GetCurrentDirectory()) == di.Name)
                continue;

            // временно отключаю диск D: (в режиме отладки)
#if DEBUG
            if ("D:\\" == di.Name)
                continue;
#endif

            result.Add(di);
        }

        return result;
    }

    //----------------------------------------------------------------------
    public static List<string> GetFileExtendedListToCopy(string drive)
    {
        List<string> result = new List<string>();

        return result;
    }

    //----------------------------------------------------------------------
    // Рекурсивная функция поиска файлов по указанным настройками поиска
    public static List<FileInfo>? RecursiveDirectoryTree(DirectoryInfo root)
    {
        List<FileInfo> resultList = new List<FileInfo>();

        FileInfo[]? files = null;
        DirectoryInfo[]? subDirs = null;

        try
        {
            files = root.GetFiles();
        }
        catch (UnauthorizedAccessException e)
        {
            //log.Add(e.Message);
            Trace.TraceWarning(e.Message); // warning
        }
        catch (DirectoryNotFoundException e)
        {
            Trace.TraceWarning(e.Message); // warning
        }

        if (files == null)
            return null;

        foreach (FileInfo fi in files)
        {
            if (!IsFileShouldBeSkipped(fi))
            { 
                resultList.Add(fi);
                Trace.WriteLine($"{fi.FullName}"); // verbose
            }
        }

        // Now find all the subdirectories under this directory.
        subDirs = root.GetDirectories();

        foreach (DirectoryInfo dirInfo in subDirs)
        {
            // проверка папок исключений
            if (IsDirectoryShouldBeSkipped(dirInfo.Name))
                continue;

            // рекурсивный поиск файлов по папкам
            var subDirfiles = RecursiveDirectoryTree(dirInfo);
            if (subDirfiles != null) 
                resultList.AddRange(subDirfiles);
        }

        return resultList;
    }

    //----------------------------------------------------------------------
    // Умная сортировка 
    public static List<FileInfo> SmartSort(List<FileInfo> files)
    {
        // Большие группы по расширению и размеру (Б - без расширения)


        // Средние группы по шаблону имени файла
        // 1 - фотки и видео с телефона/камеры
        // 0 - остальные
        //
        // Малые подгруппы по имени папки:
        // 0 - фото*, foto*, icloud*?, apple*?, Telegram*, Pictures
        // 1 - Desktop, Documents
        // 2 - корзина, *Recycle.Bin*, Temp
        // 3 - останьные
        // 9 - Downloads

        List<FileInfo> sortedList = new();

        var fileOrderMap = new Dictionary<FileInfo, int>();

        foreach (var fi in files)
        {
            int filePriority = 0; // по умолчанию самый низкий приоритет

            // Большие группы по расширению и размеру
            // (максимальный приоритет)

            if (IsFileImage(fi))
            {
                filePriority = 50_000; // приоритет выше видео-файлов
                if (fi.Length > 10_000 && fi.Length <= 10_000_000)
                    filePriority += 1000;

                // картинки 10k-200k - максиммальный приоритет
                // 10k-200k = 99*100 + 3000 = 12900
                if (fi.Length > 10_000 && fi.Length <= 200_000)
                {
                    filePriority += 99 * 100;
                }

                // 0.2M-10M (шаг 100k) 98 уровней
                // 0.2М - 0.3М = 98 * 100 + 3000 = 12800
                // 9.9M - 10M = 1 * 100 + 3000 = 3100
                else if (fi.Length > 200_000 && fi.Length <= 10_000_000)
                {
                    int level = 98 - (int)((fi.Length - 200_000) / 100_000);
                    filePriority += level * 100;
                }

                // 10M - 20M(шаг 1M) 10 уровней
                // 10М - 11М = 10 * 100 + 1000 = 2000...
                // 19М - 20М = 1 * 100 + 1000 = 1100
                else if (fi.Length > 10_000_000 && fi.Length <= 20_000_000)
                {
                    int level = 10 - (int)((fi.Length - 10_000_000) / 1_000_000);
                    filePriority += level * 100;
                }
            }
            else if(IsFileVideo(fi))
            {
                // 0-4G (шаг 10M) 400 уровней
                // 0М - 10М = 400 * 100 = 40_000
                // 3990M -4000M = 1 * 100  = 100
                if (fi.Length <= 4_000_000_000)
                {
                    int level = 400 - (int)((fi.Length) / 10_000_000);
                    filePriority += level * 100;
                }
            }

            // TODO: 6 - без расширения (проверка по собержимому) 


            // Средние подгруппы по имени папки:
            // 40 - фото*, *, *?, *?, *, 
            // 30 - Desktop, Documents
            // 20 - по-умолчанию (останьные)
            // 10 - корзина, *Recycle.Bin *, Temp
            // 0 - Downloads

            int folderPriority = 20; // по-умолчанию (останьные)
            if (fi.DirectoryName.ToLower().Contains("фото") ||
                fi.DirectoryName.ToLower().Contains("фотки") ||
                fi.DirectoryName.ToLower().Contains("foto") ||
                fi.DirectoryName.ToLower().Contains("icloud") ||
                fi.DirectoryName.ToLower().Contains("apple") ||
                fi.DirectoryName.ToLower().Contains("telegram") ||
                fi.DirectoryName.ToLower().Contains("instagram") ||
                fi.DirectoryName.ToLower().Contains("whatsapp") ||
                fi.DirectoryName.ToLower().Contains("dcim") ||
                fi.DirectoryName.ToLower().Contains("camera") ||
                fi.DirectoryName.ToLower().Contains("pictures"))
            {
                folderPriority = 40;
            }
            else if (fi.DirectoryName.ToLower().Contains("desktop") ||
                fi.DirectoryName.ToLower().Contains("documents"))
            {
                folderPriority = 30;
            }
            else if (fi.DirectoryName.ToLower().Contains("recycle.bin") ||
                fi.DirectoryName.ToLower().Contains("temp"))
            {
                folderPriority = 10;
            }
            else if (fi.DirectoryName.ToLower().Contains("downloads") ||
                fi.DirectoryName.ToLower().Contains("загрузки"))
            {
                folderPriority = 0;
            }

            filePriority += folderPriority;

            // Малые группы по шаблону имени файла
            // 1 - фотки и видео с телефона/камеры
            // 0 - остальные
            if (IsCamera(fi))
                filePriority += 1;

            fileOrderMap.Add(fi, filePriority);
        }


        for (int i = 65_000; i >= 0; i--)
        {
            foreach (KeyValuePair<FileInfo, int> kvp in fileOrderMap)
            {
                if (kvp.Value == i)
                {
                    sortedList.Add(kvp.Key);
                    Stat.AddFileToTolalStat(kvp.Key);

                    Trace.WriteLine($"Key = {kvp.Key}, size = {kvp.Key.Length:N0}, Value = {kvp.Value}"); // verbose
                }
            }
        }

        Trace.TraceInformation($"Sorted list size = {sortedList.Count:N0}"); // info
        return sortedList;
    }

    //----------------------------------------------------------------------
    // 
    public static void CopyFiles(List<FileInfo> sourceList, string destinationDir)
    {
        DateTime start = DateTime.Now;
        int count = 0;
        long currentFotalSize = 0;
        long fullFotalSize = 0;

        // Определяем общий размер 
        foreach (FileInfo fi in sourceList)
        {
            fullFotalSize += fi.Length;
        }

        // 
        foreach (FileInfo fi in sourceList) 
        {
            string fullDestinationDir = destinationDir + "\\" +
                                        fi.DirectoryName?.Replace(":", "");

            if (!Directory.Exists(fullDestinationDir))
            {
                Directory.CreateDirectory(fullDestinationDir);
            }

            try
            {
                File.Copy(fi.FullName, fullDestinationDir + "\\" + fi.Name);
                currentFotalSize += fi.Length;
                long copyPercent = currentFotalSize * 100 / fullFotalSize;
                Trace.TraceInformation($"[{DateTime.Now - start}][Copied {currentFotalSize:N0} ({copyPercent}%)] Copy file #{++count:N0} = {fi.FullName} - size {fi.Length:N0}"); // info

                Stat.AddFileToCompletedStat(fi);
                Stat.RecalculateEstimatedTime();

            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[{DateTime.Now - start}] {ex.Message}");
            }
        }
    }

    //----------------------------------------------------------------------
    // Глобально исключаем папки
    // Windows "Program Files" "Program Files (x86)" ProgramData
    // эти папки уже наверное поштучно будем исключать AppData Downloads Загрузки
    public static bool IsDirectoryShouldBeSkipped(string dirName) =>
        dirName switch
        {
            "Windows" => true,
            "Program Files" => true,
            "Program Files (x86)" => true,
            "ProgramData" => true,
            "AppData" => true,
            "Курсовые работы" => true,

            _ => false
        };

    //----------------------------------------------------------------------
    // Глобально исключаем файлы
    public static bool IsFileShouldBeSkipped(FileInfo fi)
    {
        // предварительная проверка расширения 
        if ((IsFileImage(fi) == false) && (IsFileVideo(fi) == false)) 
            return true;

        // предварительная проверка размера
        if (fi.Length < 10_000 || fi.Length > 4_000_000_000)
        {
            //Trace.WriteLine($"Skip file by size ({fi.Length}) - {fi.Name}"); // Verbose
            return true;
        }

        // предварительная проверка имени файла
        // (ненужны всякие скачанные фильмы)
        // *S??E* - TODO
        if (fi.Name.Contains("Rip") ||
            fi.Name.Contains("WEB") ||
            fi.Name.Contains(".TS.") ||
            fi.Name.Contains("Dub") ||
            fi.Name.Contains("Season") ||
            fi.Name.Contains("XviD") ||
            // fi.Name.Contains("Scr") ||
            fi.Name.Contains("720i") ||
            fi.Name.Contains("720p") ||
            fi.Name.Contains("1080i") ||
            fi.Name.Contains("1080p"))
        {
            Trace.WriteLine($"Skip file by film name - {fi.Name}"); // Verbose
            return true;
        }

        return false;
    }

    //----------------------------------------------------------------------
    public static bool IsFileImage(FileInfo fi)
    {
        string ext = fi.Extension.ToLower();
        return (ext == ".jpg" ||
                ext == ".jpeg" ||
                ext == ".heic") ? true : false;
    }

    //----------------------------------------------------------------------
    public static bool IsFileVideo(FileInfo fi)
    {
        string ext = fi.Extension.ToLower();
        return (ext == ".mov" ||
                ext == ".mp4" ||
                ext == ".mpg" ||
                ext == ".avi" ||
                ext == ".mts" ||
                ext == ".3gp" ||
                ext == ".asf") ? true : false;
    }
    //----------------------------------------------------------------------
    
    public static bool IsCamera(FileInfo fi)
    {
        string[] patterns =
        {
            // IMG_0008.jpg 
            // IMG_3490.avi
            // MOV_3225.avi
            "(img|mov)_\\d{4}\\.(jpe?g|avi)", 

            // IMG_20220103_143124.jpg
            // 20190331_115946.mp4
            // 20150414_170108.MOV
            "\\d{8}_\\d{6}\\.(jpe?g|mp4|mpg|mov|3gp)",

            // MVI_1260.AVI
            "mvi_\\d{4}\\.avi", 

            // IMG-20190218-WA0000.jpg
            // VID-20201214-WA0028.mp4
            "(img|vid)-\\d{8}-wa\\d{4}\\.(jpe?g|mp4|mpg)",

            // 2013-02-20 11.30.58.jpg
            "\\d{4}-\\d{2}-\\d{2}\\s\\d{2}.\\d{2}.\\d{2}\\.jpe?g",

            // DSC_0581.jpg
            // DSC02803.JPG
            "dsc.\\d{4}\\.jpe?g",

            // EOS11195.JPG
            "eos\\d{5}\\.jpe?g",

            // STA_0957.jpg
            // STL_0240.JPG
            "st._\\d{4}\\.jpg",

            // SANY1218.JPG
            "sany\\d{4}\\.jpg",

            // photo_2023-04-15_23-28-07.jpg
            // video_2022-10-03_15-45-57.mp4
            "(photo|video)_\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2}.*\\.(jpe?g|mp4|mpg)",

            // 2013-09-16 07.59.34.mp4
            "\\d{4}-\\d{2}-\\d{2}\\s\\d{2}\\.\\d{2}\\.\\d{2}\\.(jpe?g|mp4|mpg)",

            // P1000777.JPG
            // P1000942.MOV
            // S1051996.JPG
            // S1051995.AVI
            "(p|s)\\d{7}\\.(jpe?g|mov|avi)",

            // foto 002.jpg 
            "foto\\s\\d{3}\\.jpg",

            // IMAG0008.JPG
            // IMAG0009.ASF
            "imag\\d{4}\\.(jpg|asf)",
            
            //16072007.3gp
            "\\d{4}\\.(jpg|asf)",

            //VIDEO0001.3gp
            "video\\d{4}\\.3gp",

            //M2U00020.MPG
            "m2u\\d{5}\\.mpg",

            // TODO: 
            // проверил до 2008 включительно
        };

        foreach (string pattern in patterns)
        {
            if (Regex.IsMatch(fi.Name, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;          
    }
    //----------------------------------------------------------------------
}