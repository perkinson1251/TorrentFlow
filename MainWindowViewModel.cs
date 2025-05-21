using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TorrentFlow
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TorrentInfo> _torrents;

        [ObservableProperty]
        private string? _statusText;

        private TorrentFileParser _torrentFileParser;

        public MainWindowViewModel()
        {
            _torrents = new ObservableCollection<TorrentInfo>();
            StatusText = "Готовий";

            _torrentFileParser = new TorrentFileParser();

            LoadTestData();
        }

        private void LoadTestData()
        {
             Torrents.Add(new TorrentInfo
            {
                Name = "Ubuntu Desktop 24.04 LTS",
                Size = "4.7 GB",
                Progress = 65.5,
                Status = "Завантажується",
                DownloadSpeed = "1.2 MB/s",
                UploadSpeed = "150 KB/s",
                Peers = "50/120"
            });
            Torrents.Add(new TorrentInfo
            {
                Name = "LibreOffice 7.6",
                Size = "350 MB",
                Progress = 100.0,
                Status = "Роздається",
                DownloadSpeed = "0 KB/s",
                UploadSpeed = "55 KB/s",
                Peers = "10/10"
            });
        }

        [RelayCommand]
        private async Task AddTorrentAsync()
        {
            StatusText = "Відкриття діалогу вибору файлу...";
            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                StatusText = "Помилка: не вдалося отримати доступ до вікна.";
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Виберіть .torrent файл",
                AllowMultiple = false,
                FileTypeFilter = new[] { TorrentFileFilter }
            });

            if (files.Count >= 1)
            {
                var filePath = files[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(filePath))
                {
                    StatusText = $"Файл вибрано: {filePath}";
                    try
                    {
                        var torrentFileContent = await _torrentFileParser.ParseFileAsync(filePath);
                        if (torrentFileContent != null)
                        {
                            Torrents.Add(new TorrentInfo
                            {
                                Name = torrentFileContent.Name ?? "Невідомий торент",
                                Size = FormatSize(torrentFileContent.TotalSize), // Потрібен метод форматування розміру
                                Status = "Додано",
                                Progress = 0
                                // Інші властивості...
                            });
                            StatusText = $"Торент '{torrentFileContent.Name}' успішно додано.";
                        }
                        else
                        {
                            StatusText = "Не вдалося розпарсити торент файл.";
                        }
                    }
                    catch (System.Exception ex)
                    {
                        StatusText = $"Помилка парсингу файлу: {ex.Message}";
                         System.Diagnostics.Debug.WriteLine($"Помилка парсингу: {ex}");
                    }
                }
                else
                {
                    StatusText = "Помилка: не вдалося отримати локальний шлях до файлу.";
                }
            }
            else
            {
                StatusText = "Вибір файлу скасовано.";
            }
        }
        
        private static TopLevel? GetTopLevel()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                return desktopLifetime.MainWindow;
            }
            if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
            {
                if (singleViewLifetime.MainView is TopLevel tl)
                {
                    return tl;
                }
            }
            return null; 
        }

        public static FilePickerFileType TorrentFileFilter { get; } = new("Torrent Files")
        {
            Patterns = new[] { "*.torrent" }
        };

        private string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblSByte = bytes;
            while (dblSByte >= 1024 && i < suffixes.Length -1)
            {
                dblSByte /= 1024;
                i++;
            }
            return $"{dblSByte:0.##} {suffixes[i]}";
        }


        [RelayCommand]
        private void OpenSettings()
        {
            StatusText = "Функція 'Налаштування' ще не реалізована.";
        }
    }
}