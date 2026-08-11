namespace FileBackuper.Core;

public sealed class FileSizeGroupService
{
    private readonly IReadOnlyList<FileSizeGroupOptions> groups;

    public FileSizeGroupService(IEnumerable<FileSizeGroupOptions> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        this.groups = groups.ToList();
        if (this.groups.Count == 0)
            throw new ArgumentException("At least one file size group is required.", nameof(groups));
    }

    public FileSizeGroupOptions GetGroup(long fileSizeBytes) => groups[GetGroupIndex(fileSizeBytes)];

    public bool TryGetGroup(long fileSizeBytes, out FileSizeGroupOptions? group)
    {
        group = groups.FirstOrDefault(candidate =>
            fileSizeBytes >= candidate.MinBytes && fileSizeBytes <= candidate.MaxBytes);
        return group is not null;
    }

    public int GetGroupIndex(long fileSizeBytes)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            FileSizeGroupOptions group = groups[index];
            if (fileSizeBytes >= group.MinBytes && fileSizeBytes <= group.MaxBytes)
                return index;
        }

        throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), fileSizeBytes,
            "The file size does not belong to any configured size group.");
    }
}
