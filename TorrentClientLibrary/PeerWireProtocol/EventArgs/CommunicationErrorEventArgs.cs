using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class CommunicationErrorEventArgs : EventArgs
    {
        public CommunicationErrorEventArgs(string errorMessage)
        {
            errorMessage.CannotBeNullOrEmpty();

            this.ErrorMessage = errorMessage;
        }
        private CommunicationErrorEventArgs()
        {
        }
        public string ErrorMessage
        {
            get;
            private set;
        }
    }
}
