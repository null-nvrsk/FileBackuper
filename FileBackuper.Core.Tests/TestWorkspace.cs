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
        File.WriteAllBytes(filePath, new byte[sizeInBytes]);
        return new FileInfo(filePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }
}
