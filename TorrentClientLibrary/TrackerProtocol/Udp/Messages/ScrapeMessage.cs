using System.Collections.Generic;
using System.Linq;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public class ScrapeMessage : TrackerMessage
    {
        private const int ActionLength = 4;
        private const int ConnectionIdLength = 8;
        private const int InfoHashLength = 20;
        private const int TransactionIdLength = 4;
        public ScrapeMessage(long connectionId, int transactionId, IEnumerable<string> infoHashes)
            : base(TrackingAction.Scrape, transactionId)
        {
            connectionId.MustBeGreaterThanOrEqualTo(0);
            infoHashes.CannotBeNull();

            this.ConnectionId = connectionId;
            this.InfoHashes = infoHashes;
        }
        public long ConnectionId
        {
            get;
            private set;
        }
        public IEnumerable<string> InfoHashes
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return ConnectionIdLength + ActionLength + TransactionIdLength + (this.InfoHashes.Count() * 20);
            }
        }
        public static bool TryDecode(byte[] buffer, int offset, out ScrapeMessage message)
        {
            long connectionId;
            int action;
            int transactionId;
            List<string> infoHashes = new List<string>();

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + ConnectionIdLength + ActionLength + TransactionIdLength &&
                offset >= 0)
            {
                connectionId = Message.ReadLong(buffer, ref offset);
                action = Message.ReadInt(buffer, ref offset);
                transactionId = Message.ReadInt(buffer, ref offset);

                if (connectionId >= 0 &&
                    action == (int)TrackingAction.Scrape &&
                    transactionId >= 0)
                {
                    while (offset <= buffer.Length - InfoHashLength)
                    {
                        infoHashes.Add(Message.ReadBytes(buffer, ref offset, InfoHashLength).ToHexaDecimalString());
                    }

                    message = new ScrapeMessage(connectionId, transactionId, infoHashes);
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

            Message.Write(buffer, ref written, this.ConnectionId);
            Message.Write(buffer, ref written, (int)this.Action);
            Message.Write(buffer, ref written, this.TransactionId);

            foreach (string infoHash in this.InfoHashes)
            {
                Message.Write(buffer, ref written, infoHash.ToByteArray());
            }

            return written - offset;
        }
    }
}
