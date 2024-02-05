using System.Collections.ObjectModel;
using System.ComponentModel;
using UnlockFps.Gui.Utils;

namespace UnlockFps.Gui.Model;

public partial class Config : INotifyPropertyChanged
{
    public string? GamePath { get; set; }

    public bool AutoStart { get; set; }
    public bool AutoClose { get; set; }
    public bool PopupWindow { get; set; }
    public bool Fullscreen { get; set; } = true;
    public bool UseCustomRes { get; set; }
    public bool IsExclusiveFullscreen { get; set; }
    public bool StartMinimized { get; set; }
    public bool UsePowerSave { get; set; }
    public bool SuspendLoad { get; set; }
    public bool UseMobileUI { get; set; }

    public int FPSTarget { get; set; } = 120;
    public int CustomResX { get; set; } = 1920;
    public int CustomResY { get; set; } = 1080;
    public int MonitorNum { get; set; } = 1;
    public int Priority { get; set; } = 3;

    public bool ShowDebugConsole { get; set; }

    public ObservableCollection<string> DllList { get; set; } = new();

    public Config()
    {
        this.PropertyChanged += Config_PropertyChanged;
    }

    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShowDebugConsole)) return;
        try
        {
            if (ShowDebugConsole)
            {
                ConsoleManager.Show();
            }
            else
            {
                ConsoleManager.Hide();
            }
        }
        catch
        {
            // ignored
        }
    }
}