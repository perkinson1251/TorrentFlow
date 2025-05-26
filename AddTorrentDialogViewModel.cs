using System.ComponentModel;

namespace TorrentFlow
{
    public class AddTorrentDialogViewModel : INotifyPropertyChanged
    {
        private string _selectedDirectory;
        private string _torrentName;

        public string TorrentName
        {
            get => _torrentName;
            set
            {
                if (_torrentName != value)
                {
                    _torrentName = value;
                    OnPropertyChanged(nameof(TorrentName));
                }
            }
        }

        public string SelectedDirectory
        {
            get => _selectedDirectory;
            set
            {
                if (_selectedDirectory != value)
                {
                    _selectedDirectory = value;
                    OnPropertyChanged(nameof(SelectedDirectory));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}