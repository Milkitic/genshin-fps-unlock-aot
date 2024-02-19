using System.Collections.ObjectModel;
using System.ComponentModel;

namespace UnlockFps;

public partial class LaunchOptions : INotifyPropertyChanged
{
    public string? GamePath { get; set; }

    public bool IsWindowBorderless { get; set; }
    public bool Fullscreen { get; set; } = true;
    public bool IsExclusiveFullscreen { get; set; }
    public bool UseCustomResolution { get; set; }
    public int CustomResolutionX { get; set; } = 1920;
    public int CustomResolutionY { get; set; } = 1080;
    public bool UseMobileUI { get; set; }

    public int MonitorId { get; set; } = 1;

    public bool SuspendLoad { get; set; }
    public ObservableCollection<string> DllList { get; set; } = new();
}

public partial class Config : INotifyPropertyChanged
{
    public LaunchOptions LaunchOptions { get; set; } = new();

    public bool AutoLaunch { get; set; }
    public bool AutoClose { get; set; }
    public bool UsePowerSave { get; set; }
    public int FpsTarget { get; set; } = 120;
    public int FpsPowerSave { get; set; } = 10;
    public int ProcessPriority { get; set; } = 3;
    public bool ShowDebugConsole { get; set; }
    public bool WindowQueryUseEvent { get; set; } = true;
}