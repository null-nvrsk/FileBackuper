namespace FileBackuper.Core.Tests;

public class FileCopierTests
{
    [Fact]
    public void CopyFile_CopiesOnceAndThenReportsCurrentTarget()
    {
        using TestWorkspace workspace = new();
        FileInfo sourceFile = workspace.CreateFile("photo.jpg", 10);
        string destinationDirectory = Path.Combine(workspace.RootDirectory.FullName, "destination");

        bool firstResult = FileCopier.CopyFile(sourceFile, destinationDirectory, CancellationToken.None);
        bool secondResult = FileCopier.CopyFile(sourceFile, destinationDirectory, CancellationToken.None);

        Assert.False(firstResult);
        Assert.True(secondResult);
    }

    [Fact]
    public void CopyFile_AddsDetectedExtensionToExtensionlessDestinationFile()
    {
        using TestWorkspace workspace = new();
        FileInfo sourceFile = workspace.CreateFile(Path.Combine("cache", "f_000001"), 10);
        BackupFileCandidate candidate = new(sourceFile, new MediaFileAnalysis
        {
            FileSizeBytes = sourceFile.Length,
            Kind = MediaKind.Image,
            DetectionSource = MediaDetectionSource.Signature,
            DetectedExtension = ".jpg"
        });
        string destinationDirectory = Path.Combine(workspace.RootDirectory.FullName, "destination");

        bool firstResult = FileCopier.CopyFile(candidate, destinationDirectory, CancellationToken.None);
        bool secondResult = FileCopier.CopyFile(candidate, destinationDirectory, CancellationToken.None);

        string targetDirectory = destinationDirectory + "\\" + sourceFile.DirectoryName!.Replace(":", "");
        Assert.True(File.Exists(Path.Combine(targetDirectory, "f_000001.jpg")));
        Assert.False(File.Exists(Path.Combine(targetDirectory, "f_000001")));
        Assert.False(firstResult);
        Assert.True(secondResult);
    }

    [Fact]
    public void CopyFile_DoesNotReplaceExistingSourceExtensionWithDetectedExtension()
    {
        using TestWorkspace workspace = new();
        FileInfo sourceFile = workspace.CreateFile("photo.jpeg", 10);
        BackupFileCandidate candidate = new(sourceFile, new MediaFileAnalysis
        {
            FileSizeBytes = sourceFile.Length,
            Kind = MediaKind.Image,
            DetectionSource = MediaDetectionSource.Signature,
            DetectedExtension = ".jpg"
        });
        string destinationDirectory = Path.Combine(workspace.RootDirectory.FullName, "destination");

        FileCopier.CopyFile(candidate, destinationDirectory, CancellationToken.None);

        string targetDirectory = destinationDirectory + "\\" + sourceFile.DirectoryName!.Replace(":", "");
        Assert.True(File.Exists(Path.Combine(targetDirectory, "photo.jpeg")));
        Assert.False(File.Exists(Path.Combine(targetDirectory, "photo.jpeg.jpg")));
    }
}
