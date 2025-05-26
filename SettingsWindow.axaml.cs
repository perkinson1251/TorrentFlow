using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage; // Required for IStorageFolder and StorageProvider
using System;
using System.ComponentModel;
using System.IO; // Required for Path
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TorrentFlow.Data;
using TorrentFlow.Services;

namespace TorrentFlow
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

        private int _tempMaxDownloadSpeedKBps;
        public int TempMaxDownloadSpeedKBps
        {
            get => _tempMaxDownloadSpeedKBps;
            set
            {
                if (_tempMaxDownloadSpeedKBps != value)
                {
                    _tempMaxDownloadSpeedKBps = value;
                    OnPropertyChanged();
                }
            }
        }
        
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
            TempMaxDownloadSpeedKBps = settings.MaxDownloadSpeedKBps;
        }
        
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.StorageProvider == null) return;

            var folderPickerOptions = new FolderPickerOpenOptions
            {
                Title = "Select Default Save Location",
                AllowMultiple = false,
            };

            var result = await this.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

            if (result != null && result.Count > 0)
            {
                // IStorageFolder itself doesn't have TryGetLocalPath directly, 
                // but it implements IStorageItem which has this extension method.
                string localPath = result[0].TryGetLocalPath(); 
                if (!string.IsNullOrEmpty(localPath))
                {
                    TempDefaultSaveLocation = localPath;
                }
                else
                {
                    // Fallback or error if a local path couldn't be obtained
                    Console.WriteLine($"Could not retrieve a local path for the selected folder: {result[0].Name}");
                    // Optionally show a message to the user
                }
            }
        }
        
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.GetSettings();
            settings.DefaultSaveLocation = TempDefaultSaveLocation;
            settings.MaxDownloadSpeedKBps = TempMaxDownloadSpeedKBps;
            
            await _settingsService.UpdateSettings(settings); 
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}