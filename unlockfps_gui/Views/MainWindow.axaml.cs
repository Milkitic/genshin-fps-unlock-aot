using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using UnlockFps.Gui.Model;
using UnlockFps.Gui.Service;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public int MinimumFps { get; set; } = 1;
        public int MaximumFps { get; set; } = 420;
        public Config Config { get; set; } = null!;

        public ICommand OpenInitializationWindowCommand { get; } = ReactiveCommand.Create(ShowWindow<InitializationWindow>);
        public ICommand OpenSettingsWindowCommand { get; } = ReactiveCommand.Create(ShowWindow<SettingsWindow>);
        public ICommand OpenAboutWindowCommand { get; } = ReactiveCommand.Create(ShowWindow<AboutWindow>);

        private static void ShowWindow<T>() where T : Window
        {
            var window = App.DefaultServices.GetRequiredService<T>();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog(MainWindow);
        }

        private static Window MainWindow =>
            ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
    }
}

namespace UnlockFps.Gui.Views
{
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
}