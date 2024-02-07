using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace UnlockFps;

public class ProcessScanner
{
    public event Action<string, bool>? Logged;

    private readonly Config _config;
    private CancellationTokenSource? _cts;

    public ProcessScanner(Config config)
    {
        _config = config;
    }

    public ProcessContext? ProcessContext { get; private set; }
    public void Start()
    {
        //STARTUPINFO si = new();
        //uint creationFlag = _config.SuspendLoad ? 4u : 0u;
        //var gameFolder = Path.GetDirectoryName(_config.GamePath);

        //if (!Native.CreateProcess(_config.GamePath, "", IntPtr.Zero, IntPtr.Zero, false, creationFlag,
        //        IntPtr.Zero, gameFolder, ref si, out var pi))
        //{
        //    throw new Win32Exception(Marshal.GetLastWin32Error(),
        //        $"CreateProcess failed. ({Marshal.GetLastPInvokeErrorMessage()})");
        //}
        ////var currentProcess = Process.Start(path);

        //ProcessContext = new ProcessContext()
        //{
        //    CurrentProcess = Process.GetProcessById(pi.dwProcessId),
        //    ProcessHandle = pi.hProcess,
        //    DirectoryName = gameFolder,
        //    FileName = _config.GamePath
        //};
        //var success = await GetProcessModulesAsync(ProcessContext, CancellationToken.None);
        //SetupData(ProcessContext);

        _cts = new CancellationTokenSource();
        Task.Factory.StartNew(() =>
        {
            Run(_cts.Token);
        }, TaskCreationOptions.LongRunning);
    }

