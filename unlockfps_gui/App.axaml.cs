using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using UnlockFps.Gui.Service;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui;

public partial class App : Application
{
    public static ServiceProvider DefaultServices { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void RegisterServices()
    {
        base.RegisterServices();

        var services = new ServiceCollection();
        services.AddTransient<AboutWindow>();
        services.AddTransient<AlertWindow>();
        services.AddTransient<InitializationWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<ConfigService>();
        DefaultServices = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!Program.DuplicatedInstance)
            {
                desktop.MainWindow = DefaultServices.GetRequiredService<MainWindow>();
            }
            else
            {
                desktop.MainWindow = DefaultServices.GetRequiredService<AlertWindow>();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}