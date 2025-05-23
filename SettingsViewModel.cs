using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TorrentFlow
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly AppSettings _appSettings;
        private readonly Window _ownerWindow;

        [ObservableProperty]
        private string _downloadPath;

        [ObservableProperty]
        private ThemeVariant _selectedTheme;
        
        public ThemeVariant[] AvailableThemes { get; } = new[] { ThemeVariant.Light, ThemeVariant.Dark, ThemeVariant.Default };

        [ObservableProperty]
        private long _maxDownloadSpeed; // В байтах

        [ObservableProperty]
        private long _maxUploadSpeed; // В байтах

        [ObservableProperty]
        private int _maxActiveDownloads;

        [ObservableProperty]
        private int _listeningPort;

        public SettingsViewModel(AppSettings appSettings, Window ownerWindow)
        {
            _appSettings = appSettings;
            _ownerWindow = ownerWindow;

            DownloadPath = _appSettings.DownloadPath;
            SelectedTheme = _appSettings.SelectedTheme ?? ThemeVariant.Default;
            MaxDownloadSpeed = _appSettings.MaxDownloadSpeed;
            MaxUploadSpeed = _appSettings.MaxUploadSpeed;
            MaxActiveDownloads = _appSettings.MaxActiveDownloads;
            ListeningPort = _appSettings.ListeningPort;

            BrowseDownloadPathCommand = new AsyncRelayCommand(BrowseDownloadPathAsync);
            SaveChangesCommand = new RelayCommand(SaveChangesAndClose);
            CancelCommand = new RelayCommand(CloseWindow);
        }

        public IAsyncRelayCommand BrowseDownloadPathCommand { get; }
        public IRelayCommand SaveChangesCommand { get; }
        public IRelayCommand CancelCommand { get; }

        private async Task BrowseDownloadPathAsync()
        {
            var topLevel = GetTopLevel();
            if (topLevel == null) return;

            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Оберіть папку для завантажень",
                AllowMultiple = false
            });

            if (folder.Count >= 1)
            {
                DownloadPath = folder[0].Path.LocalPath;
            }
        }

        private void SaveChangesAndClose()
        {
            _appSettings.DownloadPath = this.DownloadPath;
            _appSettings.SelectedTheme = this.SelectedTheme;
            _appSettings.MaxDownloadSpeed = this.MaxDownloadSpeed;
            _appSettings.MaxUploadSpeed = this.MaxUploadSpeed;
            _appSettings.MaxActiveDownloads = this.MaxActiveDownloads;
            _appSettings.ListeningPort = this.ListeningPort;
            
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = this.SelectedTheme;
            }

            SettingsManager.SaveSettings(_appSettings);

            CloseWindow();
        }

        private void CloseWindow()
        {
            _ownerWindow?.Close();
        }
        
        private TopLevel GetTopLevel()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Якщо вікно налаштувань відкривається модально, воно може бути активним
                return _ownerWindow ?? desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
            }
            return _ownerWindow; // Повертаємо вікно-власника, якщо воно є
        }
    }
}