using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UnlockFps.Gui.Views;

public partial class AlertWindow : Window
{
    public static readonly StyledProperty<bool> IsErrorProperty =
        AvaloniaProperty.Register<AlertWindow, bool>(nameof(IsError), true);

    public bool IsError
    {
        get => GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AlertWindow, string>(nameof(Text), "Info");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public AlertWindow()
    {
        this.SetSystemChrome();
        DataContext = this;
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}