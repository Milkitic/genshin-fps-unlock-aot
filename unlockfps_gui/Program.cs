using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using UnlockFps.Gui.Utils;

namespace UnlockFps.Gui;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using (new Mutex(true, @"GenshinFPSUnlocker", out var createdNew))
        {
            ConsoleManager.Show();
            DuplicatedInstance = !createdNew;
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
    }

    public static bool DuplicatedInstance { get; private set; }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var appBuilder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithNativeFonts()
            .LogToTrace()
            .UseReactiveUI();
        if (WineHelper.DetectWine(out _, out _))
        {
            return appBuilder
                .With(new Win32PlatformOptions
                {
                    CompositionMode = [Win32CompositionMode.RedirectionSurface],
                    RenderingMode = [Win32RenderingMode.Software],
                    OverlayPopups = true
                });
        }
        else
        {
            return appBuilder;
        }
    }
}