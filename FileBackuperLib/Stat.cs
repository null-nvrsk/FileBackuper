using FileBackuperLib;

namespace FileBackuper;

public static class Stat
{
    static DateTime startTime;
    static DateTime endTime;
    static TimeSpan? ETAimg; // примерное время окончания копирования картинок
    static TimeSpan ETAfull; //  примерное время окончания копирования
    static DateTime recalculateTime;

    static int totalCount = 0;

    static long totalSize = 0;
    static long completeSize = 0;

    static long totalImgSize = 0;
    static long completeImgSize = 0;

    static long totalVidSize = 0;
    static long completeVidSize = 0;

    static long currentFileSize = 0;

    static StatFile statFile = new StatFile();

    public static void Start()
    {
        startTime = DateTime.Now;
    }

    //--------------------------------------------------------------------------
    public static TimeSpan Stop()
    {
        statFile.CloseFile();
        endTime = DateTime.Now;
        return endTime - startTime;
        
    }

    //--------------------------------------------------------------------------
    public static TimeSpan GetCurrentScanTime()
    {
        return DateTime.Now - startTime;
    }

    //--------------------------------------------------------------------------
    public static void RecalculateEstimatedTime()
    {
        // пересчитываем расчетное время каждые (10) 30 секунд
        if ((DateTime.Now - recalculateTime).TotalSeconds < 27)
            return;

        // пересчитываем только когда достаточно много уже скопировано
        if (completeSize < 10_000_000)
            return;

        recalculateTime = DateTime.Now;

        TimeSpan? getImageDateTime = GetETAimg();
        if (getImageDateTime != null)
        {
            ETAimg = getImageDateTime;
        }
        ETAfull = GetETAfull();

        statFile.GenerateNewFile(
            GetPercentageOfCompletion(),
            GetCurrentGroupType(),
            currentFileSize,
            ETAimg,
            ETAfull,
            GetCurrentScanTime());
    }

    //--------------------------------------------------------------------------
    public static int GetPercentageOfCompletion()
    {
        if (totalSize > 0)
            return (int)((double)completeSize / totalSize * 100);
        else 
            return 0;
    }

    //--------------------------------------------------------------------------
    // Указывает в какой группе сейчас идет копирование
    public static GroupType GetCurrentGroupType()
    {
        return (completeVidSize == 0) ? GroupType.Image : GroupType.Video;
    }

    //--------------------------------------------------------------------------
    // Добавляем файл в общую статистику

    public static void AddFileToTolalStat(FileInfo fi)
    {
        totalCount += 1;
        totalSize += fi.Length;

        if (FileBackuperLib.IsFileImage(fi))
        {
            totalImgSize += fi.Length;
        }

        if (FileBackuperLib.IsFileVideo(fi))
        {
            totalVidSize += fi.Length;
        }
    }

    //--------------------------------------------------------------------------
    // Добавляем файл в статистику скопированных

    public static void AddFileToCompletedStat(FileInfo fi)
    {
        currentFileSize = fi.Length;
        completeSize += fi.Length;

        if (FileBackuperLib.IsFileImage(fi))
        {
            completeImgSize += fi.Length;
        }

        if (FileBackuperLib.IsFileVideo(fi))
        {
            completeVidSize += fi.Length;
        }
    }

    
    //--------------------------------------------------------------------------
    // перерасчет примерного времи, через сколько окончится копирование картинок
    public static TimeSpan? GetETAimg()
    {
        // Если все картинки скопированы, то подсчет пропускаем
        if (completeImgSize == totalImgSize)
            return null;

        // посчитать среднюю скорость копирования (байт / с)
        double scanTimeSeconds = GetCurrentScanTime().TotalSeconds;
        double scanSpeedBytesPerSecond = (double)completeImgSize / scanTimeSeconds;

        // расчитываем сколько еще надо секунд
        double secondsToComplete = (totalImgSize - completeImgSize) / scanSpeedBytesPerSecond;

        // примерное время гогда закончить копирование картинок
        DateTime imgEndTime = DateTime.Now.AddSeconds(secondsToComplete);
        
        return imgEndTime - DateTime.Now;
    }

    //--------------------------------------------------------------------------
    // перерасчет примерного время окончания копирования картинок
    public static TimeSpan GetETAfull()
    {
        // посчитать среднюю скорость копирования (байт / с)
        double scanTimeSeconds = GetCurrentScanTime().TotalSeconds;
        double scanSpeedBytesPerSecond = (double)completeSize / scanTimeSeconds;

        // расчитываем сколько еще надо секунд
        double secondsToComplete = (totalSize - completeSize) / scanSpeedBytesPerSecond;
        DateTime fullEndTime = DateTime.Now.AddSeconds(secondsToComplete);
        return fullEndTime - DateTime.Now;
    }
}
