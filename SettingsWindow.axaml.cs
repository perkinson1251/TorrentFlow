using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using Avalonia;
using TorrentFlow.Services;

namespace TorrentFlow;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    public SettingsWindowViewModel ViewModel { get; }

    public SettingsWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        ViewModel = new SettingsWindowViewModel();
        DataContext = ViewModel;
        InitializeComponent();
        LoadSettingsToViewModel();
    }

    private void LoadSettingsToViewModel()
    {
        var settings = _settingsService.GetSettings();
        ViewModel.TempDefaultSaveLocation = settings.DefaultSaveLocation;
        ViewModel.TempMaxDownloadSpeedKBpsRaw = settings.MaxDownloadSpeedKBps.ToString();
        ViewModel.TempSelectedTheme = settings.SelectedTheme;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (StorageProvider == null) return;

        var folderPickerOptions = new FolderPickerOpenOptions
        {
            Title = "Select Default Save Location",
            AllowMultiple = false
        };

        var result = await StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

        if (result != null && result.Count > 0)
        {
            var localPath = result[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(localPath))
                ViewModel.TempDefaultSaveLocation = localPath;
            else
                Console.WriteLine($"Could not retrieve a local path for the selected folder: {result[0].Name}");
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.GetSettings();
        settings.DefaultSaveLocation = ViewModel.TempDefaultSaveLocation;

        if (int.TryParse(ViewModel.TempMaxDownloadSpeedKBpsRaw, out int parsedSpeed))
        {
            settings.MaxDownloadSpeedKBps = parsedSpeed;
        }
        else
        {
            settings.MaxDownloadSpeedKBps = 0;
        }

        settings.SelectedTheme = ViewModel.TempSelectedTheme;

        await _settingsService.UpdateSettings(settings);

        if (Application.Current is App app) app.ApplyTheme(settings.SelectedTheme);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}