using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using UnlockFps.Gui.Services;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui;

public partial class App : Application
{
    public static ServiceProvider DefaultServices { get; private set; } = null!;

    public static Window? CurrentMainWindow =>
        (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

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
        services.AddSingleton<ProcessService>();
        DefaultServices = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            if (!Program.DuplicatedInstance)
            {
                desktop.MainWindow = DefaultServices.GetRequiredService<MainWindow>();
            }
            else
            {
                var alertWindow = DefaultServices.GetRequiredService<AlertWindow>();
                alertWindow.Text = "Another unlocker is already running.";
                desktop.MainWindow = alertWindow;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception.InnerExceptions.Count == 1)
        {
            Console.WriteLine("Unobserved task exception: " + e.Exception.InnerException);
        }
        else
        {
            Console.WriteLine("Unobserved task exception: " + e.Exception);
        }
    }
}