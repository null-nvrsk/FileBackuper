namespace FileBackuper.Core;

/// <summary>
/// A shared copy queue prepared from one or more source-volume jobs.
/// </summary>
public sealed class BackupJobBatch
{
    private readonly Dictionary<BackupFileCandidate, BackupJob> sourceJobs = new();
    private readonly List<BackupFileCandidate> files = new();

    public BackupJobBatch(IEnumerable<BackupJob> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        Jobs = jobs.ToList();
        if (Jobs.Count == 0)
            throw new ArgumentException("A batch must contain at least one job.", nameof(jobs));
    }

    public IReadOnlyList<BackupJob> Jobs { get; }

    public IReadOnlyList<BackupFileCandidate> Files => files;

    public void CollectScannedFiles()
    {
        files.Clear();
        sourceJobs.Clear();

        foreach (BackupJob job in Jobs.Where(job => job.Status == JobStatus.Sorting))
        {
            foreach (BackupFileCandidate candidate in job.Files)
            {
                files.Add(candidate);
                sourceJobs.Add(candidate, job);
            }
        }
    }

    public void SetSortedFiles(IEnumerable<BackupFileCandidate> sortedFiles)
    {
        ArgumentNullException.ThrowIfNull(sortedFiles);
        List<BackupFileCandidate> orderedFiles = sortedFiles.ToList();
        if (orderedFiles.Count != files.Count || orderedFiles.Any(candidate => !sourceJobs.ContainsKey(candidate)))
            throw new ArgumentException("The sorted files must be the files collected by this batch.", nameof(sortedFiles));

        files.Clear();
        files.AddRange(orderedFiles);

        foreach (BackupJob job in Jobs.Where(job => job.Status == JobStatus.Sorting))
        {
            IEnumerable<BackupFileCandidate> jobFiles = files.Where(candidate => sourceJobs[candidate] == job);
            job.SetSortedFiles(jobFiles);
        }
    }

    public BackupJob GetSourceJob(BackupFileCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return sourceJobs.TryGetValue(candidate, out BackupJob? job)
            ? job
            : throw new ArgumentException("The file does not belong to this batch.", nameof(candidate));
    }
}
