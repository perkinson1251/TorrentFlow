using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TorrentSeedingEventArgs : EventArgs
    {
        public TorrentSeedingEventArgs(TorrentInfo torrentInfo)
        {
            torrentInfo.CannotBeNull();

            this.TorrentInfo = torrentInfo;
        }
        private TorrentSeedingEventArgs()
        {
        }
        public TorrentInfo TorrentInfo
        {
            get;
            private set;
        }
    }
}
