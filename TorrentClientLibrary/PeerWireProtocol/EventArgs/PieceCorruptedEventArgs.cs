using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class PieceCorruptedEventArgs : EventArgs
    {
        public PieceCorruptedEventArgs(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);

            this.PieceIndex = pieceIndex;
        }
        private PieceCorruptedEventArgs()
        {
        }
        public int PieceIndex
        {
            get;
            private set;
        }
    }
}
