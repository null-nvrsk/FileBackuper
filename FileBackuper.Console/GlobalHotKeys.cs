using FileBackuper.Core;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FileBackuper.Console;

internal sealed class GlobalHotKeys : IDisposable
{
    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint Shift = 0x0004;
    private const uint NoRepeat = 0x4000;
    private const uint HotKeyMessage = 0x0312;
    private const uint QuitMessage = 0x0012;
    private const int PauseId = 1;
    private const int ResumeId = 2;
    private const int ExitId = 3;
    private const uint PKey = 0x50;
    private const uint RKey = 0x52;
    private const uint XKey = 0x58;

    private readonly Action pause;
    private readonly Action resume;
    private readonly Action exit;
    private readonly Thread thread;
    private readonly ManualResetEventSlim started = new();
    private uint threadId;
    private bool disposed;

    public GlobalHotKeys(Action pause, Action resume, Action exit)
    {
        this.pause = pause ?? throw new ArgumentNullException(nameof(pause));
        this.resume = resume ?? throw new ArgumentNullException(nameof(resume));
        this.exit = exit ?? throw new ArgumentNullException(nameof(exit));
        thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "FileBackuper global hotkeys"
        };
        thread.Start();
        started.Wait();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (threadId != 0)
            PostThreadMessage(threadId, QuitMessage, UIntPtr.Zero, IntPtr.Zero);
        if (thread.IsAlive)
            thread.Join();
        started.Dispose();
    }

    private void RunMessageLoop()
    {
        threadId = GetCurrentThreadId();
        bool pauseRegistered = Register(PauseId, PKey, "Ctrl+Shift+Alt+P");
        bool resumeRegistered = Register(ResumeId, RKey, "Ctrl+Shift+Alt+R");
        bool exitRegistered = Register(ExitId, XKey, "Ctrl+Shift+Alt+X");
        started.Set();

        try
        {
            while (GetMessage(out NativeMessage message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.Message != HotKeyMessage)
                    continue;

                try
                {
                    switch (unchecked((int)message.WParam.ToUInt64()))
                    {
                        case PauseId: pause(); break;
                        case ResumeId: resume(); break;
                        case ExitId: exit(); break;
                    }
                }
                catch (Exception exception)
                {
                    BackupLog.Warning("Ошибка обработки горячей клавиши. " +
                        BackupLog.GetExceptionDescription(exception));
                    BackupLog.Flush();
                }
            }
        }
        finally
        {
            if (pauseRegistered) UnregisterHotKey(IntPtr.Zero, PauseId);
            if (resumeRegistered) UnregisterHotKey(IntPtr.Zero, ResumeId);
            if (exitRegistered) UnregisterHotKey(IntPtr.Zero, ExitId);
        }
    }

    private static bool Register(int id, uint key, string displayName)
    {
        if (RegisterHotKey(IntPtr.Zero, id, Control | Shift | Alt | NoRepeat, key))
            return true;

        BackupLog.Warning($"Не удалось зарегистрировать горячую клавишу {displayName}. " +
            new Win32Exception(Marshal.GetLastWin32Error()).Message);
        BackupLog.Flush();
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr WindowHandle;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, IntPtr windowHandle,
        uint minimumMessage, uint maximumMessage);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
