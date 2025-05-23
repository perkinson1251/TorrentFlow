using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TorrentStoppedEventArgs : EventArgs
    {
        public TorrentStoppedEventArgs(TorrentInfo torrentInfo)
        {
            torrentInfo.CannotBeNull();

            this.TorrentInfo = torrentInfo;
        }
        private TorrentStoppedEventArgs()
        {
        }
        public TorrentInfo TorrentInfo
        {
            get;
            private set;
        }
    }
}
