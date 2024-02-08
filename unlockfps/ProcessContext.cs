using System.Diagnostics;

namespace UnlockFps;

public class ProcessContext : IDisposable
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