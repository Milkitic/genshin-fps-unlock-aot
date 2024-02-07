using System.Runtime.InteropServices;

namespace UnlockFps;

internal static partial class UnsafeMethods
{
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, Span<byte> lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, Span<byte> lpBuffer, int nSize, out int lpNumberOfBytesRead);

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);
}