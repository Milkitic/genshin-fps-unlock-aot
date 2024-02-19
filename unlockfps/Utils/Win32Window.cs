using System.Buffers;
using System.Diagnostics;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace UnlockFps.Utils;

[SupportedOSPlatform("windows5.0")]
public class Win32Window
{
    private readonly HWND _hWnd;
    private string? _className;
    private string? _title;
    private string? _processName;
    private uint _pid;

    public Win32Window(nint handle)
    {
        _hWnd = (HWND)handle;
    }

    public nint Handle => _hWnd;

    public string ClassName => _className ??= CallWin32ToGetPWSTR(512, (p, l) => PInvoke.GetClassName(_hWnd, p, l));

    public string Title => _title ??= CallWin32ToGetPWSTR(512, (p, l) => PInvoke.GetWindowText(_hWnd, p, l));

    public uint ProcessId => _pid is 0 ? (_pid = GetProcessIdCore()) : _pid;
    
    //public string ProcessName => _processName ??= Process.GetProcessById((int)ProcessId).ProcessName;
    public unsafe string ProcessName
    {
        get
        {
            if (_processName == null)
            {
                var hProcess =
                    PInvoke.OpenProcess(
                        PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION |
                        PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE | PROCESS_ACCESS_RIGHTS.PROCESS_SYNCHRONIZE, false,
                        ProcessId);
                try
                {
                    uint bufferSize = 512;
                    Span<char> span = stackalloc char[(int)bufferSize];

                    fixed (char* o = span)
                    {
                        if (!PInvoke.QueryFullProcessImageName(hProcess, 0, new PWSTR(o), &bufferSize))
                        {
                            return "";
                        }
                    }

                    var path = new string(span.Slice(0, (int)bufferSize));
                    var processName = Path.GetFileNameWithoutExtension(path);
                    _processName = processName;
                }
                finally
                {
                    PInvoke.CloseHandle(hProcess);
                }
            }

            return _processName;
        }
    }

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