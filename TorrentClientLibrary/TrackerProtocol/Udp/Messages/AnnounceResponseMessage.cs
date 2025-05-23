using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public class AnnounceResponseMessage : TrackerMessage
    {
        private const int ActionLength = 4;
        private const int IntervalLength = 4;
        private const int IpAddressLength = 4;
        private const int LeechersLength = 4;
        private const int PortLength = 2;
        private const int SeedersLength = 4;
        private const int TransactionIdLength = 4;
        public AnnounceResponseMessage(int transactionId, TimeSpan interval, int leecherCount, int seederCount, IEnumerable<IPEndPoint> peers)
            : base(TrackingAction.Announce, transactionId)
        {
            interval.MustBeGreaterThan(TimeSpan.Zero);
            leecherCount.MustBeGreaterThanOrEqualTo(0);
            seederCount.MustBeGreaterThanOrEqualTo(0);
            peers.CannotBeNull();

            this.Interval = interval;
            this.LeecherCount = leecherCount;
            this.SeederCount = seederCount;
            this.Peers = peers;
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
        public override int Length
        {
            get
            {
                return ActionLength + TransactionIdLength + IntervalLength + LeechersLength + SeedersLength + (this.Peers.Count() * (IpAddressLength + PortLength));
            }
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
        public static bool TryDecode(byte[] buffer, int offset, out AnnounceResponseMessage message)
        {
            int action;
            int transactionId;
            int interval;
            int leechers;
            int seeders;
            IPEndPoint endpoint;
            IDictionary<string, IPEndPoint> peers = new Dictionary<string, IPEndPoint>();

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + ActionLength + TransactionIdLength + IntervalLength + LeechersLength + SeedersLength &&
                offset >= 0)
            {
                action = Message.ReadInt(buffer, ref offset);
                transactionId = Message.ReadInt(buffer, ref offset);
                interval = Message.ReadInt(buffer, ref offset);
                leechers = Message.ReadInt(buffer, ref offset);
                seeders = Message.ReadInt(buffer, ref offset);

                if (action == (int)TrackingAction.Announce &&
                    transactionId >= 0 &&
                    interval > 0 &&
                    leechers >= 0 &&
                    seeders >= 0)
                {
                    while (offset <= buffer.Length - IpAddressLength - PortLength)
                    {
                        endpoint = Message.ReadEndpoint(buffer, ref offset);

                        if (!peers.ContainsKey(endpoint.Address.ToString()))
                        {
                            peers.Add(endpoint.Address.ToString(), endpoint);
                        }
                    }

                    message = new AnnounceResponseMessage(transactionId, TimeSpan.FromSeconds(interval), leechers, seeders, peers.Values);
                }
            }

            return message != null;
        }
        public override int Encode(byte[] buffer, int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);

            int written = offset;

            Message.Write(buffer, ref written, (int)this.Action);
            Message.Write(buffer, ref written, this.TransactionId);
            Message.Write(buffer, ref written, (int)this.Interval.TotalSeconds);
            Message.Write(buffer, ref written, this.LeecherCount);
            Message.Write(buffer, ref written, this.SeederCount);

            foreach (IPEndPoint peerEndpoint in this.Peers)
            {
                Message.Write(buffer, ref written, peerEndpoint);
            }

            return written - offset;
        }
        public override string ToString()
        {
            return "UdpTrackerAnnounceResponseMessage";
        }
    }
}
