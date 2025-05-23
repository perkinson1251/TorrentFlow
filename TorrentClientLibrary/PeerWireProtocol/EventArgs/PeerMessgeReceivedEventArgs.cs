using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class PeerMessgeReceivedEventArgs : EventArgs
    {
        public PeerMessgeReceivedEventArgs(PeerMessage message)
        {
            message.CannotBeNull();

            this.Message = message;
        }
        public PeerMessage Message
        {
            get;
            private set;
        }
    }
}
