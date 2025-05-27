using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using TorrentFlow.Enums;
using TorrentFlow.Services;

namespace TorrentFlow;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private readonly SettingsService _settingsService;
    private string _tempDefaultSaveLocation;

    public string TempDefaultSaveLocation
    {
        get => _tempDefaultSaveLocation;
        set
        {
            if (_tempDefaultSaveLocation != value)
            {
                _tempDefaultSaveLocation = value;
                OnPropertyChanged();
            }
        }
    }

    private string _tempMaxDownloadSpeedKBpsRaw;

    public string TempMaxDownloadSpeedKBpsRaw
    {
        get => _tempMaxDownloadSpeedKBpsRaw;
        set
        {
            if (_tempMaxDownloadSpeedKBpsRaw != value)
            {
                _tempMaxDownloadSpeedKBpsRaw = value;
                OnPropertyChanged();
            }
        }
    }

    private ThemeType _tempSelectedTheme;

    public ThemeType TempSelectedTheme
    {
        get => _tempSelectedTheme;
        set
        {
            if (_tempSelectedTheme != value)
            {
                _tempSelectedTheme = value;
                OnPropertyChanged();
            }
        }
    }

    public Array ThemeTypes => Enum.GetValues(typeof(ThemeType));

    public SettingsWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        DataContext = this;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.GetSettings();
        TempDefaultSaveLocation = settings.DefaultSaveLocation;
        // Конвертуємо int в string дл відображення
        TempMaxDownloadSpeedKBpsRaw = settings.MaxDownloadSpeedKBps.ToString(); //
        TempSelectedTheme = settings.SelectedTheme;
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
                TempDefaultSaveLocation = localPath;
            else
                Console.WriteLine($"Could not retrieve a local path for the selected folder: {result[0].Name}");
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.GetSettings();
        settings.DefaultSaveLocation = TempDefaultSaveLocation;

        if (int.TryParse(TempMaxDownloadSpeedKBpsRaw, out int parsedSpeed))
        {
            settings.MaxDownloadSpeedKBps = parsedSpeed;
        }
        else
        {
            settings.MaxDownloadSpeedKBps = 0;
        }

        settings.SelectedTheme = TempSelectedTheme;

        await _settingsService.UpdateSettings(settings);

        if (Application.Current is App app) app.ApplyTheme(settings.SelectedTheme);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}