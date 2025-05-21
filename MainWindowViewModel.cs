using System;
using System.Collections.ObjectModel;
using System.IO;
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
        private TrackerClient _trackerClient; // Додаємо клієнт трекера
        private string _peerId; // Зберігаємо наш peer_id
        private const ushort ListeningPort = 6881; // Приклад порту для прослуховування

        public MainWindowViewModel()
        {
            _torrents = new ObservableCollection<TorrentInfo>();
            
            StatusText = "Готовий";

            _torrentFileParser = new TorrentFileParser();
            _trackerClient = new TrackerClient(); // Ініціалізуємо клієнт трекера

            // Генеруємо peer_id один раз при створенні ViewModel
            _peerId = GeneratePeerId();

            LoadTestData(); // Можна закоментувати, якщо вже додаємо реальні торенти
        }
        
        private string GeneratePeerId()
        {
            // Простий варіант: "-TF0001-" + 12 випадкових символів
            // TF - TorrentFlow, 0001 - версія
            var random = new Random();
            var randomChars = new char[12];
            for (int i = 0; i < randomChars.Length; i++)
            {
                randomChars[i] = (char)random.Next(48, 122); // ASCII цифри та літери (не ідеально, але для початку)
            }
            return $"-TF0001-{new string(randomChars)}";
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
                    StatusText = $"Файл вибрано: {Path.GetFileName(filePath)}. Парсинг...";
                    try
                    {
                        TorrentFileContent? torrentContent = await _torrentFileParser.ParseFileAsync(filePath);
                        if (torrentContent != null && torrentContent.InfoHash != null)
                        {
                            var newTorrentInfo = new TorrentInfo
                            {
                                Name = torrentContent.Name ?? Path.GetFileNameWithoutExtension(filePath),
                                Size = FormatSize(torrentContent.TotalSize),
                                Status = "Додано, очікування трекера...",
                                Progress = 0,
                                // Зберігаємо повний контент для подальшого використання
                                FullTorrentContent = torrentContent 
                            };
                            Torrents.Add(newTorrentInfo);
                            StatusText = $"Торент '{newTorrentInfo.Name}' додано. Запит до трекера...";

                            // Робимо запит до трекера
                            TrackerResponse? trackerResponse = await _trackerClient.AnnounceAsync(
                                torrentContent,
                                _peerId,
                                ListeningPort,
                                0, // uploaded
                                0, // downloaded
                                "started" // event
                            );

                            if (trackerResponse != null)
                            {
                                if (trackerResponse.RequestFailed)
                                {
                                    newTorrentInfo.Status = $"Помилка трекера: {trackerResponse.FailureReason}";
                                    StatusText = $"Помилка трекера для '{newTorrentInfo.Name}': {trackerResponse.FailureReason}";
                                }
                                else
                                {
                                    newTorrentInfo.Status = $"Пірів: {trackerResponse.Peers.Count}, S:{trackerResponse.Complete}/L:{trackerResponse.Incomplete}";
                                    StatusText = $"Відповідь трекера для '{newTorrentInfo.Name}': {trackerResponse.Peers.Count} пірів.";
                                    System.Diagnostics.Debug.WriteLine($"Отримані піри ({trackerResponse.Peers.Count}):");
                                    foreach (var peer in trackerResponse.Peers)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"- {peer.EndPoint}");
                                    }
                                    // Тут можна зберегти trackerResponse.Peers для newTorrentInfo
                                    // або передати в PeerManager
                                }
                            }
                            else
                            {
                                newTorrentInfo.Status = "Помилка відповіді від трекера";
                                StatusText = $"Помилка відповіді від трекера для '{newTorrentInfo.Name}'.";
                            }
                        }
                        else
                        {
                            StatusText = "Не вдалося розпарсити торент файл або відсутній InfoHash.";
                        }
                    }
                    catch (System.Exception ex)
                    {
                        StatusText = $"Помилка обробки файлу: {ex.Message}";
                        System.Diagnostics.Debug.WriteLine($"Помилка обробки: {ex}");
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