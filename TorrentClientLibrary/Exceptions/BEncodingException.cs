using System;
using System.Runtime.Serialization;

namespace TorrentFlow.TorrentClientLibrary.Exceptions
{
    [Serializable]
    public class BEncodingException : Exception
    {
        public BEncodingException()
            : base()
        {
        }
        public BEncodingException(string message)
            : base(message)
        {
        }
        public BEncodingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected BEncodingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
