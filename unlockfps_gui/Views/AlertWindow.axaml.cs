using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UnlockFps.Gui.Views;

public partial class AlertWindow : Window
{
    public AlertWindow()
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}