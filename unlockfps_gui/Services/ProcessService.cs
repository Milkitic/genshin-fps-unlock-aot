using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnlockFps.Gui.Model;
using UnlockFps.Gui.Utils;
using UnlockFps.Gui.ViewModels;

namespace UnlockFps.Gui.Services;

public class ProcessService : ViewModelBase, IDisposable
{
    private static Native.WinEventProc _eventCallback;

    private static uint[] PriorityClass =
    {
        0x00000100,
        0x00000080,
        0x00008000,
        0x00000020,
        0x00004000,
        0x00000040
    };

    private IntPtr? _winEventHook;
    private GCHandle _pinnedCallback;
    private bool _gameInForeground = true;

    private IntPtr _gameHandle = IntPtr.Zero;
    private IntPtr _remoteUnityPlayer = IntPtr.Zero;
    private IntPtr _remoteUserAssembly = IntPtr.Zero;
    private int _gamePid = 0;

    private IntPtr _pFpsValue = IntPtr.Zero;

    private readonly Config _config;

    private readonly byte[] _fpsReadBuffer = new byte[4];
    private bool _isRunning;

    public ProcessService(ConfigService configService)
    {
        _config = configService.Config;

        _eventCallback = WinEventProc;
        _pinnedCallback = GCHandle.Alloc(_eventCallback, GCHandleType.Normal);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetField(ref _isRunning, value);
    }

