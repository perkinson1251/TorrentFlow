using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TorrentLeechingEventArgs : EventArgs
    {
        public TorrentLeechingEventArgs(TorrentInfo torrentInfo)
        {
            torrentInfo.CannotBeNull();

            this.TorrentInfo = torrentInfo;
        }
        private TorrentLeechingEventArgs()
        {
        }
        public TorrentInfo TorrentInfo
        {
            get;
            private set;
        }
    }
}
