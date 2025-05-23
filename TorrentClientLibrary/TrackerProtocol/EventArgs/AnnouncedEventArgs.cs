using System;
using System.Collections.Generic;
using System.Net;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol
{
    public sealed class AnnouncedEventArgs : EventArgs
    {
        public AnnouncedEventArgs(TimeSpan interval, int leecherCount, int seederCount, IEnumerable<IPEndPoint> peers)
        {
            interval.MustBeGreaterThan(TimeSpan.Zero);
            leecherCount.MustBeGreaterThanOrEqualTo(0);
            seederCount.MustBeGreaterThanOrEqualTo(0);
            peers.CannotContainOnlyNull();

            this.Interval = interval;
            this.LeecherCount = leecherCount;
            this.SeederCount = seederCount;
            this.Peers = peers;
        }
        private AnnouncedEventArgs()
        {
        }
        public TimeSpan Interval
        {
            get;
            private set;
        }
        public int LeecherCount
        {
            get;
            private set;
        }
        public IEnumerable<IPEndPoint> Peers
        {
            get;
            private set;
        }
        public int SeederCount
        {
            get;
            private set;
        }
    }
}
