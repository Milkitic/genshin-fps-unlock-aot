using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace UnlockFps;

internal static class FpsPatterns
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(FpsPatterns));
  
    public static unsafe nint ProvideAddress(ProcessModule mdUnityPlayer, ProcessModule mdUserAssembly, Process process)
    {
        var unityPlayerPath = mdUnityPlayer.FileName;
        var userAssemblyPath = mdUserAssembly.FileName;

        using ModuleGuard shUnityPlayer = NativeMethods.LoadLibraryEx(unityPlayerPath, IntPtr.Zero, 0x20);
        using ModuleGuard shUserAssembly = NativeMethods.LoadLibraryEx(userAssemblyPath, IntPtr.Zero, 0x20);

        var pUnityPlayer = shUnityPlayer.BaseAddress;
        var pUserAssembly = shUserAssembly.BaseAddress;

        var dosHeader = Marshal.PtrToStructure<IMAGE_DOS_HEADER>(pUnityPlayer);
        var ntHeader =
            Marshal.PtrToStructure<IMAGE_NT_HEADERS>((nint)(pUnityPlayer.ToInt64() + dosHeader.e_lfanew));

        if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 3.7
        {
            Logger.LogDebug($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}, <3.7");
            var addressPtr = ProcessUtils.PatternScan(pUnityPlayer, "7F 0F 8B 05 ?? ?? ?? ??");
            byte* address = (byte*)addressPtr;
            if (address == null) throw new Exception("Unrecognized FPS pattern.");

            Logger.LogDebug($"Scanned pattern successfully: 0x{addressPtr:X16}");
            byte* rip = address + 2;
            int rel = *(int*)(rip + 2);
            var localVa = rip + rel + 6;
            var rva = localVa - pUnityPlayer.ToInt64();
            return (nint)(pUnityPlayer.ToInt64() + rva);
        }
        else
        {
            byte* rip = null;
            if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 4.3
            {
                Logger.LogDebug($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}, <4.3");
                var addressPtr =
                    ProcessUtils.PatternScan(pUserAssembly, "E8 ?? ?? ?? ?? 85 C0 7E 07 E8 ?? ?? ?? ?? EB 05");
                byte* address = (byte*)addressPtr;
                if (address == null) throw new Exception("Unrecognized FPS pattern.");

                Logger.LogDebug($"Scanned pattern successfully: 0x{addressPtr:X16}");
                rip = address;
                rip += *(int*)(rip + 1) + 5;
                rip += *(int*)(rip + 3) + 7;
            }
            else
            {
                Logger.LogDebug($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}");
                var addressPtr = ProcessUtils.PatternScan(pUserAssembly, "B9 3C 00 00 00 FF 15");
                byte* address = (byte*)addressPtr;
                if (address == null) throw new Exception("Unrecognized FPS pattern.");

                Logger.LogDebug($"Scanned pattern successfully: 0x{addressPtr:X16}");
                rip = address;
                rip += 5;
                rip += *(int*)(rip + 2) + 6;
            }

            byte* remoteVa = rip - pUserAssembly.ToInt64() + mdUserAssembly.BaseAddress.ToInt64();
            byte* dataPtr = null;

            Span<byte> readResult = stackalloc byte[8];
            while (dataPtr == null)
            {
                NativeMethods.ReadProcessMemory(process.Handle, (IntPtr)remoteVa, readResult, readResult.Length, out _);
                ulong value = BitConverter.ToUInt64(readResult);
                dataPtr = (byte*)value;
            }

            byte* localVa = dataPtr - mdUnityPlayer.BaseAddress.ToInt64() + pUnityPlayer.ToInt64();
            while (localVa[0] == 0xE8 || localVa[0] == 0xE9)
                localVa += *(int*)(localVa + 1) + 5;

            localVa += *(int*)(localVa + 2) + 6;
            var rva = localVa - pUnityPlayer.ToInt64();
            return (IntPtr)(mdUnityPlayer.BaseAddress.ToInt64() + rva);
        }

    }
}

internal class LogUtils
{
    public static ILoggerFactory LoggerFactory { get; set; } = new NullLoggerFactory();

    public static ILogger GetLogger(string name)
    {
        return LoggerFactory.CreateLogger(name);
    }

    public static ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }

}

[SupportedOSPlatform("windows5.0")]
public class Win32Window
{
    private readonly HWND _hWnd;
    private string? _className;
    private string? _title;
    private string? _processName;
    private uint _pid;

    internal Win32Window(nint handle)
    {
        _hWnd = (HWND)handle;
    }

    public nint Handle => _hWnd;

    public string ClassName => _className ??= CallWin32ToGetPWSTR(512, (p, l) => PInvoke.GetClassName(_hWnd, p, l));

    public string Title => _title ??= CallWin32ToGetPWSTR(512, (p, l) => PInvoke.GetWindowText(_hWnd, p, l));

    public uint ProcessId => _pid is 0 ? (_pid = GetProcessIdCore()) : _pid;

    public string ProcessName => _processName ??= Process.GetProcessById((int)ProcessId).ProcessName;

    private unsafe uint GetProcessIdCore()
    {
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(_hWnd, &pid);
        return pid;
    }

    private unsafe string CallWin32ToGetPWSTR(int bufferLength, Func<PWSTR, int, int> getter)
    {
        var buffer = ArrayPool<char>.Shared.Rent(bufferLength);
        try
        {
            fixed (char* ptr = buffer)
            {
                getter(ptr, bufferLength);
                return new string(ptr);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}