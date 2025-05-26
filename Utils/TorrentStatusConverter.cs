using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MonoTorrent.Client;

namespace TorrentFlow;

public class TorrentStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TorrentState state)
            return state switch
            {
                TorrentState.Downloading => "Downloading",
                TorrentState.Seeding => "Seeding",
                TorrentState.Paused => "Paused",
                TorrentState.Stopped => "Stopped",
                TorrentState.Error => "Error",
                TorrentState.Hashing => "Hashing",
                TorrentState.Metadata => "Metadata",
                TorrentState.Stopping => "Stopping",
                _ => state.ToString()
            };
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}