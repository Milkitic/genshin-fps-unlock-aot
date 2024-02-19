using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using UnlockFps.Utils;
using Windows.Win32.System.Threading;

using static Windows.Win32.PInvoke;
using PROCESS_INFORMATION = Windows.Win32.System.Threading.PROCESS_INFORMATION;

namespace UnlockFps.Services;

[SupportedOSPlatform("windows5.1.2600")]
public class ProcessService
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(ProcessService));

    private readonly Config _config;
    private int _lastProcessId;

    public ProcessService(ConfigService configService)
    {
        _config = configService.Config;
    }

    public void Start()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Only windows or wine is supported.");
        }

        var runningProcess = Process.GetProcesses()
            .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNames, x.ProcessName) != -1);

        if (runningProcess is not null)
        {
            throw new Exception("An instance of the game is already running: " + runningProcess.Id);
        }

        var launchOptions = _config.LaunchOptions;
        using var disposable = CreateProcessRaw(launchOptions, out var lpProcessInformation);

        if (!ProcessUtils.InjectDlls(lpProcessInformation.hProcess, launchOptions.DllList))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
        }

        if (launchOptions.SuspendLoad)
        {
            var retCode = ResumeThread(lpProcessInformation.hThread);
            if (retCode == 0xFFFFFFFF)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"ResumeThread failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            }
        }

        _lastProcessId = (int)lpProcessInformation.dwProcessId;
    }

    private static unsafe IDisposable CreateProcessRaw(LaunchOptions launchOptions, out PROCESS_INFORMATION lpProcessInformation)
    {
        var lpCurrentDirectory = Path.GetDirectoryName(launchOptions.GamePath);
        var commandLine = BuildCommandLine(launchOptions);
        var lpStartupInfo = new STARTUPINFOW();
        var dwCreationFlags = launchOptions.SuspendLoad ? PROCESS_CREATION_FLAGS.CREATE_SUSPENDED : default;

        var array = ArrayPool<char>.Shared.Rent(commandLine.Length + 1);
        try
        {
            var lpCommandLine = new Span<char>(array, 0, commandLine.Length + 1);
            commandLine.CopyTo(lpCommandLine);
            lpCommandLine[^1] = '\0';

            if (!CreateProcess(launchOptions.GamePath, ref lpCommandLine,
                    default, default, false,
                    dwCreationFlags, default, lpCurrentDirectory,
                    in lpStartupInfo, out lpProcessInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"CreateProcess failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(array);
        }

        return new ThreadGuard(lpProcessInformation.hThread);
    }

    private static string BuildCommandLine(LaunchOptions launchOptions)
    {
        var commandLine = new StringBuilder($"{launchOptions.GamePath} ");
        if (launchOptions.IsWindowBorderless)
        {
            commandLine.Append("-popupwindow ");
        }

        if (launchOptions.UseCustomResolution)
        {
            commandLine.Append(
                $"-screen-width {launchOptions.CustomResolutionX} -screen-height {launchOptions.CustomResolutionY} ");
        }

        commandLine.Append($"-screen-fullscreen {(launchOptions.Fullscreen ? 1 : 0)} ");
        if (launchOptions.Fullscreen)
        {
            commandLine.Append($"-window-mode {(launchOptions.IsExclusiveFullscreen ? "exclusive" : "borderless")} ");
        }

        if (launchOptions.UseMobileUI)
        {
            commandLine.Append("use_mobile_platform -is_cloud 1 -platform_type CLOUD_THIRD_PARTY_MOBILE ");
        }

        commandLine.Append($"-monitor {launchOptions.MonitorId} ");
        return commandLine.ToString();
    }

    public void KillLastProcess()
    {
        try
        {
            var process = Process.GetProcessById(_lastProcessId);
            if (Array.IndexOf(GameConstants.GameNames, process.ProcessName) != -1)
            {
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Kill process failed");
        }
    }
}