using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TorrentFlow.Converters
{
    public class BytesToKilobytesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return bytes / 1024;
            }
            if (value is int intBytes)
            {
                return intBytes / 1024;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal kilobytes)
            {
                return (long)(kilobytes * 1024);
            }
            if (value is double doubleKilobytes)
            {
                return (long)(doubleKilobytes * 1024);
            }
            if (value is int intKilobytes)
            {
                return (long)(intKilobytes * 1024);
            }
            return 0L;
        }
    }
}