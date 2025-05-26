using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TorrentFlow.Data;
using TorrentFlow.Enums;

namespace TorrentFlow.Services
{
    public class SettingsService
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsFilePath;
        private Settings _settings;
        private readonly TorrentManagerService _torrentManagerService;

        public SettingsService(TorrentManagerService torrentManagerService)
        {
            _torrentManagerService = torrentManagerService ?? throw new ArgumentNullException(nameof(torrentManagerService));
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            LoadSettingsInternal(); 
        }

        public async Task InitializeAsync()
        {
            await ApplySpeedLimitAsync();
        }

        private void LoadSettingsInternal()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _settings = JsonSerializer.Deserialize<Settings>(json);
                    if (_settings == null)
                    {
                        Console.WriteLine("Settings deserialized to null, using default settings.");
                        _settings = CreateDefaultSettings();
                    }
                }
                else
                {
                    _settings = CreateDefaultSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}. Using default settings.");
                _settings = CreateDefaultSettings();
            }
        }
        
        private Settings CreateDefaultSettings()
        {
            return new Settings
            {
                DefaultSaveLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Torrents"),
                MaxDownloadSpeedKBps = 0,
                SelectedTheme = ThemeType.Default
            };
        }

        public Settings GetSettings()
        {
            return _settings;
        }

        public async Task UpdateSettings(Settings newSettings)
        {
            _settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
            SaveSettingsInternal();
            await ApplySpeedLimitAsync();
        }

        private async Task ApplySpeedLimitAsync()
        {
            if (_settings != null)
            {
                await _torrentManagerService.SetSpeed(_settings.MaxDownloadSpeedKBps);
            }
        }
        
        private void SaveSettingsInternal()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public void SaveSettings()
        {
            SaveSettingsInternal();
        }
    }
}