using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class AsyncReadData
    {
        public AsyncReadData(int bufferLength)
        {
            bufferLength.MustBeGreaterThan(0);

            this.Buffer = new byte[bufferLength];
            this.OffsetStart = 0;
            this.OffsetEnd = 0;
        }
        private AsyncReadData()
        {
        }
        public byte[] Buffer
        {
            get;
            private set;
        }
        public int OffsetEnd
        {
            get;
            set;
        }
        public int OffsetStart
        {
            get;
            set;
        }
    }
}
