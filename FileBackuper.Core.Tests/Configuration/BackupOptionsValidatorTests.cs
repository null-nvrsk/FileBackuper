using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Configuration;

public class BackupOptionsValidatorTests
{
    [Fact]
    public void Validate_AcceptsDefaultFileSizeGroups()
    {
        BackupOptions options = new();

        BackupOptionsValidator.Validate(options, Path.GetTempPath());
    }

    [Fact]
    public void Validate_ThrowsForNonPositiveDrivePollingInterval()
    {
        BackupOptions options = new() { DrivePollingIntervalSeconds = 0 };

        Assert.Throws<InvalidOperationException>(() =>
            BackupOptionsValidator.Validate(options, Path.GetTempPath()));
    }

    [Fact]
    public void Validate_ThrowsWhenFileSizeGroupsHaveGap()
    {
        BackupOptions options = CreateOptionsWithGroups(
            new FileSizeGroupOptions { Name = "First", MinBytes = 10, MaxBytes = 20 },
            new FileSizeGroupOptions { Name = "Second", MinBytes = 22, MaxBytes = 30 });

        Assert.Throws<InvalidOperationException>(() =>
            BackupOptionsValidator.Validate(options, Path.GetTempPath()));
    }

    [Fact]
    public void Validate_ThrowsWhenFileSizeGroupNamesAreDuplicatedIgnoringCase()
    {
        BackupOptions options = CreateOptionsWithGroups(
            new FileSizeGroupOptions { Name = "Files", MinBytes = 10, MaxBytes = 20 },
            new FileSizeGroupOptions { Name = "files", MinBytes = 21, MaxBytes = 30 });

        Assert.Throws<InvalidOperationException>(() =>
            BackupOptionsValidator.Validate(options, Path.GetTempPath()));
    }

    private static BackupOptions CreateOptionsWithGroups(params FileSizeGroupOptions[] groups) => new()
    {
        MinFileSizeBytes = 10,
        MaxFileSizeBytes = 30,
        FileSizeGroups = groups.ToList()
    };
}
