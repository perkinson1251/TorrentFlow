using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using MonoTorrent.Client;

namespace TorrentFlow
{
    public class TorrentView : INotifyPropertyChanged
    {
        private readonly TorrentManager _torrentManager;
        private string _downloadSpeedString; // Змінено
        private string _uploadSpeedString;   // Змінено
        public string FileName { get; set; }
        public string Name => _torrentManager.Torrent.Name;
        public string SavePath => _torrentManager.SavePath;
        public double Progress => _torrentManager.Complete ? 100 : Math.Round(_torrentManager.Progress, 2); // Додано округлення для прогресу
        public bool Completed => _torrentManager.Complete;

        public string DownloadSpeed // Змінено тип на string
        {
            get => _downloadSpeedString;
            set
            {
                if (_downloadSpeedString != value)
                {
                    _downloadSpeedString = value;
                    OnPropertyChanged();
                }
            }
        }

        public string UploadSpeed // Змінено тип на string
        {
            get => _uploadSpeedString;
            set
            {
                if (_uploadSpeedString != value)
                {
                    _uploadSpeedString = value;
                    OnPropertyChanged();
                }
            }
        }

        public TorrentState Status => _torrentManager.State;

        public event PropertyChangedEventHandler PropertyChanged;

        public TorrentView(TorrentManager manager)
        {
            _torrentManager = manager;
            _torrentManager.TorrentStateChanged += (_, __) => OnPropertyChanged(nameof(Status));
            // Ініціалізація значень швидкостей
            UpdateSpeeds();
        }

        public void UpdateProgress() => OnPropertyChanged(nameof(Progress));

        private static string FormatSpeed(double speedInKB)
        {
            if (speedInKB < 0) speedInKB = 0; // Переконаємося, що швидкість не від'ємна

            if (speedInKB < 1024)
            {
                return $"{speedInKB:F2} KB/s";
            }
            else if (speedInKB < 1024 * 1024)
            {
                return $"{speedInKB / 1024.0:F2} MB/s";
            }
            else
            {
                return $"{speedInKB / (1024.0 * 1024.0):F2} GB/s";
            }
        }

        public void UpdateSpeeds()
        {
            Dispatcher.UIThread.Post(() =>
            {
                DownloadSpeed = FormatSpeed(_torrentManager.Monitor.DownloadRate / 1024.0); // Використовуємо KB для розрахунку
                UploadSpeed = FormatSpeed(_torrentManager.Monitor.UploadRate / 1024.0);   // Використовуємо KB для розрахунку
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}