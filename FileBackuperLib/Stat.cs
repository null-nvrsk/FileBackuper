namespace FileBackuper;

public class Stat
{
    int[] totalFiles = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    long[] totalFileSize = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    DateTime start;
    DateTime end;

    public void Start()
    {
        start = DateTime.Now;
    }

    //---------------------------------------------------------------------
    public TimeSpan Stop()
    {
        end = DateTime.Now;
        TimeSpan scanTime = end - start;
        return scanTime;
    }

}
