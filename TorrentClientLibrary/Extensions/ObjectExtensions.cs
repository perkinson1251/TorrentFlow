using System;

namespace TorrentFlow.TorrentClientLibrary.Extensions
{
    public static class ObjectExtensions
    {
        public static T As<T>(this object value)
        {
            if (value is T)
            {
                return (T)value;
            }
            else
            {
                throw new ArgumentException("Value is of incorrect type");
            }
        }
    }
}
