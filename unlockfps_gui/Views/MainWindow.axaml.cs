using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using UnlockFps.Gui.Model;
using UnlockFps.Gui.Services;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ICommand? _launchGameCommand;

        public int MinimumFps { get; set; } = 1;
        public int MaximumFps { get; set; } = 420;
        public Config Config { get; set; } = null!;

        public ICommand OpenInitializationWindowCommand { get; } = ReactiveCommand.CreateFromTask(ShowWindow<InitializationWindow>);
        public ICommand OpenSettingsWindowCommand { get; } = ReactiveCommand.CreateFromTask(ShowWindow<SettingsWindow>);
        public ICommand OpenAboutWindowCommand { get; } = ReactiveCommand.CreateFromTask(ShowWindow<AboutWindow>);

        public static async Task ShowWindow<T>() where T : Window
        {
            var window = App.DefaultServices.GetRequiredService<T>();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            await window.ShowDialog(MainWindow);
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
        private readonly ProcessService _processService;

#if DEBUG
        public MainWindow()
        {
            if (!Design.IsDesignMode) throw new InvalidOperationException();
            InitializeComponent();
        }
#endif

        public MainWindow(ConfigService configService, ProcessService processService)
        {
            this.SetSystemChrome();
            DataContext = _viewModel = new MainWindowViewModel();
            _configService = configService;
            _processService = processService;

            _viewModel.Config = configService.Config;
            InitializeComponent();
        }

        private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
        {
            _configService.Save();
        }

        private async void BtnLaunchGame_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!File.Exists(_viewModel.Config.GamePath))
            {
                await MainWindowViewModel.ShowWindow<InitializationWindow>();
            }

            if (await _processService.StartAsync())
            {
                WindowState = WindowState.Minimized;
            }
        }
    }
}