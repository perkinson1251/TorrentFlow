using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class PieceRequestedEventArgs : EventArgs
    {
        public PieceRequestedEventArgs(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);

            this.PieceIndex = pieceIndex;
        }
        public byte[] PieceData
        {
            get;
            set;
        }
        public int PieceIndex
        {
            get;
            private set;
        }
    }
}
