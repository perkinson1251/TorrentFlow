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

                App.StartupArgs = desktop.Args?.ToList() ?? new List<string>();
                
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
                    if (!mainWindow.IsVisible) mainWindow.Show();
                    mainWindow.Show();
                    mainWindow.LoadFile(filePath, "", true);
                });
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TorrentManagerService>();
            services.AddSingleton<SettingsService>(provider => 
                new SettingsService(provider.GetRequiredService<TorrentManagerService>())
            );

            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<AddTorrentDialog>();
        }

        public static T GetService<T>() where T : class
        {
            if (((App)Current)._serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider is not initialized yet.");
            }
            return ((App)Current)._serviceProvider.GetRequiredService<T>();
        }

        private void TrayShow()
        {
            if (_mainWindow != null)
            {
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.Show();
                }
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        private async void OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            if (_serviceProvider == null) return; 
            var torrentManager = _serviceProvider.GetService<TorrentManagerService>();
            if (torrentManager != null)
            {
                await torrentManager.SaveAllTorrentsStateAsync();
            }
            
            if (_mainWindow is MainWindow mainWindow)
            {
                await mainWindow.SaveSessionAsync();
            }
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