using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!Program.DuplicatedInstance)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
            else
            {
                desktop.MainWindow = new AlertWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}