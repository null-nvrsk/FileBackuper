using System.Runtime.InteropServices;

namespace FileBackuper.Console;

internal static class ConsoleWindow
{
    private const int HideWindowCommand = 0;
    private const int ShowWindowCommand = 5;

    public static void SetVisibility(bool isVisible)
    {
        IntPtr consoleWindow = GetConsoleWindow();
        if (consoleWindow != IntPtr.Zero)
            ShowWindow(consoleWindow, isVisible ? ShowWindowCommand : HideWindowCommand);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);
}
