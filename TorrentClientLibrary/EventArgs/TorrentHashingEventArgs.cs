using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TorrentHashingEventArgs : EventArgs
    {
        public TorrentHashingEventArgs(TorrentInfo torrentInfo)
        {
            torrentInfo.CannotBeNull();

            this.TorrentInfo = torrentInfo;
        }
        private TorrentHashingEventArgs()
        {
        }
        public TorrentInfo TorrentInfo
        {
            get;
            private set;
        }
    }
}
