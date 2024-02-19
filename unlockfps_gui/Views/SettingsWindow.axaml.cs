using System;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;
using UnlockFps.Services;

namespace UnlockFps.Gui.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private ICommand? _addDllCommand;
        private ICommand? _removeDllCommand;

        public required SettingsWindow Window { get; init; }
        public required Config Config { get; init; }

        public string? SelectedDll { get; set; }

        public ICommand AddDllCommand => _addDllCommand ??= ReactiveCommand.CreateFromTask(async () =>
        {
            var selectedFiles = await Window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter =
                [
                    new FilePickerFileType("DLL (*.dll)") { Patterns = ["*.dll"] },
                    new FilePickerFileType("All files (*.*)") { Patterns = ["*.*"] },
                ],
                AllowMultiple = true
            });

            foreach (var selectedFile in selectedFiles)
            {
                var localPath = selectedFile.Path.LocalPath;
                if (!VerifyDll(localPath))
                {
                    var alertWindow = App.DefaultServices.GetRequiredService<AlertWindow>();
                    alertWindow.Text =
                        $"""
                         Invalid File: 
                         {localPath}
                         
                         Only native x64 dlls are supported.
                         """;
                    await alertWindow.ShowDialog(Window);
                }
                else
                {
                    Config.LaunchOptions.DllList.Add(localPath);
                }
            }
        });

        public ICommand RemoveDllCommand => _removeDllCommand ??= ReactiveCommand.Create(() =>
        {
            if (SelectedDll != null)
            {
                Config.LaunchOptions.DllList.Remove(SelectedDll);
            }
        });

        private static bool VerifyDll(string fullPath)
        {
            if (!File.Exists(fullPath))
                return false;

            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            using var peReader = new PEReader(fs);
            if (peReader.HasMetadata)
                return false;

            return peReader.PEHeaders.CoffHeader.Machine == Machine.Amd64;
        }
    }
}

namespace UnlockFps.Gui.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigService? _configService;
        private readonly SettingsWindowViewModel _viewModel;

        public SettingsWindow(ConfigService configService)
        {
            this.SetSystemChrome();
            _configService = configService;
            _viewModel = new SettingsWindowViewModel
            {
                Config = _configService.Config,
                Window = this,
            };
            DataContext = _viewModel;
            InitializeComponent();
        }

#if DEBUG
        public SettingsWindow()
        {
            if (!Design.IsDesignMode) throw new InvalidOperationException();
            InitializeComponent();
        }
#endif

        private void Control_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_configService != null)
            {
                _configService.Config.PropertyChanged += Config_PropertyChanged;
                _configService.Config.LaunchOptions.PropertyChanged += Config_PropertyChanged;
                _configService.Config.LaunchOptions.DllList.CollectionChanged += DllList_CollectionChanged;
            }
        }

        private void Control_OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_configService != null)
            {
                _configService.Config.PropertyChanged -= Config_PropertyChanged;
                _configService.Config.LaunchOptions.PropertyChanged -= Config_PropertyChanged;
                _configService.Config.LaunchOptions.DllList.CollectionChanged -= DllList_CollectionChanged;
            }
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _configService?.Save();
        }

        private void DllList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _configService?.Save();
        }
    }
}