    public async ValueTask StartAsync(Action<string, bool>? onPreparingLogging)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Only windows or wine is supported.");
        }

        _winEventHook ??= Native.SetWinEventHook(
            3, // EVENT_SYSTEM_FOREGROUND
            3, // EVENT_SYSTEM_FOREGROUND
            IntPtr.Zero,
            _eventCallback,
            0,
            0,
            0 // WINEVENT_OUTOFCONTEXT
        );

        if (IsGameRunning())
        {
            throw new Exception("An instance of the game is already running.");
        }

        IsRunning = true;
        var cts = new CancellationTokenSource();
        try
        {
            await PrepareAsync(onPreparingLogging, cts);
        }
        catch (Exception e)
        {
            IsRunning = false;
            throw;
        }

        BackgroundWorker(cts);
    }

    private async Task PrepareAsync(Action<string, bool>? onPreparingLogging, CancellationTokenSource cts)
    {
        Process.GetProcesses()
            .ToList()
            .Where(x => x.ProcessName is "GenshinImpact" or "YuanShen")
            .ToList()
            .ForEach(x => x.Kill());

        STARTUPINFO si = new();
        uint creationFlag = _config.SuspendLoad ? 4u : 0u;
        var gameFolder = Path.GetDirectoryName(_config.GamePath);

        if (!Native.CreateProcess(_config.GamePath, BuildCommandLine(), IntPtr.Zero, IntPtr.Zero, false, creationFlag,
                IntPtr.Zero, gameFolder, ref si, out var pi))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"CreateProcess failed. ({Marshal.GetLastPInvokeErrorMessage()})");
        }

        onPreparingLogging?.Invoke($"Created process. hProcess: {pi.hProcess}; hThread: {pi.hThread}", false);

        if (!ProcessUtils.InjectDlls(pi.hProcess, _config.DllList))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
        }

        if (_config.SuspendLoad)
        {
            var retCode = Native.ResumeThread(pi.hThread);
            if (retCode != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"CreateProcess failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            }
        }

        _gamePid = pi.dwProcessId;
        _gameHandle = pi.hProcess;

        if (!Native.CloseHandle(pi.hThread))
        {
            onPreparingLogging?.Invoke($"CloseHandle failed ({Marshal.GetLastWin32Error()})\r\n" +
                                       $"{Marshal.GetLastPInvokeErrorMessage()}", true);
        }

        await UpdateRemoteModules(onPreparingLogging, cts.Token);
        SetupData(onPreparingLogging);
        await WaitGamingWindow(onPreparingLogging, cts.Token);
    }

    private async void BackgroundWorker(CancellationTokenSource cts)
    {
        try
        {
            while (IsGameRunning() && !cts.Token.IsCancellationRequested)
            {
                ApplyFpsLimit();
                await Task.Delay(200, cts.Token);
            }

            await Console.Out.WriteLineAsync("Game exited.");
            await cts.CancelAsync();
            cts.Dispose();
        }
        finally
        {
            IsRunning = false;
            Native.CloseHandle(_gameHandle);
        }
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType != 3)
            return;

        Native.GetWindowThreadProcessId(hWnd, out var pid);
        _gameInForeground = pid == _gamePid;

        ApplyFpsLimit();

        if (!_config.UsePowerSave)
            return;

        uint targetPriority = _gameInForeground ? PriorityClass[_config.Priority] : 0x00000040;
        Native.SetPriorityClass(_gameHandle, targetPriority);
    }

    private bool IsGameRunning()
    {
        if (_gameHandle == IntPtr.Zero)
            return false;

        Native.GetExitCodeProcess(_gameHandle, out var exitCode);
        return exitCode == 259;
    }

    private void ApplyFpsLimit()
    {
        int fpsTarget = _gameInForeground ? _config.FPSTarget : _config.UsePowerSave ? 10 : _config.FPSTarget;
        if (Native.ReadProcessMemory(_gameHandle, _pFpsValue, _fpsReadBuffer, 4, out var readBytes))
        {
            var currentFps = BitConverter.ToInt32(_fpsReadBuffer, 0);
            if (currentFps != fpsTarget)
            {
                var toWrite = BitConverter.GetBytes(fpsTarget);
                Native.WriteProcessMemory(_gameHandle, _pFpsValue, toWrite, 4, out _);
                Console.WriteLine($"FPS Change: {currentFps} -> {fpsTarget}");
            }
        }
    }

    private string BuildCommandLine()
    {
        string commandLine = $"{_config.GamePath} ";
        if (_config.PopupWindow)
            commandLine += "-popupwindow ";

        if (_config.UseCustomRes)
            commandLine += $"-screen-width {_config.CustomResX} -screen-height {_config.CustomResY} ";

        commandLine += $"-screen-fullscreen {(_config.Fullscreen ? 1 : 0)} ";
        if (_config.Fullscreen)
            commandLine += $"-window-mode {(_config.IsExclusiveFullscreen ? "exclusive" : "borderless")} ";

        if (_config.UseMobileUI)
            commandLine += "use_mobile_platform -is_cloud 1 -platform_type CLOUD_THIRD_PARTY_MOBILE ";

        commandLine += $"-monitor {_config.MonitorNum} ";
        return commandLine;
    }

    private unsafe void SetupData(Action<string, bool>? onPreparingLogging)
    {
        var gameDir = Path.GetDirectoryName(_config.GamePath);
        var gameName = Path.GetFileNameWithoutExtension(_config.GamePath);
        var dataDir = Path.Combine(gameDir, $"{gameName}_Data");

        var unityPlayerPath = Path.Combine(gameDir, "UnityPlayer.dll");
        var userAssemblyPath = Path.Combine(dataDir, "Native", "UserAssembly.dll");

        using ModuleGuard pUnityPlayer = Native.LoadLibraryEx(unityPlayerPath, IntPtr.Zero, 32);
        using ModuleGuard pUserAssembly = Native.LoadLibraryEx(userAssemblyPath, IntPtr.Zero, 32);

        if (!pUnityPlayer || !pUserAssembly)
        {
            throw new Exception("Failed to load UnityPlayer.dll or UserAssembly.dll");
        }

        var dosHeader = Marshal.PtrToStructure<IMAGE_DOS_HEADER>(pUnityPlayer);
        var ntHeader =
            Marshal.PtrToStructure<IMAGE_NT_HEADERS>((IntPtr)(pUnityPlayer.BaseAddress.ToInt64() + dosHeader.e_lfanew));

        if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 3.7
        {
            onPreparingLogging?.Invoke($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}, <3.7", false);
            byte* address = (byte*)ProcessUtils.PatternScan(pUnityPlayer, "7F 0F 8B 05 ? ? ? ?");
            if (address == null) throw new Exception("Unrecognized FPS pattern.");

            onPreparingLogging?.Invoke($"Scanned pattern successfully: {address->ToString()}", false);
            byte* rip = address + 2;
            int rel = *(int*)(rip + 2);
            var localVa = rip + rel + 6;
            var rva = localVa - pUnityPlayer.BaseAddress.ToInt64();
            _pFpsValue = (IntPtr)(pUnityPlayer.BaseAddress.ToInt64() + rva);
        }
        else
        {
            byte* rip = null;
            if (ntHeader.FileHeader.TimeDateStamp < 0x656FFAF7U) // < 4.3
            {
                onPreparingLogging?.Invoke($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}, <4.3", false);
                byte* address =
                    (byte*)ProcessUtils.PatternScan(pUserAssembly, "E8 ? ? ? ? 85 C0 7E 07 E8 ? ? ? ? EB 05");
                if (address == null) throw new Exception("Unrecognized FPS pattern.");

                onPreparingLogging?.Invoke($"Scanned pattern successfully: {address->ToString()}", false);
                rip = address;
                rip += *(int*)(rip + 1) + 5;
                rip += *(int*)(rip + 3) + 7;
            }
            else
            {
                onPreparingLogging?.Invoke($"TimeDateStamp: {ntHeader.FileHeader.TimeDateStamp}", false);
                byte* address = (byte*)ProcessUtils.PatternScan(pUserAssembly, "B9 3C 00 00 00 FF 15");
                if (address == null) throw new Exception("Unrecognized FPS pattern.");

                onPreparingLogging?.Invoke($"Scanned pattern successfully: {address->ToString()}", false);
                rip = address;
                rip += 5;
                rip += *(int*)(rip + 2) + 6;
            }

            byte* remoteVa = rip - pUserAssembly.BaseAddress.ToInt64() + _remoteUserAssembly.ToInt64();
            byte* dataPtr = null;

            while (dataPtr == null)
            {
                byte[] readResult = new byte[8];
                Native.ReadProcessMemory(_gameHandle, (IntPtr)remoteVa, readResult, readResult.Length, out _);

                ulong value = BitConverter.ToUInt64(readResult, 0);
                dataPtr = (byte*)value;
            }

            byte* localVa = dataPtr - _remoteUnityPlayer.ToInt64() + pUnityPlayer.BaseAddress.ToInt64();
            while (localVa[0] == 0xE8 || localVa[0] == 0xE9)
                localVa += *(int*)(localVa + 1) + 5;

            localVa += *(int*)(localVa + 2) + 6;
            var rva = localVa - pUnityPlayer.BaseAddress.ToInt64();
            _pFpsValue = (IntPtr)(_remoteUnityPlayer.ToInt64() + rva);
        }

        onPreparingLogging?.Invoke($"Get FPS address successfully: {_pFpsValue}", false);
    }

    private async ValueTask UpdateRemoteModules(Action<string, bool>? onPreparingLogging, CancellationToken token)
    {
        int retries = 0;

        onPreparingLogging?.Invoke($"Trying to get remote module base address...", false);
        while (IsGameRunning() && !token.IsCancellationRequested)
        {
            _remoteUnityPlayer = ProcessUtils.GetModuleBase(_gameHandle, "UnityPlayer.dll");
            _remoteUserAssembly = ProcessUtils.GetModuleBase(_gameHandle, "UserAssembly.dll");

            if (_remoteUnityPlayer != IntPtr.Zero && _remoteUserAssembly != IntPtr.Zero)
                break;

            if (retries > 40)
                break;

            await Task.Delay(500, token);
            retries++;
            onPreparingLogging?.Invoke($"({retries}) Trying to get remote module base address...", false);
        }

        if (_remoteUnityPlayer == IntPtr.Zero || _remoteUserAssembly == IntPtr.Zero)
        {
            throw new Exception("Failed to get remote module base address");
        }
    }

    private async Task WaitGamingWindow(Action<string, bool>? onPreparingLogging, CancellationToken token)
    {
        onPreparingLogging?.Invoke("Waiting game window to open...", false);
        var processId = Process.GetProcessById(_gamePid);
        while (processId.MainWindowHandle == IntPtr.Zero && !token.IsCancellationRequested)
        {
            await Task.Delay(200, token);
        }
    }

    private void ReleaseUnmanagedResources()
    {
        _pinnedCallback.Free();
        if (_winEventHook is { } handle) Native.UnhookWinEvent(handle);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~ProcessService()
    {
        ReleaseUnmanagedResources();
    }
}