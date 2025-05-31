using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
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

namespace TorrentFlow;

public partial class MainWindow : Window
{
    private readonly TorrentManagerService _torrentManager;
    private string _sessionDirectory = AppDomain.CurrentDomain.BaseDirectory + "session.json";
    private DispatcherTimer _updateTimer;

    public MainWindowViewModel ViewModel { get; }

    public MainWindow(TorrentManagerService torrentManagerService)
    {
        InitializeComponent();
        _torrentManager = torrentManagerService;
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _updateTimer.Tick += async (sender, args) => await UpdateTorrentVisualsAndSaveState(); 
        _updateTimer.Start();

        LoadSessionAsync();

        Closing += MainWindow_Closing;
        Opened += MainWindow_Opened;
    }

    private async Task UpdateTorrentVisualsAndSaveState()
    {
        ViewModel.UpdateAllTorrentVisuals();
        await _torrentManager.SaveAllTorrentsStateAsync();
        await SaveSessionAsync();
    }
    
    private async void MainWindow_Opened(object sender, EventArgs e)
    {
        await Task.Delay(250);

        foreach (var arg in App.StartupArgs)
        {
            if (arg.StartsWith("magnetic", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Loading magnetic link: " + arg);
                await LoadFile(arg, "", true);
            }

            if (arg.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Loading torrent: " + arg);
                await LoadFile(arg, "", true);
            }
        }
    }

    private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                await PrepareForShutdown(); 
                desktopLifetime.Shutdown();
            }
        }
    }
    
    private async Task PrepareForShutdown()
    {
        if (_torrentManager != null)
        {
            await _torrentManager.SaveAllTorrentsStateAsync();
        }
        await SaveSessionAsync();
    }


    public async Task LoadFile(string filePath, string savePath, bool startOnAdd)
    {
        Torrent torrent = null;
        var tempFilePath = "";

        if (filePath.StartsWith("magnet"))
        {
            tempFilePath = filePath.Replace("%", "");
            var data = await _torrentManager.LoadMagneticLinkMetadata(tempFilePath);
            torrent = Torrent.Load(data);
        }
        else if (filePath.EndsWith(".torrent"))
        {
            if (!File.Exists(filePath))
                return;

            var tempDirectory = AppDomain.CurrentDomain.BaseDirectory + "tmp";
            if (!Directory.Exists(tempDirectory)) Directory.CreateDirectory(tempDirectory);
            var fileName = Path.GetFileName(filePath);
            tempFilePath = Path.Combine(tempDirectory, fileName);

            if (!File.Exists(tempFilePath))
                try
                {
                    File.Copy(filePath, tempFilePath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error copying file: {ex.Message}");
                    return;
                }
            
            torrent = Torrent.Load(tempFilePath);

            if (ViewModel.Torrents.Any(t => t.Name == torrent.Name))
            {
                ShowWarning($"The torrent \"{torrent.Name}\" is already downloading.");
                return;
            }
        }

        if (string.IsNullOrEmpty(savePath))
        {
            var addTorrentDialog = new AddTorrentDialog(torrent.Name);
            savePath = await addTorrentDialog.ShowDialog<string>(this);
        }

        if (!string.IsNullOrWhiteSpace(savePath))
            try
            {
                var torrentManager = await _torrentManager.StartTorrentAsync(torrent, savePath, startOnAdd);
                
                if (!ViewModel.Torrents.Any(t => t.Name == torrent.Name))
                {
                    var torrentView = new TorrentView(torrentManager) { FileName = tempFilePath };
                    ViewModel.AddTorrent(torrentView);
                    Console.WriteLine($"Added torrent to UI: {torrent.Name}");
                }
                else
                {
                    Console.WriteLine($"Torrent already exists in UI: {torrent.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading torrent {torrent?.Name}: {ex.Message}");
                ShowWarning($"Error loading torrent: {ex.Message}");
            }
    }

    public async void ShowWarning(string message)
    {
        var messageBox = MessageBoxManager
            .GetMessageBoxCustom(new MessageBoxCustomParams
            {
                ContentTitle = "Warning",
                ContentMessage = message,
                ButtonDefinitions = new[] { new ButtonDefinition { Name = "OK" } }
            });
        await messageBox.ShowWindowDialogAsync(this);
    }

    private async void AddTorrentButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a Torrent File",
            Filters = new List<FileDialogFilter> { new() { Name = "Torrent Files", Extensions = new List<string> { "torrent" } } }
        };
        var result = await dialog.ShowAsync(this);
        if (result != null && result.Length > 0)
        {
            await LoadFile(result[0], "", true);
        }
    }

    private async void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TorrentView torrent)
            await _torrentManager.ResumeTorrentAsync(torrent.Name);
    }

    private async void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TorrentView torrent)
            await _torrentManager.PauseTorrentAsync(torrent.Name);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TorrentView torrent)
        {
            ViewModel.RemoveTorrent(torrent);

            if (torrent.FileName.EndsWith(".torrent"))
                try
                {
                    File.Delete(Path.Combine(torrent.FileName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not delete torrent file {torrent.FileName}: {ex.Message}");
                }
            
            await _torrentManager.DeleteTorrentAsync(torrent.Name, true);
        }
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Process.Start("explorer.exe", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", path);
        }
    }

    public async Task SaveSessionAsync()
    {
        try
        {
            var session = new SessionData();
            foreach (var kvp in ViewModel.Torrents)
                session.Torrents.Add(new TorrentSessionItem
                {
                    TorrentFile = kvp.FileName, SavePath = kvp.SavePath, State = kvp.Status
                });
            var json = System.Text.Json.JsonSerializer.Serialize(session);
            await File.WriteAllTextAsync(_sessionDirectory, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving session: {ex.Message}");
        }
    }

    private async Task LoadSessionAsync()
    {
        try
        {
            if (File.Exists(_sessionDirectory))
            {
                var json = await File.ReadAllTextAsync(_sessionDirectory);
                var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(json);
                if (session?.Torrents != null)
                {
                    Console.WriteLine($"Loading {session.Torrents.Count} torrents from session");
                    foreach (var item in session.Torrents)
                        try
                        {
                            await LoadFile(item.TorrentFile, item.SavePath, item.State == TorrentState.Downloading);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading torrent from session {item.TorrentFile}: {ex.Message}");
                        }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading session: {ex.Message}");
        }
    }
}