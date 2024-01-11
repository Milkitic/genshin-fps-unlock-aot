using System;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;

namespace UnlockFps.Gui;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"GenshinFPSUnlocker", out var createdNew);
#if !DEBUG
        if (!createdNew)
        {
            return;
        }
#endif

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithNativeFonts()
            .LogToTrace()
            .UseReactiveUI();
}