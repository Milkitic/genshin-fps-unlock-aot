using System;
using Avalonia.Controls;
using UnlockFps.Gui.Service;
using UnlockFps.Gui.ViewModels;

namespace UnlockFps.Gui.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ConfigService _configService;

#if DEBUG
    public MainWindow()
    {
        if (!Design.IsDesignMode) throw new InvalidOperationException();
        InitializeComponent();
    }
#endif

    public MainWindow(ConfigService configService)
    {
        DataContext = _viewModel = new MainWindowViewModel();
        _configService = configService;

        _viewModel.Config = configService.Config;
        InitializeComponent();
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _configService.Save();
    }
}