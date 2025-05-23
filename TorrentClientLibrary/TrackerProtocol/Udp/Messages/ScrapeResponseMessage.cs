using System.Collections.Generic;
using System.Linq;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public sealed class ScrapeResponseMessage : TrackerMessage
    {
        private const int ActionLength = 4;
        private const int CompletedLength = 4;
        private const int LeechersLength = 4;
        private const int SeedersLength = 4;
        private const int TransactionIdLength = 4;
        public ScrapeResponseMessage()
            : this(0, new List<ScrapeDetails>())
        {
        }
        public ScrapeResponseMessage(int transactionId, IEnumerable<ScrapeDetails> scrapes)
            : base(TrackingAction.Scrape, transactionId)
        {
            scrapes.CannotBeNull();

            this.Scrapes = scrapes;
        }
        public override int Length
        {
            get
            {
                return ActionLength + TransactionIdLength + (this.Scrapes.Count() * (SeedersLength + CompletedLength + LeechersLength));
            }
        }
        public IEnumerable<ScrapeDetails> Scrapes
        {
            get;
            private set;
        }
        public static bool TryDecode(byte[] buffer, int offset, out ScrapeResponseMessage message)
        {
            int action;
            int transactionId;
            int seeds;
            int completed;
            int leechers;
            List<ScrapeDetails> scrapeInfo = new List<ScrapeDetails>();

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + ActionLength + TransactionIdLength &&
                offset >= 0)
            {
                action = Message.ReadInt(buffer, ref offset);
                transactionId = Message.ReadInt(buffer, ref offset);

                if (action == (int)TrackingAction.Scrape &&
                    transactionId >= 0)
                {
                    while (offset <= buffer.Length - SeedersLength - CompletedLength - LeechersLength)
                    {
                        seeds = Message.ReadInt(buffer, ref offset);
                        completed = Message.ReadInt(buffer, ref offset);
                        leechers = Message.ReadInt(buffer, ref offset);

                        scrapeInfo.Add(new ScrapeDetails(seeds, leechers, completed));
                    }

                    message = new ScrapeResponseMessage(transactionId, scrapeInfo);
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

            foreach (var scrape in this.Scrapes)
            {
                Message.Write(buffer, ref written, scrape.SeedersCount);
                Message.Write(buffer, ref written, scrape.CompleteCount);
                Message.Write(buffer, ref written, scrape.LeechesCount);
            }

            return written - offset;
        }
    }
}
