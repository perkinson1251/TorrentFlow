using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TorrentStartedEventArgs : EventArgs
    {
        public TorrentStartedEventArgs(TorrentInfo torrentInfo)
        {
            torrentInfo.CannotBeNull();

            this.TorrentInfo = torrentInfo;
        }
        private TorrentStartedEventArgs()
        {
        }
        public TorrentInfo TorrentInfo
        {
            get;
            private set;
        }
    }
}
