using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MonoTorrent.Client;

namespace TorrentFlow
{
    public class TorrentStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TorrentState state)
            {
                return state switch
                {
                    TorrentState.Downloading => "Завантажується",
                    TorrentState.Seeding => "Роздається",
                    TorrentState.Paused => "Призупинено",
                    TorrentState.Stopped => "Завантажено",
                    TorrentState.Error => "Помилка",
                    TorrentState.Hashing => "Перевірка",
                    TorrentState.Metadata => "Метадані",
                    TorrentState.Stopping => "Зупиняється",
                    _ => state.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}