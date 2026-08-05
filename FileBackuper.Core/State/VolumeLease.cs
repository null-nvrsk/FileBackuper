using System.Security.Cryptography;
using System.Text;

namespace FileBackuper.Core;

public sealed class VolumeLease : IDisposable
{
    private FileStream? lockStream;

    private VolumeLease(string volumeId, string lockPath, FileStream lockStream)
    {
        VolumeId = volumeId;
        LockPath = lockPath;
        this.lockStream = lockStream;
    }

    public string VolumeId { get; }

    public string LockPath { get; }

    public static VolumeLease? TryAcquire(string stateDirectory, string volumeId)
    {
        if (string.IsNullOrWhiteSpace(stateDirectory))
            throw new ArgumentException("Каталог состояния не может быть пустым.", nameof(stateDirectory));
        if (string.IsNullOrWhiteSpace(volumeId))
            throw new ArgumentException("Идентификатор тома не может быть пустым.", nameof(volumeId));

        string volumesDirectory = Path.Combine(Path.GetFullPath(stateDirectory), "volumes");
        Directory.CreateDirectory(volumesDirectory);
        string lockFileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(volumeId))) + ".lock";
        string lockPath = Path.Combine(volumesDirectory, lockFileName);

        try
        {
            FileStream lockStream = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new VolumeLease(volumeId, lockPath, lockStream);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        lockStream?.Dispose();
        lockStream = null;
    }
}
