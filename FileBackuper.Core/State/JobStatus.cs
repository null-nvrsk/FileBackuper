namespace FileBackuper.Core;

public enum JobStatus
{
    Pending,
    Scanning,
    Sorting,
    Copying,
    Completed,
    Cancelled,
    Failed
}
