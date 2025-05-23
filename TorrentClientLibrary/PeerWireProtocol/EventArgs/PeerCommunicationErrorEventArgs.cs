using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class PeerCommunicationErrorEventArgs : EventArgs
    {
        public PeerCommunicationErrorEventArgs(string errorMessage, bool isFatal)
        {
            errorMessage.CannotBeNullOrEmpty();

            this.ErrorMessage = errorMessage;
            this.IsFatal = isFatal;
        }
        private PeerCommunicationErrorEventArgs()
        {
        }
        public string ErrorMessage
        {
            get;
            private set;
        }
        public bool IsFatal
        {
            get;
            private set;
        }
    }
}
