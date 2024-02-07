using System.Runtime.InteropServices;
using System.Text;

namespace UnlockFps;

internal class ProcessUtils
{
    public static string GetProcessPathFromPid(uint pid, out IntPtr processHandle)
    {
        var hProcess = Native.OpenProcess(
            ProcessAccess.QUERY_LIMITED_INFORMATION |
            ProcessAccess.TERMINATE |
            StandardAccess.SYNCHRONIZE, false, pid);

        processHandle = hProcess;

        if (hProcess == IntPtr.Zero)
            return string.Empty;

        StringBuilder sb = new StringBuilder(1024);
        uint bufferSize = (uint)sb.Capacity;
        if (!Native.QueryFullProcessImageName(hProcess, 0, sb, ref bufferSize))
            return string.Empty;

        return sb.ToString();
    }

    public static bool InjectDlls(IntPtr processHandle, IReadOnlyList<string> dllPaths)
    {
        if (dllPaths.Count == 0)
            return true;

        Native.RtlAdjustPrivilege(20, true, false, out var _);

        var kernel32 = Native.LoadLibrary("kernel32.dll");
        var loadLibrary = Native.GetProcAddress(kernel32, "LoadLibraryW");

        var remoteVa = Native.VirtualAllocEx(processHandle, IntPtr.Zero, 0x1000,
            AllocationType.COMMIT | AllocationType.RESERVE, MemoryProtection.READWRITE);
        if (remoteVa == IntPtr.Zero)
            return false;

        foreach (var dllPath in dllPaths)
        {
            var nativeString = Marshal.StringToHGlobalUni(dllPath);
            var bytes = Encoding.Unicode.GetBytes(dllPath);
            Marshal.FreeHGlobal(nativeString);

            if (!Native.WriteProcessMemory(processHandle, remoteVa, bytes, bytes.Length, out var bytesWritten))
                return false;

            var thread = Native.CreateRemoteThread(processHandle, IntPtr.Zero, 0, loadLibrary, remoteVa, 0, out var threadId);
            if (thread == IntPtr.Zero)
                return false;

            Native.WaitForSingleObject(thread, uint.MaxValue);
            Native.CloseHandle(thread);
            Native.WriteProcessMemory(processHandle, remoteVa, new byte[bytes.Length], bytes.Length, out _);
        }

        Native.VirtualFreeEx(processHandle, remoteVa, 0, FreeType.RELEASE);

        return true;
    }

    public static unsafe IntPtr PatternScan(IntPtr module, string signature)
    {
        var dosHeader = Marshal.PtrToStructure<IMAGE_DOS_HEADER>(module);
        var ntHeader = Marshal.PtrToStructure<IMAGE_NT_HEADERS>((nint)(module.ToInt64() + dosHeader.e_lfanew));

        var sizeOfImage = ntHeader.OptionalHeader.SizeOfImage;

        using var scanner = new Reloaded.Memory.Sigscan.Scanner((byte*)module.ToPointer(), (int)sizeOfImage);

        var result = scanner.FindPattern(signature);
        if (result.Found)
        {
            return (nint)(module.ToInt64() + result.Offset);
        }

        return IntPtr.Zero;
    }

    public static IntPtr GetModuleBase(IntPtr hProcess, string moduleName)
    {
        var modules = new IntPtr[1024];

        if (!Native.EnumProcessModules(hProcess, modules, (uint)(modules.Length * IntPtr.Size), out var bytesNeeded))
        {
            if (Marshal.GetLastWin32Error() != 299)
                return IntPtr.Zero;
        }

        foreach (var module in modules.Where(x => x != IntPtr.Zero))
        {
            StringBuilder sb = new StringBuilder(1024);
            if (Native.GetModuleBaseName(hProcess, module, sb, (uint)sb.Capacity) == 0)
                continue;

            if (sb.ToString() != moduleName)
                continue;

            if (!Native.GetModuleInformation(hProcess, module, out var moduleInfo, (uint)Marshal.SizeOf<MODULEINFO>()))
                continue;

            return moduleInfo.lpBaseOfDll;
        }

        return IntPtr.Zero;
    }
}