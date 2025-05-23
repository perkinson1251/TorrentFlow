using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public class ScrapeDetails
    {
        public ScrapeDetails(int seedersCount, int leechesCount, int completeCount)
        {
            seedersCount.MustBeGreaterThanOrEqualTo(0);
            leechesCount.MustBeGreaterThanOrEqualTo(0);
            completeCount.MustBeGreaterThanOrEqualTo(0);

            this.CompleteCount = completeCount;
            this.LeechesCount = leechesCount;
            this.SeedersCount = seedersCount;
        }
        private ScrapeDetails()
        {
        }
        public int CompleteCount
        {
            get;
            private set;
        }
        public int LeechesCount
        {
            get;
            private set;
        }
        public int SeedersCount
        {
            get;
            private set;
        }
    }
}
