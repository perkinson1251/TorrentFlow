using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TorrentFlow.Enums;

namespace TorrentFlow
{
    public class SettingsWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _tempDefaultSaveLocation;
        public string TempDefaultSaveLocation
        {
            get => _tempDefaultSaveLocation;
            set
            {
                if (_tempDefaultSaveLocation != value)
                {
                    _tempDefaultSaveLocation = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _tempMaxDownloadSpeedKBpsRaw;
        public string TempMaxDownloadSpeedKBpsRaw
        {
            get => _tempMaxDownloadSpeedKBpsRaw;
            set
            {
                if (_tempMaxDownloadSpeedKBpsRaw != value)
                {
                    _tempMaxDownloadSpeedKBpsRaw = value;
                    OnPropertyChanged();
                }
            }
        }

        private ThemeType _tempSelectedTheme;
        public ThemeType TempSelectedTheme
        {
            get => _tempSelectedTheme;
            set
            {
                if (_tempSelectedTheme != value)
                {
                    _tempSelectedTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public IEnumerable<ThemeType> ThemeTypes => Enum.GetValues(typeof(ThemeType)).Cast<ThemeType>();
    }
}