using System;
using System.Runtime.Serialization;

namespace TorrentFlow.TorrentClientLibrary.Exceptions
{
    [Serializable]
    public class TrackerProtocolException : Exception
    {
        public TrackerProtocolException()
            : base()
        {
        }
        public TrackerProtocolException(string message)
            : base(message)
        {
        }
        public TrackerProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected TrackerProtocolException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
