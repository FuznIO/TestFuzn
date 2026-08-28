using System.Runtime.InteropServices;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Enables ANSI/VT escape sequence processing on the Windows console.
/// No-op reporting success on non-Windows platforms.
/// </summary>
internal static class WindowsVirtualTerminal
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    /// <summary>
    /// Returns true when VT output processing is available: already enabled, enabled by this
    /// call, or running on a non-Windows platform where no enablement is needed.
    /// </summary>
    public static bool TryEnable()
    {
        if (!OperatingSystem.IsWindows())
            return true;

        var handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return false;

        if (!GetConsoleMode(handle, out var mode))
            return false;

        if ((mode & EnableVirtualTerminalProcessing) != 0)
            return true;

        return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
