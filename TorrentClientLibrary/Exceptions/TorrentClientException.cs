using System;
using System.Runtime.Serialization;

namespace TorrentFlow.TorrentClientLibrary.Exceptions
{
    [Serializable]
    public class TorrentClientException : Exception
    {
        public TorrentClientException()
            : base()
        {
        }
        public TorrentClientException(string message)
            : base(message)
        {
        }
        public TorrentClientException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected TorrentClientException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
