using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using UnlockFps.Gui.Model;
using UnlockFps.Gui.Services;
using UnlockFps.Gui.Utils;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;

namespace UnlockFps.Gui.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public required ProcessService ProcessService { get; init; }
        public required Config Config { get; init; }
        public int MinimumFps { get; set; } = 1;
        public int MaximumFps { get; set; } = 420;
        public string? PreparingLog { get; set; }

        public ICommand OpenInitializationWindowCommand { get; } =
            ReactiveCommand.CreateFromTask(ShowWindow<InitializationWindow>);

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
        private readonly TrayIcon _trayIcon;

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
            DataContext = _viewModel = new MainWindowViewModel()
            {
                Config = configService.Config,
                ProcessService = processService,
            };
            _configService = configService;
            _processService = processService;
            InitializeComponent();

            if (WineHelper.DetectWine(out var version, out var buildId))
            {
                Title += $" (Wine {version})";
            }
            else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Title += $" ({Environment.OSVersion})";
            }

            _trayIcon = TrayIcon.GetIcons(Application.Current!)![0];
            _trayIcon.Clicked += (_, _) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    Show();
                    WindowState = WindowState.Normal;
                }
            };
            if (_trayIcon.Menu is { } menu)
            {
                var items = menu.Items
                    .Where(k => k is not NativeMenuItemSeparator)
                    .OfType<NativeMenuItem>()
                    .ToArray();
                items[0].Click += (_, _) =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                };
                items[1].Click += (_, _) => Close();
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property != WindowStateProperty) return;
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                _trayIcon.IsVisible = true;
                _trayIcon.ToolTipText = $"{Title} (FPS: {_viewModel.Config.FPSTarget})";
            }
            else
            {
                _trayIcon.IsVisible = false;
            }
        }

        private async void Window_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (Design.IsDesignMode) return;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConsoleManager.BindExitAction(() =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("User manually closes debug window. Program will now exit.");
                    Console.ResetColor();
                    Thread.Sleep(1000);
                    Dispatcher.UIThread.Invoke(Close);
                });
            }

            if (_viewModel.Config.AutoStart)
            {
                await LaunchGame(true);
            }
        }

        private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
        {
            _configService.Save();
        }

        private void Window_OnClosed(object? sender, EventArgs e)
        {
            try
            {
                ConsoleManager.Hide();
            }
            catch
            {
                // ignored
            }
        }

        private async void BtnLaunchGame_OnClick(object? sender, RoutedEventArgs e)
        {
            await LaunchGame(false);
        }

        private async Task LaunchGame(bool isAutoStart)
        {
            if (!File.Exists(_viewModel.Config.GamePath))
            {
                if (isAutoStart) return;
                await MainWindowViewModel.ShowWindow<InitializationWindow>();
            }

            if (!File.Exists(_viewModel.Config.GamePath)) return;

            try
            {
                await _processService.StartAsync((message, isError) =>
                {
                    _viewModel.PreparingLog = message;
                    if (isError)
                        Console.Error.WriteLine(message);
                    else
                        Console.WriteLine(message);
                });
                _viewModel.ProcessService.PropertyChanged += ProcessServiceOnPropertyChanged;
                _viewModel.PreparingLog = null;
                WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                await ShowErrorMessage(ex.Message);
                _viewModel.PreparingLog = null;
            }

            return;

            void ProcessServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                _viewModel.ProcessService.PropertyChanged -= ProcessServiceOnPropertyChanged;
                if (_viewModel.Config.AutoClose)
                {
                    Close();
                }
                else
                {
                    Show();
                    WindowState = WindowState.Normal;
                }
            }
        }

        private static async Task ShowErrorMessage(string infoWindowText)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var infoWindow = App.DefaultServices.GetRequiredService<AlertWindow>();
                infoWindow.Text = infoWindowText;
                await infoWindow.ShowDialog(App.CurrentMainWindow!);
            });
        }
    }
}
