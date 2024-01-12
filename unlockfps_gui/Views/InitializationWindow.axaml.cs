using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Win32;
using UnlockFps.Gui.Model;
using UnlockFps.Gui.Service;
using UnlockFps.Gui.ViewModels;

namespace UnlockFps.Gui.ViewModels
{
    public class InitializationWindowViewModel : ViewModelBase
    {
        public required Config Config { get; init; }
        public SearchStatus SearchStatus { get; set; }
        public ObservableCollection<string> InstallationPaths { get; } = new();
        public string? SelectedInstallationPath { get; set; }
    }

    public enum SearchStatus
    {
        Searching, NotFound, HasResult
    }
}

namespace UnlockFps.Gui.Views
{
    public partial class InitializationWindow : Window
    {
        private readonly ConfigService _configService;
        private readonly InitializationWindowViewModel _viewModel;

        private CancellationTokenSource? _cts;

#if DEBUG
        public InitializationWindow()
        {
            if (!Design.IsDesignMode) throw new InvalidOperationException();
            InitializeComponent();
        }
#endif

        public InitializationWindow(ConfigService configService)
        {
            this.SetSystemChrome();
            _configService = configService;
            DataContext = _viewModel = new InitializationWindowViewModel()
            {
                Config = _configService.Config,
                SelectedInstallationPath = configService.Config.GamePath
            };

            InitializeComponent();
        }

        private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            await Task.Run(() => SearchRegistry(_cts.Token));
        }

        private void Control_OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_cts is { } cts)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

#pragma warning disable CA1416
        private void SearchRegistry(CancellationToken token = default)
        {
            using var uninstallKey =
                Registry.LocalMachine?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey == null) return;

            var keys = uninstallKey.GetSubKeyNames();
            var installationPaths = _viewModel.InstallationPaths;

            foreach (var key in keys)
            {
                if (key is not ("Genshin Impact" or "Ô­Éñ")) continue;

                using var subKey = uninstallKey.OpenSubKey(key);
                if (subKey == null)
                {
                    return;
                }

                var installationDir = (string?)subKey.GetValue("InstallPath");
                if (!Directory.Exists(installationDir)) continue;

                var configPath = Path.Combine(installationDir, "config.ini");
                if (!File.Exists(configPath)) continue;

                string? gamePath = null;
                string? gameName = null;
                var configLines = File.ReadLines(configPath);
                foreach (var line in configLines)
                {
                    var indexOf = line.IndexOf('=');
                    if (indexOf < 0) continue;

                    var iniKey = GetIniKey(line, indexOf);
                    if (iniKey.Equals("game_install_path", StringComparison.Ordinal))
                    {
                        gamePath = GetIniValue(line, indexOf);
                    }
                    else if (iniKey.Equals("game_start_name", StringComparison.Ordinal))
                    {
                        gameName = GetIniValue(line, indexOf);
                    }
                }

                if (gamePath == null || gameName == null) continue;

                var combine = Path.Combine(gamePath, gameName);
                if (File.Exists(combine))
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        installationPaths.Add(Path.GetFullPath(combine));
                    });
                }
            }

            var selectedPath = _viewModel.SelectedInstallationPath;
            if (installationPaths.Count > 0 && (selectedPath == null || !installationPaths.Contains(selectedPath)))
            {
                _viewModel.SelectedInstallationPath = installationPaths[0];
            }
        }

        private static string GetIniKey(string s, int indexOf)
        {
            return s.Substring(0, indexOf).Trim();
        }

        private static string GetIniValue(string s, int indexOf)
        {
            return s.Substring(indexOf + 1).Trim();
        }
#pragma warning restore CA1416
    }
}