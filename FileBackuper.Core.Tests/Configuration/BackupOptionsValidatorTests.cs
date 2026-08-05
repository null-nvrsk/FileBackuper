using FileBackuper.Core;

namespace FileBackuper.Core.Tests.Configuration;

public class BackupOptionsValidatorTests
{
    [Fact]
    public void Validate_ThrowsForNonPositiveDrivePollingInterval()
    {
        BackupOptions options = new() { DrivePollingIntervalSeconds = 0 };

        Assert.Throws<InvalidOperationException>(() =>
            BackupOptionsValidator.Validate(options, Path.GetTempPath()));
    }
}
