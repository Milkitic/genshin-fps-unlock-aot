using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;

namespace UnlockFps.Gui.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        this.SetSystemChrome();
        InitializeComponent();
    }

    private void HyperLink_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock { Text: { } text })
        {
            Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
        }
    }
}