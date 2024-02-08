using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Threading;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using static Windows.Win32.PInvoke;

namespace UnlockFps;

[SupportedOSPlatform("windows5.0")]
public class FpsOverrideDaemon : IDisposable
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(FpsOverrideDaemon));

    private readonly Config _config;

    private HWINEVENTHOOK _winEventHook;

    private static readonly ProcessPriorityClass[] PriorityClass =
    [
        ProcessPriorityClass.RealTime,
        ProcessPriorityClass.High,
        ProcessPriorityClass.AboveNormal,
        ProcessPriorityClass.Normal,
        ProcessPriorityClass.BelowNormal,
        ProcessPriorityClass.Idle
    ];

    private readonly SynchronizationContext _synchronizationContext = new SingleSynchronizationContext("WinEventHook Callback");

    public FpsOverrideDaemon(Config config)
    {
        _config = config;
    }

    public ProcessContext? ProcessContext { get; private set; }

    // https://blog.walterlv.com/post/monitor-foreground-window-on-windows
    public void Start(bool enableMessageLoop = false)
    {
        if (_winEventHook != default)
        {
            return;
        }

        Logger.LogInformation("Attempting to find game window...");
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            HMODULE.Null,
            WinEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS
        );

        if (enableMessageLoop && GetMessage(out var lpMsg, default, default, default))
        {
            TranslateMessage(in lpMsg);
            DispatchMessage(in lpMsg);
        }

        var win32Window = new Win32Window(GetForegroundWindow());
        _synchronizationContext.Send(CallBack, win32Window);
    }

    public void Stop()
    {
        if (!_winEventHook.IsNull && UnhookWinEvent(_winEventHook))
        {
            _winEventHook = default;
        }

        ProcessContext?.CancellationTokenSource.Cancel();
    }

    private void WinEventProc(HWINEVENTHOOK hWinEventHook, uint @event, HWND hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (ProcessContext != null) return;
        var win32Window = new Win32Window(hwnd);
        _synchronizationContext.Send(CallBack, win32Window);
    }

    private void CallBack(object? state)
    {
        if (state is not Win32Window win32Window) return;

        if (ProcessContext == null)
        {
            var process = Process.GetProcessById((int)win32Window.ProcessId);
            if (!CheckProcess(process, out var processContext)) return;
            processContext.Win32Window = win32Window;

            var text = $"[0x{win32Window.Handle:X16} {win32Window.ClassName}] ({win32Window.ProcessId} {win32Window.ProcessName}.exe) {win32Window.Title}";
            Logger.LogInformation($"Find the game window: {text}");
            Logger.LogInformation("Start applying FPS.");

            Task.Factory.StartNew(() =>
            {
                processContext.IsFpsApplied = true;
                LoopApply(processContext.CancellationTokenSource.Token);
                ProcessContext?.Dispose();
            }, TaskCreationOptions.LongRunning);
        }
        else
        {
            ProcessContext.IsGameInForeground = ProcessContext.CurrentProcess.Id == win32Window.ProcessId;

            if (_config.UsePowerSave)
            {
                ProcessContext.CurrentProcess.PriorityClass = ProcessContext.IsGameInForeground
                    ? PriorityClass[_config.ProcessPriority]
                    : ProcessPriorityClass.Idle;
            }
        }
    }

    private void LoopApply(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (ProcessContext is not { CurrentProcess.HasExited: false } processContext) break;

            ApplyFpsLimit(processContext);
            if (!TaskUtils.TaskSleep(200, token)) return;
        }

        if (ProcessContext != null)
        {
            if (ProcessContext.CurrentProcess.HasExited && ProcessContext.Win32Window != null)
            {
                Logger.LogInformation($"Process exit: {ProcessContext.Win32Window.ProcessName}");
            }

            ProcessContext.Dispose();
            ProcessContext = null;
        }

        Logger.LogInformation("Stop applying FPS.");
    }

    private bool CheckProcess(Process process, [NotNullWhen(true)] out ProcessContext? processContext)
    {
        processContext = null;
        if (!CheckProcessPath(process, out var fileName, out var directoryName)) return false;
        if (process.HasExited) return false;

        ProcessContext = processContext = new ProcessContext
        {
            CurrentProcess = process,
            FileName = fileName,
            DirectoryName = directoryName
        };
        try
        {
            Logger.LogInformation($"Trying to get remote module base address...");
            var success = GetProcessModules(ProcessContext, CancellationToken.None);
            if (!success) return false;
            Logger.LogInformation($"Get remote module base address successfully.");

            Logger.LogInformation($"Trying to get FPS address...");
            processContext.FpsValueAddress = FpsPatterns.ProvideAddress(ProcessContext.UnityPlayerModule,
                processContext.UserAssemblyModule, process);
            Logger.LogInformation($"Get FPS address successfully: {processContext.FpsValueAddress}");
            return true;
        }
        catch
        {
            ProcessContext = processContext = null;
            throw;
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
            Logger.LogDebug($"({retryCount}) Trying to get remote module base address...", false);
        }

        return processContext is { UnityPlayerModule: not null, UserAssemblyModule: not null };
    }

    private void ApplyFpsLimit(ProcessContext context)
    {
        int fpsTarget;
        if (GetForegroundWindow() == context.CurrentProcess.MainWindowHandle)
        {
            fpsTarget = _config.FpsTarget;
        }
        else
        {
            fpsTarget = _config.UsePowerSave ? _config.FpsPowerSave : _config.FpsTarget;
        }

        Span<byte> buffer = stackalloc byte[4];
        var readProcessMemory = NativeMethods.ReadProcessMemory(context.CurrentProcess.Handle, context.FpsValueAddress,
            buffer, 4, out var readBytes);
        if (!readProcessMemory || readBytes != 4) return;

        var currentFps = BitConverter.ToInt32(buffer);
        if (currentFps == fpsTarget) return;

        var toWrite = BitConverter.GetBytes(fpsTarget);
        if (NativeMethods.WriteProcessMemory(context.CurrentProcess.Handle, context.FpsValueAddress, toWrite, 4, out _))
        {
            Logger.LogInformation($"FPS Override: {currentFps} -> {fpsTarget}");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}