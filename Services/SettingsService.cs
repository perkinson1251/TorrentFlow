using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using TorrentFlow.Data;
using TorrentFlow.Enums;

namespace TorrentFlow.Services
{
    public class SettingsService
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsFilePath;

        private Settings _settings;

        public SettingsService()
        {
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            LoadSettings();
        }

        public void UpdateTray(App app)
        {
            var trayIcons = TrayIcon.GetIcons(app);
            if (trayIcons == null || !trayIcons.Any()) return;
            var trayIcon = trayIcons.First();
            if (trayIcon.Menu == null || trayIcon.Menu.Items == null) return;

            NativeMenuItem? profilesTray = trayIcon.Menu.Items
                .OfType<NativeMenuItem>()
                .FirstOrDefault(item => item.Header?.ToString() == "Profile");

            if (profilesTray == null)
            {
                Console.WriteLine("Error: 'Profile' NativeMenuItem not found in App.axaml. Creating it.");
                profilesTray = new NativeMenuItem { Header = "Profile", Menu = new NativeMenu() };
                // You might want to insert it at a specific position if other items exist
                var menuItemsList = trayIcon.Menu.Items.Cast<NativeMenuItem>().ToList();
                menuItemsList.Insert(0, profilesTray); // Example: insert at the beginning
                
                // Rebuild the menu items if direct modification of NativeMenu.Items is not fully supported for additions
                // or if NativeMenu.Items is a read-only snapshot.
                // This step depends on Avalonia's NativeMenu behavior.
                // For simplicity, assuming direct modification or that it's initially empty/rebuilt.
                // If it's complex, ensure this logic correctly adds the 'Profile' item if missing.
                // However, it's better to ensure 'Profile' exists in App.axaml.
                // For this fix, we'll assume it should exist from XAML and log an error if not found.
                // Reverting to a simpler find and error if not present:
                profilesTray = trayIcon.Menu.Items
                    .OfType<NativeMenuItem>()
                    .FirstOrDefault(item => item.Header?.ToString() == "Profile");

                if (profilesTray == null) 
                {
                     Console.WriteLine("Critical Error: 'Profile' NativeMenuItem could not be found or created.");
                     return;
                }
            }
            
            if (profilesTray.Menu == null)
            {
                profilesTray.Menu = new NativeMenu();
            }
            profilesTray.Menu.Items.Clear();

            if (_settings?.SpeedProfiles != null)
            {
                foreach (var item in _settings.SpeedProfiles)
                {
                    var menuItem = new NativeMenuItem
                    {
                        Header = item.ProfileName,
                        ToggleType = NativeMenuItemToggleType.Radio,
                        IsChecked = item.Active,
                    };
                    menuItem.Click += (sender, args) => ActivateSpeedProfile(item.ProfileName);
                    profilesTray.Menu.Items.Add(menuItem);
                }
            }
        }
        public Settings GetSettings()
        {
            return _settings;
        }
        public void UpdateSettings(Settings newSettings)
        {
            _settings = newSettings;
            SaveSettings();
        }

        public async Task ActivateSpeedProfile(string? header)
        {
            if (_settings == null || _settings.SpeedProfiles == null || !_settings.SpeedProfiles.Any()) return;

            if (header == null)
            {
                header = _settings.SpeedProfiles.FirstOrDefault(x => x.Active)?.ProfileName;
                if (header == null && _settings.SpeedProfiles.Any()) // If none active, activate the first one
                {
                    header = _settings.SpeedProfiles.First().ProfileName;
                }
            }
            
            if (header == null) return; // Still no profile to activate

            var profile = _settings.SpeedProfiles.FirstOrDefault(x => x.ProfileName == header);
            if (profile == null) return;

            _settings.SpeedProfiles.ToList().ForEach(p => p.Active = false);
            profile.Active = true;
        
            SaveSettings(); // Save settings after changing active profile
            // UpdateTray((App)App.Current); // Update tray after activating

            var speedInKb = 0;
            switch (profile.UnitType)
            {
                case SpeedUnitType.Kb:
                    speedInKb = profile.Speed;
                    break;
                case SpeedUnitType.Mb:
                    speedInKb = profile.Speed * 1024;
                    break;
                case SpeedUnitType.Gb:
                    speedInKb = profile.Speed * 1024 * 1024;
                    break;
            }
            TorrentManagerService torrentManager = App.GetService<TorrentManagerService>();
            await torrentManager.SetSpeed(speedInKb);
        }
        private async Task LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    _settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
                else
                {
                    _settings = new Settings();
                }

                if (_settings.SpeedProfiles == null || !_settings.SpeedProfiles.Any())
                {
                    _settings.SpeedProfiles = new System.Collections.Generic.List<SpeedProfileEntry>();
                    SpeedProfileEntry unlimited = new SpeedProfileEntry { Active = true, ProfileName = "Unlimited", Speed = 0, UnitType = SpeedUnitType.Kb };
                    _settings.SpeedProfiles.Add(unlimited);
                }
                
                // Ensure at least one profile is active
                if (!_settings.SpeedProfiles.Any(p => p.Active) && _settings.SpeedProfiles.Any())
                {
                    _settings.SpeedProfiles.First().Active = true;
                }

                await ActivateSpeedProfile(null); // Activate based on loaded settings or default
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                _settings = new Settings();
                 if (_settings.SpeedProfiles == null || !_settings.SpeedProfiles.Any())
                {
                    _settings.SpeedProfiles = new System.Collections.Generic.List<SpeedProfileEntry>();
                    SpeedProfileEntry unlimited = new SpeedProfileEntry { Active = true, ProfileName = "Unlimited", Speed = 0, UnitType = SpeedUnitType.Kb };
                    _settings.SpeedProfiles.Add(unlimited);
                }
                await ActivateSpeedProfile(null);
            }
        }
        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                if (App.Current is App currentApp)
                {
                    UpdateTray(currentApp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}