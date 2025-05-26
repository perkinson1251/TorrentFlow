using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using TorrentFlow.Data;

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
            _torrentManagerService = torrentManagerService;
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            LoadSettings();
        }

        public Settings GetSettings()
        {
            return _settings;
        }

        public async Task UpdateSettings(Settings newSettings)
        {
            _settings = newSettings;
            SaveSettings();
            await ApplySpeedLimitAsync();
        }

        private async Task ApplySpeedLimitAsync()
        {
            if (_settings != null)
            {
                await _torrentManagerService.SetSpeed(_settings.MaxDownloadSpeedKBps);
            }
        }

        private async void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    _settings = JsonSerializer.Deserialize<Settings>(json) ?? CreateDefaultSettings();
                }
                else
                {
                    _settings = CreateDefaultSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                _settings = CreateDefaultSettings();
            }
            await ApplySpeedLimitAsync(); // Apply speed limit on load
        }

        private Settings CreateDefaultSettings()
        {
            return new Settings
            {
                DefaultSaveLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Torrents"), // Example default
                MaxDownloadSpeedKBps = 0 // Unlimited
            };
        }

        public async void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                await ApplySpeedLimitAsync(); // Re-apply speed limit on save
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}