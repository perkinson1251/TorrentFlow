using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class PieceCompletedEventArgs : EventArgs
    {
        public PieceCompletedEventArgs(int pieceIndex, byte[] pieceData)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceData.CannotBeNullOrEmpty();

            this.PieceIndex = pieceIndex;
            this.PieceData = pieceData;
        }
        private PieceCompletedEventArgs()
        {
        }
        public byte[] PieceData
        {
            get;
            private set;
        }
        public int PieceIndex
        {
            get;
            private set;
        }
    }
}
