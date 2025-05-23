using System;
using System.Runtime.Serialization;

namespace TorrentFlow.TorrentClientLibrary.Exceptions
{
    [Serializable]
    public class PeerWireProtocolException : Exception
    {
        public PeerWireProtocolException()
            : base()
        {
        }
        public PeerWireProtocolException(string message)
            : base(message)
        {
        }
        public PeerWireProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected PeerWireProtocolException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
