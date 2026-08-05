namespace FileBackuper.Core.Tests;

internal sealed class TestWorkspace : IDisposable
{
    private readonly string directoryPath = Path.Combine(Path.GetTempPath(), "FileBackuper.Tests", Guid.NewGuid().ToString("N"));

    public TestWorkspace()
    {
        Directory.CreateDirectory(directoryPath);
    }

    public FileInfo CreateFile(string fileName, int sizeInBytes)
    {
        string filePath = Path.Combine(directoryPath, fileName);
        string? parentDirectory = Path.GetDirectoryName(filePath);
        if (parentDirectory != null)
            Directory.CreateDirectory(parentDirectory);
        File.WriteAllBytes(filePath, new byte[sizeInBytes]);
        return new FileInfo(filePath);
    }

    public DirectoryInfo RootDirectory => new(directoryPath);

    public void Dispose()
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }
}
