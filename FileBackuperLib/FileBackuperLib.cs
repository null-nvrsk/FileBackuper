using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Diagnostics;
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
        // 9 - картинки
        // 8 - видео < 100 Mb 
        // 7 - видео 100-200 Mb
        // TODO: 6 - без расширения (проверка по собержимому) 
        // 5 - видео 200-300 Mb
        // 4 - видео 300-400 Mb
        // 3 - видео 400-500 Mb
        // 2 - видео 500-1000 Mb
        // 1 - видео 1-4 Gb

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
            // 9 - картинки (максимальный приоритет)
            if (IsFileImage(fi)) filePriority = 900;

            // 8 - видео < 100 Mb
            if (fi.Length <= 100_000_000 && IsFileVideo(fi)) filePriority = 800;

            // 7 - видео 100-200 Mb
            if (fi.Length > 100_000_000 && 
                fi.Length <= 200_000_000 && 
                IsFileVideo(fi))
                filePriority = 700;

            // TODO: 6 - без расширения (проверка по собержимому) 

            // 5 - видео 200-300 Mb
            if (fi.Length > 200_000_000 &&
                fi.Length <= 300_000_000 &&
                IsFileVideo(fi))
                filePriority = 500;

            // 4 - видео 300-400 Mb
            if (fi.Length > 300_000_000 &&
                fi.Length <= 400_000_000 &&
                IsFileVideo(fi))
                filePriority = 400;

            // 3 - видео 400-500 Mb
            if (fi.Length > 400_000_000 &&
                fi.Length <= 500_000_000 &&
                IsFileVideo(fi))
                filePriority = 300;

            // 2 - видео 500-1000 Mb
            if (fi.Length > 500_000_000 &&
                fi.Length <= 1_000_000_000 &&
                IsFileVideo(fi))
                filePriority = 200;

            // 1 - видео 1-4 Gb
            if (fi.Length > 1_000_000_000 &&
                fi.Length <= 4_000_000_000 &&
                IsFileVideo(fi))
                filePriority = 100;

            // Средние группы по шаблону имени файла
            // 1 - фотки и видео с телефона/камеры
            // 0 - остальные
            if(IsCamera(fi))
                filePriority += 10;

            // Малые подгруппы по имени папки:
            // 5 - фото*, *, *?, *?, *, 
            // 4 - Desktop, Documents
            // 3 - корзина, *Recycle.Bin*, Temp
            // 2 - останьные
            // 1 - Downloads

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
                filePriority += 5;
            }
            else if (fi.DirectoryName.ToLower().Contains("desktop") ||
                fi.DirectoryName.ToLower().Contains("documents"))
            {
                filePriority += 4;
            }
            else if (fi.DirectoryName.ToLower().Contains("recycle.bin") ||
                fi.DirectoryName.ToLower().Contains("temp"))
            {
                filePriority += 3;
            }                    

            fileOrderMap.Add(fi, filePriority);
        }


        for (int i = 999; i >= 0; i--)
        {
            foreach (KeyValuePair<FileInfo, int> kvp in fileOrderMap)
            {
                if (kvp.Value == i)
                {
                    sortedList.Add(kvp.Key);

#if DEBUG
                    Trace.WriteLine($"Key = {kvp.Key}, size = {kvp.Key.Length:N0}, Value = {kvp.Value}"); // verbose
#endif
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

            File.Copy(fi.FullName, fullDestinationDir + "\\" + fi.Name);
            currentFotalSize += fi.Length;
            long copyPercent = currentFotalSize * 100 / fullFotalSize;
            Trace.TraceInformation($"[{DateTime.Now - start}][Copied {currentFotalSize:N0} ({copyPercent}%)] Copy file #{++count:N0} = {fi.FullName} - size {fi.Length:N0}"); // info
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
        if (fi.Length < 30_000 || fi.Length > 4_000_000_000)
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
            "img_\\d{4}\\.(jpe?g|avi)", 
            

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

            // photo_2023-04-15_23-28-07.jpg
            // video_2022-10-03_15-45-57.mp4
            "(photo|video)_\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2}.*\\.(jpe?g|mp4|mpg)",

        };

        foreach (string pattern in patterns)
        {
            if (Regex.IsMatch(fi.Name, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        // TODO: 
        // 2013-09-16 07.59.34.mp4

        // проверил до 2008 включительно

        // foto 002.jpg

        // P1000777.JPG
        // P1000942.MOV

        // S1051996.JPG
        // S1051995.AVI

        // EOS11195.JPG
        // SANY1218.JPG

        // STA_0957.jpg
        // STL_0240.JPG

        //MOV_3225.avi

        //VIDEO0001.3gp
        //M2U00020.MPG


        //16072007.3gp

        // IMAG0008.JPG
        // IMAG0009.ASF

        return false;
        //            
    }
    //----------------------------------------------------------------------
}