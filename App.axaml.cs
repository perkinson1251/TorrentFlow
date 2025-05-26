using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TorrentFlow.Services;

namespace TorrentFlow
{
    public partial class App : Application
    {        
        private IServiceProvider _serviceProvider;
        private SettingsService _settingsService;
        private Window _mainWindow;
        public ICommand ExitCommand { get; }
        public ICommand ShowCommand { get; }
        public static List<string> StartupArgs { get; private set; }

        public App()
        {
            ExitCommand = new RelayCommand(TrayExit);
            ShowCommand = new RelayCommand(TrayShow);
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                desktop.MainWindow = _mainWindow;

                _settingsService = _serviceProvider.GetRequiredService<SettingsService>();
                _settingsService.UpdateTray(this);

                App.StartupArgs = desktop.Args.ToList();
                
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.Exit += OnExit;
            }

            base.OnFrameworkInitializationCompleted();
        }

        public static void OnFileOpened(string filePath)
        {
            if (Current is App app && app._mainWindow is MainWindow mainWindow)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.Show();
                    mainWindow.LoadFile(filePath, "", true);
                });
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TorrentManagerService>();
            services.AddSingleton<SettingsService>();

            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<AddTorrentDialog>();
        }

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._serviceProvider.GetRequiredService<T>();
        }

        private void TrayShow()
        {
            if (_mainWindow != null)
            {
                if (!_mainWindow.IsVisible || _mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                }
                _mainWindow.Activate();
            }
        }

        private async void OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            var torrentManager = _serviceProvider.GetRequiredService<TorrentManagerService>();
            await torrentManager.SaveAllTorrentsStateAsync();
            await ((MainWindow)_mainWindow).SaveSessionAsync();
        }

        public void TrayExit()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }
}