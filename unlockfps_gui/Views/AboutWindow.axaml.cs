using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using UnlockFps.Gui.Utils;

namespace UnlockFps.Gui.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        this.SetSystemChrome();
        InitializeComponent();
        Run_Version.Text = "v" + ReflectionUtil.GetInformationalVersion();
    }

    private void HyperLink_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock { Text: { } text })
        {
            Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
        }
    }
}