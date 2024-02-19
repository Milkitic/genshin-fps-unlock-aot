using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Milki.Extensions.Threading;
using UnlockFps.Logging;
using UnlockFps.Utils;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using static Windows.Win32.PInvoke;

namespace UnlockFps.Services;

[SupportedOSPlatform("windows5.0")]
public class GameInstanceService : IDisposable, INotifyPropertyChanged
{
    public event Action<Process>? ProcessExit;

    private static readonly ILogger Logger = LogManager.GetLogger(nameof(GameInstanceService));

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

    private SynchronizationContext? _hwndSynchronizationContext;
    private readonly SynchronizationContext _synchronizationContext = new SingleSynchronizationContext("WinEventHook Callback");

    private WINEVENTPROC _eventCallBack;
    private Timer _timer;
    private bool _isRunning;
    private ProcessContext? _context;

    public GameInstanceService(ConfigService configService)
    {
        _config = configService.Config;
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetField(ref _isRunning, value);
    }

    internal ProcessContext? Context
    {
        get => _context;
        private set
        {
            _context = value;
            IsRunning = value != null;
        }
    }

    // https://blog.walterlv.com/post/monitor-foreground-window-on-windows
    public void Start()
    {
        if (_winEventHook != default) return;

        if (SynchronizationContext.Current == null)
        {
            _hwndSynchronizationContext ??= new SingleSynchronizationContext("HWND SynchronizationContext", true);
            SynchronizationContext.SetSynchronizationContext(_hwndSynchronizationContext);
            _hwndSynchronizationContext.Post(_ =>
            {
                Thread.CurrentThread.Name = "HWND SynchronizationContext";
            }, null);
        }

        SynchronizationContext.Current!.Post(a =>
        {
            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "Default SynchronizationContext";
            }

            if (!WineHelper.DetectWine(out _, out _) && _config.WindowQueryUseEvent)
            {
                Logger.LogInformation($"[{Thread.CurrentThread.Name}] Attempting to find game window (Event Mode)");

                _eventCallBack = WinEventProc;
                _winEventHook = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    HMODULE.Null,
                    _eventCallBack,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS
                );
                if (_hwndSynchronizationContext == null) return;
                if (GetMessage(out var lpMsg, default, default, default))
                {
                    TranslateMessage(in lpMsg);
                    DispatchMessage(in lpMsg);
                }
            }
            else
            {
                Logger.LogInformation($"[{Thread.CurrentThread.Name}] Attempting to find game window (Timer Mode)");

                nint lastWindow = 0;
                _timer = new Timer(_ =>
                {
                    var foregroundWindow = GetForegroundWindow();
                    if (lastWindow != foregroundWindow)
                    {
                        var win32Window = new Win32Window(foregroundWindow);
                        _synchronizationContext.Send(CallBack, win32Window);
                        lastWindow = foregroundWindow;
                    }
                }, null, 300, 300);
            }
        }, null);

        var win32Window = new Win32Window(GetForegroundWindow());
        _synchronizationContext.Send(CallBack, win32Window);
    }

    public void Stop()
    {
        if (_hwndSynchronizationContext != null)
        {
            _hwndSynchronizationContext?.Post(_ =>
            {
                if (!_winEventHook.IsNull && UnhookWinEvent(_winEventHook))
                {
                    _winEventHook = default;
                }
            }, null);
        }
        else
        {
            if (!_winEventHook.IsNull && UnhookWinEvent(_winEventHook))
            {
                _winEventHook = default;
            }
        }

        _eventCallBack = default;
        _timer?.Dispose();
        Context?.CancellationTokenSource.Cancel();
    }

    private void WinEventProc(HWINEVENTHOOK hWinEventHook, uint @event, HWND hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (Context != null) return;
        var win32Window = new Win32Window(hwnd);
        _synchronizationContext.Send(CallBack, win32Window);
    }

    private void CallBack(object? state)
    {
        try
        {
            if (state is not Win32Window win32Window) return;
            ApplyContext(win32Window);
        }
        catch (Exception ex)
        {
            Console.WriteLine("WinEventHook Callback Error:" + ex);
            throw;
        }
    }

    private void ApplyContext(Win32Window win32Window)
    {
        if (Context == null)
        {
            if (win32Window.ProcessId == 0)
            {
                Logger.LogDebug($"Invalid window: {win32Window.Handle}");
                return;
            }

            var text =
                $"[0x{win32Window.Handle:X16} {win32Window.ClassName}] ({win32Window.ProcessId} {win32Window.ProcessName}.exe) {win32Window.Title}";
            var process = Process.GetProcessById((int)win32Window.ProcessId);
            if (!CheckProcess(process, out var processContext))
            {
                Logger.LogDebug($"Invalid window: {text}");
                return;
            }

            processContext.Win32Window = win32Window;

            Logger.LogInformation($"Find the game window: {text}");
            Logger.LogInformation("Start applying FPS.");

            Task.Factory.StartNew(() =>
            {
                processContext.IsFpsApplied = true;
                ApplyFpsLoop(processContext.CancellationTokenSource.Token);
                Context?.Dispose();
            }, TaskCreationOptions.LongRunning);
        }
        else
        {
            Context.IsGameInForeground = Context.CurrentProcess.Id == win32Window.ProcessId;

            if (_config.UsePowerSave)
            {
                Context.CurrentProcess.PriorityClass = Context.IsGameInForeground
                    ? PriorityClass[_config.ProcessPriority]
                    : ProcessPriorityClass.Idle;
            }
        }
    }

    private void ApplyFpsLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (Context is not { CurrentProcess.HasExited: false } processContext) break;

            ApplyFpsLimit(processContext);
            if (!TaskUtils.TaskSleep(200, token)) return;
        }

        if (Context != null)
        {
            if (Context.CurrentProcess.HasExited && Context.Win32Window != null)
            {
                Logger.LogInformation($"Process exit: {Context.Win32Window.ProcessName}");
            }

            ProcessExit?.Invoke(Context.CurrentProcess);
            Context.Dispose();
            Context = null;
        }

        Logger.LogInformation("Stop applying FPS.");
    }

    private bool CheckProcess(Process process, [NotNullWhen(true)] out ProcessContext? processContext)
    {
        processContext = null;
        if (!CheckProcessPath(process, out var fileName, out var directoryName)) return false;
        if (process.HasExited) return false;

        Context = processContext = new ProcessContext
        {
            CurrentProcess = process,
            FileName = fileName,
            DirectoryName = directoryName
        };
        try
        {
            Logger.LogInformation($"Trying to get remote module base address...");
            var success = GetProcessModules(Context, CancellationToken.None);
            if (!success) return false;
            Logger.LogInformation($"Get remote module base address successfully.");

            Logger.LogInformation($"Trying to get FPS address...");
            processContext.FpsValueAddress = FpsPatterns.ProvideAddress(Context.UnityPlayerModule,
                processContext.UserAssemblyModule, process);
            Logger.LogInformation($"Get FPS address successfully: {processContext.FpsValueAddress}");
            return true;
        }
        catch
        {
            Context = processContext = null;
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
            Logger.LogDebug($"({retryCount}) Trying to get remote module base address...");
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

    internal class ProcessContext : IDisposable
    {
        public ProcessContext()
        {
            CancellationTokenSource = new CancellationTokenSource();
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public required Process CurrentProcess { get; init; }
        //public required IntPtr ProcessHandle { get; init; }
        public required string FileName { get; init; }
        public required string DirectoryName { get; init; }

        public ProcessModule UnityPlayerModule { get; set; } = null!;
        public ProcessModule UserAssemblyModule { get; set; } = null!;
        public bool IsFpsApplied { get; set; }

        public IntPtr FpsValueAddress { get; set; }
        public bool IsGameInForeground { get; set; }

        public Win32Window? Win32Window { get; set; }

        public void Dispose()
        {
            CancellationTokenSource.Dispose();
            CurrentProcess?.Dispose();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}