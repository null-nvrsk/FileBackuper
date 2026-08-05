using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FileBackuper.Core;

public static class VolumeIdentity
{
    public static string GetVolumeId(DriveInfo drive)
    {
        ArgumentNullException.ThrowIfNull(drive);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Получение GUID тома поддерживается только в Windows.");
        if (!drive.IsReady)
            throw new InvalidOperationException($"Диск {drive.Name} недоступен.");

        StringBuilder volumeName = new(capacity: 1024);
        if (!GetVolumeNameForVolumeMountPoint(drive.RootDirectory.FullName, volumeName, volumeName.Capacity))
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode,
                $"Не удалось получить GUID тома для диска {drive.Name}.");
        }

        return volumeName.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeMountPoint,
        StringBuilder volumeName,
        int volumeNameSize);
}