    public void Run(CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                CheckProcessContextAsync(token);
                if (!TaskUtils.TaskSleep(100, token)) return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (!TaskUtils.TaskSleep(1000, token)) return;
            }

        }
    }

    private void CheckProcessContextAsync(CancellationToken token)
    {
        if (ProcessContext == null)
        {
            var processes = GameConstants.GameNames.SelectMany(Process.GetProcessesByName);
            foreach (var process in processes)
            {
                if (!CheckProcessPath(process, out var fileName, out var directoryName)) continue;
                if (process.HasExited) continue;

                ProcessContext = new ProcessContext
                {
                    ProcessHandle = process.Handle,
                    CurrentProcess = process,
                    FileName = fileName,
                    DirectoryName = directoryName
                };

                try
                {
                    var success = GetProcessModules(ProcessContext, CancellationToken.None);
                    if (success)
                    {
                        SetupData(ProcessContext);
                        break;
                    }
                }
                catch
                {
                    ProcessContext = null;
                    throw;
                }
            }
        }
        else
        {
            WaitGameWindow(ProcessContext, token);
            while (!ProcessContext.CurrentProcess.HasExited && !token.IsCancellationRequested)
            {
                ApplyFpsLimit(ProcessContext);
                if (!TaskUtils.TaskSleep(200, token)) return;
            }

            ProcessContext.Dispose();
            ProcessContext = null;
        }
    }

    private static bool CheckProcessPath(Process process,
        [NotNullWhen(true)] out string? fileName,
        [NotNullWhen(true)] out string? directoryName)
    {
        if (process.MainModule != null)
        {
            fileName = process.MainModule.FileName;
            directoryName = Path.GetDirectoryName(fileName)!;
            if (File.Exists(Path.Combine(directoryName, "UnityPlayer.dll")))
            {
                return true;
            }

            return false;
        }

        fileName = null;
        directoryName = null;
        return false;
    }

    private bool GetProcessModules(ProcessContext processContext, CancellationToken token)
    {
        int retryCount = 0;

        Logged?.Invoke($"Trying to get remote module base address...", false);
        while (processContext.CurrentProcess is { HasExited: false } currentProcess && !token.IsCancellationRequested)
        {
            currentProcess.Refresh();
            var modules = currentProcess.Modules.Cast<ProcessModule>()
                .Where(k => k.ModuleName is "UnityPlayer.dll" or "UserAssembly.dll");

            foreach (var processModule in modules)
            {
                if (processModule.ModuleName is "UnityPlayer.dll")
                {
                    processContext.UnityPlayerModule = processModule;
                }
                else if (processModule.ModuleName is "UserAssembly.dll")
                {
                    processContext.UserAssemblyModule = processModule;
                }
            }

            if (processContext is { UnityPlayerModule: not null, UserAssemblyModule: not null })
            {
                break;
            }

            if (retryCount > 40)
            {
                break;
            }

            if (!TaskUtils.TaskSleep(500, token)) break;
            retryCount++;
            Logged?.Invoke($"({retryCount}) Trying to get remote module base address...", false);
        }

        return processContext is { UnityPlayerModule: not null, UserAssemblyModule: not null };
    }

    private unsafe void SetupData(ProcessContext processContext)
    {
        var gameDir = processContext.DirectoryName;
        var gameName = Path.GetFileNameWithoutExtension(processContext.FileName);
        var dataDir = Path.Combine(gameDir, $"{gameName}_Data");

        var unityPlayerPath = Path.Combine(gameDir, "UnityPlayer.dll");
        var userAssemblyPath = Path.Combine(dataDir, "Native", "UserAssembly.dll");

        using ModuleGuard pUnityPlayer = UnsafeMethods.LoadLibraryEx(unityPlayerPath, IntPtr.Zero, 32);
        using ModuleGuard pUserAssembly = UnsafeMethods.LoadLibraryEx(userAssemblyPath, IntPtr.Zero, 32);

        if (!pUnityPlayer.LoadSuccess || !pUserAssembly.LoadSuccess)
        {
            throw new Exception("Failed to load UnityPlayer.dll or UserAssembly.dll");
        }

        var dosHeader = Marshal.PtrToStructure<IMAGE_DOS_HEADER>(pUnityPlayer);
        var ntHeader =
            Marshal.PtrToStructure<IMAGE_NT_HEADERS>((nint)(pUnityPlayer.BaseAddress.ToInt64() + dosHeader.e_lfanew));

        if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 3.7
        {
            Logged?.Invoke($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}, <3.7", false);
            var addressPtr = ProcessUtils.PatternScan(pUnityPlayer, "7F 0F 8B 05 ?? ?? ?? ??");
            byte* address = (byte*)addressPtr;
            if (address == null) throw new Exception("Unrecognized FPS pattern.");

            Logged?.Invoke($"Scanned pattern successfully: 0x{addressPtr:X16}", false);
            byte* rip = address + 2;
            int rel = *(int*)(rip + 2);
            var localVa = rip + rel + 6;
            var rva = localVa - pUnityPlayer.BaseAddress.ToInt64();
            processContext.FpsValueAddress = (IntPtr)(pUnityPlayer.BaseAddress.ToInt64() + rva);
        }
        else
        {
            byte* rip = null;
            if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 4.3
            {
                Logged?.Invoke($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}, <4.3", false);
                var addressPtr = ProcessUtils.PatternScan(pUserAssembly, "E8 ?? ?? ?? ?? 85 C0 7E 07 E8 ?? ?? ?? ?? EB 05");
                byte* address = (byte*)addressPtr;
                if (address == null) throw new Exception("Unrecognized FPS pattern.");

                Logged?.Invoke($"Scanned pattern successfully: 0x{addressPtr:X16}", false);
                rip = address;
                rip += *(int*)(rip + 1) + 5;
                rip += *(int*)(rip + 3) + 7;
            }
            else
            {
                Logged?.Invoke($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}", false);
                var addressPtr = ProcessUtils.PatternScan(pUserAssembly, "B9 3C 00 00 00 FF 15");
                byte* address = (byte*)addressPtr;
                if (address == null) throw new Exception("Unrecognized FPS pattern.");

                Logged?.Invoke($"Scanned pattern successfully: 0x{addressPtr:X16}", false);
                rip = address;
                rip += 5;
                rip += *(int*)(rip + 2) + 6;
            }

            byte* remoteVa = rip - pUserAssembly.BaseAddress.ToInt64() + processContext.UserAssemblyModule.BaseAddress.ToInt64();
            byte* dataPtr = null;

            Span<byte> readResult = stackalloc byte[8];
            while (dataPtr == null)
            {
                UnsafeMethods.ReadProcessMemory(processContext.ProcessHandle, (IntPtr)remoteVa, readResult, readResult.Length, out _);

                ulong value = BitConverter.ToUInt64(readResult);
                dataPtr = (byte*)value;
            }

            byte* localVa = dataPtr - processContext.UnityPlayerModule.BaseAddress.ToInt64() + pUnityPlayer.BaseAddress.ToInt64();
            while (localVa[0] == 0xE8 || localVa[0] == 0xE9)
                localVa += *(int*)(localVa + 1) + 5;

            localVa += *(int*)(localVa + 2) + 6;
            var rva = localVa - pUnityPlayer.BaseAddress.ToInt64();
            processContext.FpsValueAddress = (IntPtr)(processContext.UnityPlayerModule.BaseAddress.ToInt64() + rva);
        }

        Logged?.Invoke($"Get FPS address successfully: 0x{processContext.FpsValueAddress:X16}", false);
    }

    private void WaitGameWindow(ProcessContext processContext, CancellationToken token)
    {
        while (processContext.CurrentProcess.MainWindowHandle == IntPtr.Zero && !token.IsCancellationRequested)
        {
            if (!TaskUtils.TaskSleep(200, token)) return;
        }
    }

    private void ApplyFpsLimit(ProcessContext context)
    {
        int fpsTarget;
        if (UnsafeMethods.GetForegroundWindow() == context.CurrentProcess.MainWindowHandle)
        {
            fpsTarget = _config.FpsTarget;
        }
        else
        {
            fpsTarget = _config.UsePowerSave ? _config.FpsPowerSave : _config.FpsTarget;
        }

        Span<byte> buffer = stackalloc byte[4];
        var readProcessMemory = UnsafeMethods.ReadProcessMemory(context.ProcessHandle, context.FpsValueAddress, buffer, 4, out var readBytes);
        if (!readProcessMemory || readBytes != 4) return;

        var currentFps = BitConverter.ToInt32(buffer);
        if (currentFps == fpsTarget) return;

        var toWrite = BitConverter.GetBytes(fpsTarget);
        if (UnsafeMethods.WriteProcessMemory(context.ProcessHandle, context.FpsValueAddress, toWrite, 4, out _))
        {
            Console.WriteLine($"FPS Change: {currentFps} -> {fpsTarget}");
        }
    }
}