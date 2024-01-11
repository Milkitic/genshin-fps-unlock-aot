using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using UnlockFps.Gui.Model;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public int MinimumFps { get; set; } = 1;
    public int MaximumFps { get; set; } = 420;
    public Config Config { get; set; } = null!;

    public ICommand OpenInitializationWindowCommand { get; } = ReactiveCommand.Create(() =>
    {
        var window = App.DefaultServices.GetRequiredService<InitializationWindow>();
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog(((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!)
            .MainWindow!);
    });

    public ICommand OpenSettingsWindowCommand { get; } = ReactiveCommand.Create(() =>
    {
        var window = App.DefaultServices.GetRequiredService<SettingsWindow>();
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog(((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!)
            .MainWindow!);
    });

    public ICommand OpenAboutWindowCommand { get; } = ReactiveCommand.Create(() =>
    {
        var window = App.DefaultServices.GetRequiredService<AboutWindow>();
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog(((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!)
            .MainWindow!);
    });
}