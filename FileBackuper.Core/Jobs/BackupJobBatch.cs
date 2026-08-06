namespace FileBackuper.Core;

/// <summary>
/// A shared copy queue prepared from one or more source-volume jobs.
/// </summary>
public sealed class BackupJobBatch
{
    private readonly Dictionary<FileInfo, BackupJob> sourceJobs = new();
    private readonly List<FileInfo> files = new();

    public BackupJobBatch(IEnumerable<BackupJob> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        Jobs = jobs.ToList();
        if (Jobs.Count == 0)
            throw new ArgumentException("A batch must contain at least one job.", nameof(jobs));
    }

    public IReadOnlyList<BackupJob> Jobs { get; }

    public IReadOnlyList<FileInfo> Files => files;

    public void CollectScannedFiles()
    {
        files.Clear();
        sourceJobs.Clear();

        foreach (BackupJob job in Jobs.Where(job => job.Status == JobStatus.Sorting))
        {
            foreach (FileInfo file in job.Files)
            {
                files.Add(file);
                sourceJobs.Add(file, job);
            }
        }
    }

    public void SetSortedFiles(IEnumerable<FileInfo> sortedFiles)
    {
        ArgumentNullException.ThrowIfNull(sortedFiles);
        List<FileInfo> orderedFiles = sortedFiles.ToList();
        if (orderedFiles.Count != files.Count || orderedFiles.Any(file => !sourceJobs.ContainsKey(file)))
            throw new ArgumentException("The sorted files must be the files collected by this batch.", nameof(sortedFiles));

        files.Clear();
        files.AddRange(orderedFiles);

        foreach (BackupJob job in Jobs.Where(job => job.Status == JobStatus.Sorting))
        {
            IEnumerable<FileInfo> jobFiles = files.Where(file => sourceJobs[file] == job);
            job.SetSortedFiles(jobFiles);
        }
    }

    public BackupJob GetSourceJob(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return sourceJobs.TryGetValue(file, out BackupJob? job)
            ? job
            : throw new ArgumentException("The file does not belong to this batch.", nameof(file));
    }
}
