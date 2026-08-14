namespace FileBackuper.Core;

public sealed class BackupFilePriorityService
{
    private readonly FileSizeGroupService sizeGroupService;
    private readonly FolderPriorityService folderPriorityService;

    public BackupFilePriorityService(FileSizeGroupService sizeGroupService,
        FolderPriorityService? folderPriorityService = null)
    {
        ArgumentNullException.ThrowIfNull(sizeGroupService);
        this.sizeGroupService = sizeGroupService;
        this.folderPriorityService = folderPriorityService ?? new FolderPriorityService();
        Comparer = System.Collections.Generic.Comparer<BackupFileCandidate>.Create(CompareByBackupPriority);
    }

    public IComparer<BackupFileCandidate> Comparer { get; }

    public List<BackupFileCandidate> OrderByBackupPriority(IEnumerable<BackupFileCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        List<PrioritizedCandidate> prioritizedCandidates = new();
        foreach (BackupFileCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            prioritizedCandidates.Add(new PrioritizedCandidate(candidate, CreateKey(candidate)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        prioritizedCandidates.Sort(static (first, second) => CompareKeys(first.Key, second.Key));
        return prioritizedCandidates.Select(item => item.Candidate).ToList();
    }

    public int CompareByBackupPriority(BackupFileCandidate? first, BackupFileCandidate? second)
    {
        if (ReferenceEquals(first, second))
            return 0;
        if (first is null)
            return 1;
        if (second is null)
            return -1;

        return CompareKeys(CreateKey(first), CreateKey(second));
    }

    public int GetSizeGroupIndex(BackupFileCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return sizeGroupService.GetGroupIndex(candidate.Length);
    }

    public string GetPriorityCode(BackupFileCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        int sizeGroupNumber = GetSizeGroupIndex(candidate) + 1;
        char detectionType = candidate.Analysis.DetectionSource == MediaDetectionSource.Signature ? 'S' : 'E';
        char cameraType = candidate.Analysis.CameraEvidence == CameraEvidence.None ? 'O' : 'C';
        int folderPriority = folderPriorityService.GetPriority(candidate.File);
        return $"[{sizeGroupNumber:D2}{detectionType}{cameraType}{folderPriority}]";
    }

    private PriorityKey CreateKey(BackupFileCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new PriorityKey(
            sizeGroupService.GetGroupIndex(candidate.Length),
            GetDetectionRank(candidate.Analysis.DetectionSource),
            candidate.Analysis.CameraEvidence,
            folderPriorityService.GetPriority(candidate.File),
            candidate.File.FullName);
    }

    private static int CompareKeys(PriorityKey first, PriorityKey second)
    {
        int comparison = first.SizeGroupIndex.CompareTo(second.SizeGroupIndex);
        if (comparison != 0)
            return comparison;

        comparison = first.DetectionRank.CompareTo(second.DetectionRank);
        if (comparison != 0)
            return comparison;

        comparison = second.CameraEvidence.CompareTo(first.CameraEvidence);
        if (comparison != 0)
            return comparison;

        comparison = second.FolderPriority.CompareTo(first.FolderPriority);
        if (comparison != 0)
            return comparison;

        return StringComparer.OrdinalIgnoreCase.Compare(first.FullPath, second.FullPath);
    }

    private static int GetDetectionRank(MediaDetectionSource detectionSource) => detectionSource switch
    {
        MediaDetectionSource.Extension => 0,
        MediaDetectionSource.Signature => 1,
        _ => 2
    };

    private readonly record struct PriorityKey(
        int SizeGroupIndex,
        int DetectionRank,
        CameraEvidence CameraEvidence,
        int FolderPriority,
        string FullPath);

    private readonly record struct PrioritizedCandidate(BackupFileCandidate Candidate, PriorityKey Key);
}
