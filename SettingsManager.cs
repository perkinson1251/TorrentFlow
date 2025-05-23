using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Styling;

namespace TorrentFlow
{
    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "TorrentFlow",
            "settings.json");

        private static JsonSerializerOptions _serializerOptions;

        static SettingsManager()
        {
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } 
            };
        }
        
        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions);
                    if (settings != null && settings.SelectedTheme == null)
                    {
                        settings.SelectedTheme = ThemeVariant.Default;
                    }
                    return settings ?? new AppSettings();
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(settings, _serializerOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}