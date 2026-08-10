using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FileBackuper.Core;

public static class CloudFileState
{
    private const uint FileReadAttributes = 0x0080;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const int FileAttributeTagInfo = 9;

    private const uint Placeholder = 0x00000001;
    private const uint Partial = 0x00000010;
    private const uint PartiallyOnDisk = 0x00000020;
    private const uint Invalid = 0xffffffff;
    private const uint CloudFileAttributeMask =
        (uint)(FileAttributes.ReparsePoint | FileAttributes.Offline) |
        0x00040000 | // FILE_ATTRIBUTE_RECALL_ON_OPEN
        0x00080000 | // FILE_ATTRIBUTE_PINNED
        0x00100000 | // FILE_ATTRIBUTE_UNPINNED
        0x00400000;  // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
    private const int PlaceholderStandardInfo = 1;
    private const uint PinStatePinned = 1;
    private const int ErrorMoreDataHResult = unchecked((int)0x800700EA);

    public static bool IsContentAvailableLocally(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        // Reading attributes through an OPEN_REPARSE_POINT handle does not read
        // placeholder content and therefore does not trigger cloud hydration.
        using SafeFileHandle handle = CreateFile(GetExtendedPath(file.FullName), FileReadAttributes,
            FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, OpenExisting,
            FileFlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (!GetFileInformationByHandleEx(handle, FileAttributeTagInfo, out FileAttributeTagInfoData info,
                (uint)Marshal.SizeOf<FileAttributeTagInfoData>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        uint state = CfGetPlaceholderStateFromAttributeTag(info.FileAttributes, info.ReparseTag);
        if (!IsContentAvailableLocally(state))
            return false;

        int result = CfGetPlaceholderInfo(handle, PlaceholderStandardInfo,
            out PlaceholderStandardInfoData placeholderInfo,
            (uint)Marshal.SizeOf<PlaceholderStandardInfoData>(), out _);
        if (result == 0 || result == ErrorMoreDataHResult)
            return IsPinnedPlaceholderContentFullyOnDisk(
                file.Length, placeholderInfo.OnDiskDataSize, placeholderInfo.PinState);

        // On win-x86 Yandex Disk may expose ReparseTag as zero, so a zero
        // placeholder state is not enough to classify the file as ordinary.
        // Cloud-related attributes make an inconclusive query unsafe: reading
        // such a file could start hydration.
        if ((info.FileAttributes & CloudFileAttributeMask) != 0)
            return false;

        return true;
    }

    public static bool IsContentAvailableLocally(uint placeholderState)
    {
        if (placeholderState == Invalid)
            return false;

        bool isPlaceholder = (placeholderState & Placeholder) != 0;
        bool contentIsNotReady = (placeholderState & (Partial | PartiallyOnDisk)) != 0;
        return !isPlaceholder || !contentIsNotReady;
    }

    public static bool IsPlaceholderContentFullyOnDisk(long fileLength, long onDiskDataSize)
    {
        if (fileLength < 0)
            throw new ArgumentOutOfRangeException(nameof(fileLength));

        return onDiskDataSize >= fileLength;
    }

    public static bool IsPinnedPlaceholderContentFullyOnDisk(
        long fileLength, long onDiskDataSize, uint pinState) =>
        pinState == PinStatePinned && IsPlaceholderContentFullyOnDisk(fileLength, onDiskDataSize);

    public static string GetExtendedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path;
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\\?\UNC\" + path[2..];

        return @"\\?\" + path;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfoData
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PlaceholderStandardInfoData
    {
        public long OnDiskDataSize;
        public long ValidatedDataSize;
        public long ModifiedDataSize;
        public long PropertiesSize;
        public uint PinState;
        public uint InSyncState;
        public long FileId;
        public long SyncRootFileId;
        public uint FileIdentityLength;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out FileAttributeTagInfoData fileInformation,
        uint bufferSize);

    [DllImport("cldapi.dll")]
    private static extern uint CfGetPlaceholderStateFromAttributeTag(uint fileAttributes, uint reparseTag);

    [DllImport("cldapi.dll")]
    private static extern int CfGetPlaceholderInfo(
        SafeFileHandle file,
        int infoClass,
        out PlaceholderStandardInfoData infoBuffer,
        uint infoBufferLength,
        out uint returnedLength);
}
