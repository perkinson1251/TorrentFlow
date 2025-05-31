using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace TorrentFlow
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<TorrentView> _torrents = new();
        public ObservableCollection<TorrentView> Torrents
        {
            get => _torrents;
            private set
            {
                _torrents = value;
                OnPropertyChanged();
                NotifyTorrentCountChanged();
            }
        }
        
        public int TorrentsCount => Torrents?.Count ?? 0;
        public bool HasTorrents => TorrentsCount > 0;
        public bool NoTorrents => TorrentsCount == 0;

        public MainWindowViewModel()
        {
            Torrents.CollectionChanged += (s, e) => NotifyTorrentCountChanged();
        }

        private void NotifyTorrentCountChanged()
        {
            OnPropertyChanged(nameof(TorrentsCount));
            OnPropertyChanged(nameof(HasTorrents));
            OnPropertyChanged(nameof(NoTorrents));
        }
        
        public void UpdateAllTorrentVisuals()
        {
            foreach (var torrentView in Torrents)
            {
                torrentView.UpdateProgress(); // For Progress, Status, Completed
                torrentView.UpdateSpeeds();   // For DownloadSpeed, UploadSpeed
            }
        }

        public void AddTorrent(TorrentView torrentView)
        {
            if (!Torrents.Any(t => t.Name == torrentView.Name))
            {
                Dispatcher.UIThread.Post(() => Torrents.Add(torrentView));
            }
        }

        public void RemoveTorrent(TorrentView torrentView)
        {
            if (Torrents.Contains(torrentView))
            {
                Dispatcher.UIThread.Post(() => Torrents.Remove(torrentView));
            }
        }
    }
}