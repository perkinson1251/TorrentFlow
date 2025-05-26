using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using TorrentFlow.Services;
using TorrentFlow.Data;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using MonoTorrent.Client;
using MonoTorrent;
using System.Runtime.InteropServices;
using System.Diagnostics;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;

namespace TorrentFlow
{
    public partial class MainWindow : Window
    {
        private readonly TorrentManagerService _torrentManager;
        private string _sessionDirectory = AppDomain.CurrentDomain.BaseDirectory + "session.json";
        
        private DispatcherTimer _updateTimer;
        
        public ObservableCollection<TorrentView> Torrents { get; } = new();

        public MainWindow(TorrentManagerService torrentManagerService)
        {
            InitializeComponent();
            _torrentManager = torrentManagerService;
            DataContext = this; 
            
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += (sender, args) => UpdateTorrentProgress();
            _updateTimer.Start();

            LoadSessionAsync();

            this.Closing += MainWindow_Closing;
            this.Opened += MainWindow_Opened;
        }

        private async void MainWindow_Opened(object sender, EventArgs e)
        {
            await Task.Delay(250);

            foreach (var arg in App.StartupArgs)
            {
                if (arg.StartsWith("magnetic", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Loading magnetic link: " + arg);
                    LoadFile(arg, "", true);
                }
                if (arg.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Loading torrent: " + arg);
                    LoadFile(arg, "", true);
                }
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                if(Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    desktopLifetime.Shutdown();
                }
            }
        }

        public async Task LoadFile(string filePath, string savePath, bool startOnAdd)
        {
            Torrent torrent = null;
            string tempFilePath = "";

            if (filePath.StartsWith("magnet"))
            {
                var loadingDialog = new LoadingDialog(this);
                loadingDialog.Show();

                tempFilePath = filePath.Replace("%", "");
                var data = await _torrentManager.LoadMagneticLinkMetadata(tempFilePath); 
                torrent = Torrent.Load(data);
                
                loadingDialog.Close();
            }
            else if(filePath.EndsWith(".torrent"))
            {
                if (!File.Exists(filePath))
                    return;

                string tempDirectory = AppDomain.CurrentDomain.BaseDirectory + "tmp";
                if (!Directory.Exists(tempDirectory))
                {
                    Directory.CreateDirectory(tempDirectory);
                }
                string fileName = Path.GetFileName(filePath);
                tempFilePath = Path.Combine(tempDirectory, fileName);

                if(!File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Copy(filePath, tempFilePath, overwrite: true);
                        Console.WriteLine($"File copied to {tempFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error copying file: {ex.Message}");
                        return;
                    }
                }

                torrent = Torrent.Load(tempFilePath);

                if (Torrents.Any(t => t.Name == torrent.Name))
                {
                    ShowWarning($"The torrent \"{torrent.Name}\" is already downloading.");
                    return;
                }
            }
            if(savePath == string.Empty)
            {
                var addTorrentDialog = new AddTorrentDialog(torrent.Name);
                savePath = await addTorrentDialog.ShowDialog<string>(this);
            }

            if (!string.IsNullOrWhiteSpace(savePath))
            {
                var torrentManager = await _torrentManager.StartTorrentAsync(torrent, savePath, startOnAdd);
                var torrentView = new TorrentView(torrentManager);
                torrentView.FileName = tempFilePath;
                Torrents.Add(torrentView);
            }
        }

        public async void ShowWarning(string message)
        {
            var messageBox = MessageBoxManager
                .GetMessageBoxCustom(new MessageBoxCustomParams
                {
                    ContentTitle = "Warning",
                    ContentMessage = message,
                    ButtonDefinitions = new[]
                    {
                        new ButtonDefinition { Name = "OK" }
                    }
                });

            await messageBox.ShowWindowDialogAsync(this);
        }


        private async void AddTorrentButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a Torrent File",
                Filters = new List<FileDialogFilter>
                { 
                    new FileDialogFilter { Name = "Torrent Files", Extensions = new List<string> { "torrent" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                string torrentFilePath = result[0];
                await LoadFile(torrentFilePath, "", true);
            }
        }

        private async void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is TorrentView torrent)
            {
                await _torrentManager.ResumeTorrentAsync(torrent.Name);
            }
        }

        private async void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is TorrentView torrent)
            {
                await _torrentManager.PauseTorrentAsync(torrent.Name);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is TorrentView torrent)
            {
                if (Torrents.Contains(torrent))
                    Torrents.Remove(torrent);

                if(torrent.FileName.EndsWith(".torrent"))
                {
                    File.Delete(Path.Combine(torrent.FileName));
                }

                await SaveSessionAsync();
                await _torrentManager.DeleteTorrentAsync(torrent.Name, true);
            }
        }

        private async Task UpdateTorrentProgress()
        {
            foreach (var torrent in Torrents)
            {
                torrent.UpdateProgress();
                torrent.UpdateSpeeds();
            }
            await _torrentManager.SaveAllTorrentsStateAsync();
            await SaveSessionAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = App.GetService<SettingsWindow>();
            settingsWindow.ShowDialog(this);
        }

        private void OpenDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is TorrentView torrent)
            {
                var path = torrent.SavePath;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("explorer.exe", path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", path);
                }
            }
        }
        public async Task SaveSessionAsync()
        {
            await _torrentManager.SaveAllTorrentsStateAsync();
            var session = new SessionData();
            foreach (var kvp in Torrents)
            {
                session.Torrents.Add(new TorrentSessionItem 
                { 
                    TorrentFile = kvp.FileName, 
                    SavePath = kvp.SavePath,
                    State = kvp.Status
                });
            }
            string json = System.Text.Json.JsonSerializer.Serialize(session);
            File.WriteAllText(_sessionDirectory, json);
        }

        private async Task LoadSessionAsync()
        {
            if (File.Exists(_sessionDirectory))
            {
                string json = File.ReadAllText(_sessionDirectory);
                var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(json);
                foreach (var item in session.Torrents)
                {
                    await LoadFile(item.TorrentFile,
                                    item.SavePath,
                                    startOnAdd: item.State == TorrentState.Downloading);
                }
            }
        }

    }
}