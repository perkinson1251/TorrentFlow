using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // Для ICommand та RelayCommand

namespace TorrentFlow
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TorrentInfo> _torrents;

        [ObservableProperty]
        private string? _statusText;

        public MainWindowViewModel()
        {
            _torrents = new ObservableCollection<TorrentInfo>();
            StatusText = "Готовий"; // Початковий статус

            // Додамо тестові дані для перевірки
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
            Torrents.Add(new TorrentInfo 
            { 
                Name = "Big Buck Bunny", 
                Size = "270 MB", 
                Progress = 12.0, 
                Status = "Пауза", 
                DownloadSpeed = "0 KB/s", 
                UploadSpeed = "0 KB/s", 
                Peers = "5/68" 
            });
        }

        [RelayCommand]
        private void AddTorrent()
        {
            // Тут буде логіка додавання нового торента
            // Наприклад, відкриття діалогу вибору файлу
            StatusText = "Функція 'Додати торент' ще не реалізована.";
            // Для тесту можемо додати ще один рядок
            Torrents.Add(new TorrentInfo { Name = "Новий Тестовий Торент", Size = "10 MB", Progress = 0, Status = "В черзі" });
        }

        [RelayCommand]
        private void OpenSettings()
        {
            // Тут буде логіка відкриття налаштувань
            StatusText = "Функція 'Налаштування' ще не реалізована.";
        }
    }
}