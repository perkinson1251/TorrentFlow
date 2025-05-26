using System.ComponentModel;
using System.Runtime.CompilerServices;
using TorrentFlow.Enums;

namespace TorrentFlow.Data
{
    public class SpeedProfileEntry : INotifyPropertyChanged
    {
        private string _profileName;
        private int _speed;
        private SpeedUnitType _unitType;
        private bool _active;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (_profileName != value)
                {
                    _profileName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Speed
        {
            get => _speed;
            set
            {
                if (_speed != value)
                {
                    _speed = value;
                    OnPropertyChanged();
                }
            }
        }

        public SpeedUnitType UnitType
        {
            get => _unitType;
            set
            {
                if (_unitType != value)
                {
                    _unitType = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Active
        {
            get => _active;
            set
            {
                if (_active != value)
                {
                    _active = value;
                }
            }
        }
    }
}
