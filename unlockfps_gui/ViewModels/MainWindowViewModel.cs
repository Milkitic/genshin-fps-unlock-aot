using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _fps = 120;
    public int MinimumFps { get; set; } = 1;
    public int MaximumFps { get; set; } = 420;

    public int Fps
    {
        get => _fps;
        set => SetField(ref _fps, value);
    }

    public ICommand OpenInitializationWindowCommand { get; } = ReactiveCommand.Create(() =>
    {
        var window = new InitializationWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        window.ShowDialog(((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!)
            .MainWindow!);
    });

    public ICommand OpenSettingsWindowCommand { get; } = ReactiveCommand.Create(() =>
    {
        var window = new SettingsWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        window.ShowDialog(((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!)
            .MainWindow!);
    });

    public ICommand OpenAboutWindowCommand { get; } = ReactiveCommand.Create(() =>
    {
        var window = new AboutWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        window.ShowDialog(((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!)
            .MainWindow!);
    });
}