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
        private string _downloadSpeedString;
        private string _uploadSpeedString;
        public string FileName { get; set; }
        public string Name => _torrentManager.Torrent.Name;
        public string SavePath => _torrentManager.SavePath;
        public double Progress => _torrentManager.Complete ? 100 : Math.Round(_torrentManager.Progress, 2);
        public bool Completed => _torrentManager.Complete;

        public string DownloadSpeed
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

        public string UploadSpeed
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

        public TorrentState Status
        {
            get
            {
                var currentState = _torrentManager.State;
                
                if (_torrentManager.Complete && currentState != TorrentState.Seeding)
                {
                    return TorrentState.Stopped;
                }
                
                return currentState;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public TorrentView(TorrentManager manager)
        {
            _torrentManager = manager;
            _torrentManager.TorrentStateChanged += (_, __) => OnPropertyChanged(nameof(Status));
            UpdateSpeeds();
        }

        public void UpdateProgress() 
        {
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Completed));
        }

        private static string FormatSpeed(double speedInKB)
        {
            if (speedInKB < 0) speedInKB = 0;

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
                DownloadSpeed = FormatSpeed(_torrentManager.Monitor.DownloadRate / 1024.0);
                UploadSpeed = FormatSpeed(_torrentManager.Monitor.UploadRate / 1024.0);
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}