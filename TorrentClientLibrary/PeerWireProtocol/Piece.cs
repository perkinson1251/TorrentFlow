using System;
using System.Globalization;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class Piece
    {
        private int completedBlockCount = 0;
        public Piece(int pieceIndex, string pieceHash, long pieceLength, int blockLength, int blockCount, byte[] pieceData = null, bool[] bitField = null)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceHash.CannotBeNullOrEmpty();
            pieceLength.MustBeGreaterThan(0);
            blockCount.MustBeGreaterThan(0);
            pieceData.IsNotNull().Then(() => pieceData.LongLength.MustBeEqualTo(pieceLength));
            bitField.IsNotNull().Then(() => bitField.LongLength.MustBeEqualTo(blockCount));

            this.PieceIndex = pieceIndex;
            this.PieceHash = pieceHash;
            this.PieceLength = pieceLength;
            this.PieceData = pieceData ?? new byte[this.PieceLength];

            this.BlockLength = blockLength;
            this.BlockCount = blockCount;

            this.IsCompleted = false;
            this.IsCorrupted = false;

            this.BitField = bitField ?? new bool[blockCount];
        }
        private Piece()
        {
        }
        public event EventHandler<PieceCompletedEventArgs> Completed;
        public event EventHandler<EventArgs> Corrupted;
        public bool[] BitField
        {
            get;
            private set;
        }
        public int BlockCount
        {
            get;
            private set;
        }
        public int BlockLength
        {
            get;
            private set;
        }
        public bool IsCompleted
        {
            get;
            private set;
        }
        public bool IsCorrupted
        {
            get;
            private set;
        }
        public byte[] PieceData
        {
            get;
            private set;
        }
        public string PieceHash
        {
            get;
            private set;
        }
        public int PieceIndex
        {
            get;
            private set;
        }
        public long PieceLength
        {
            get;
            private set;
        }
        public byte[] GetBlock(long blockOffset)
        {
            blockOffset.MustBeGreaterThanOrEqualTo(0);
            blockOffset.MustBeLessThan(this.PieceLength);

            byte[] data;

            data = new byte[this.GetBlockLength(blockOffset)];

            Buffer.BlockCopy(this.PieceData, (int)blockOffset, data, 0, data.Length);

            return data;
        }
        public int GetBlockIndex(long blockOffset)
        {
            blockOffset.MustBeGreaterThanOrEqualTo(0);
            blockOffset.MustBeLessThanOrEqualTo(this.PieceLength);
            (blockOffset % this.BlockLength).MustBeEqualTo(0);

            return (int)(blockOffset / this.BlockLength);
        }
        public long GetBlockLength(long blockOffset)
        {
            return Math.Min(this.BlockLength, this.PieceLength - blockOffset);
        }
        public long GetBlockOffset(int blockIndex)
        {
            blockIndex.MustBeGreaterThanOrEqualTo(0);
            blockIndex.MustBeLessThanOrEqualTo((int)(this.PieceLength / this.BlockLength));

            return this.BlockLength * blockIndex;
        }
        public void PutBlock(int blockOffset, byte[] blockData = null)
        {
            blockOffset.MustBeGreaterThanOrEqualTo(0);
            ((long)blockOffset).MustBeLessThan(this.PieceLength);
            (blockOffset % this.BlockLength).MustBeEqualTo(0);
            blockData.IsNotNull().Then(() => blockData.CannotBeNullOrEmpty());
            blockData.IsNotNull().Then(() => blockData.Length.MustBeEqualTo((int)this.GetBlockLength(blockOffset)));

            int blockIndex = this.GetBlockIndex(blockOffset);

            if (!this.BitField[blockIndex])
            {
                this.BitField[blockIndex] = true;

                if (blockData != null)
                {
                    Buffer.BlockCopy(blockData, 0, this.PieceData, blockOffset, blockData.Length);
                }

                this.completedBlockCount++;

                if (this.completedBlockCount == this.BlockCount)
                {
                    if (string.Compare(this.PieceData.CalculateSha1Hash(0, (int)this.PieceLength).ToHexaDecimalString(), this.PieceHash, true, CultureInfo.InvariantCulture) == 0)
                    {
                        this.IsCompleted = true;

                        this.OnCompleted(this, new PieceCompletedEventArgs(this.PieceIndex, this.PieceData));
                    }
                    else
                    {
                        this.IsCorrupted = true;

                        this.OnCorrupted(this, new PieceCorruptedEventArgs(this.PieceIndex));
                    }
                }
            }
        }
        private void OnCompleted(object sender, PieceCompletedEventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.Completed != null)
            {
                this.Completed(sender, e);
            }
        }
        private void OnCorrupted(object sender, EventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.Corrupted != null)
            {
                this.Corrupted(sender, e);
            }
        }
    }
}
