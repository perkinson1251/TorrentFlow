using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class PieceManager : IDisposable
    {
        private readonly TimeSpan checkoutTimeout = TimeSpan.FromSeconds(120);
        private readonly TimeSpan timerTimeout = TimeSpan.FromSeconds(10);
        private Dictionary<int, DateTime> checkouts;
        private object locker = new object();
        private IEnumerable<string> pieceHashes;
        private int piecesCount = 0;
        private int presentPiecesCount = 0;
        private System.Timers.Timer timer;
        public PieceManager(string torrentInfoHash, long torrentLength, IEnumerable<string> pieceHashes, long pieceLength, int blockLength, PieceStatus[] bitField)
        {
            torrentInfoHash.CannotBeNullOrEmpty();
            pieceHashes.CannotBeNullOrEmpty();
            pieceLength.MustBeGreaterThan(0);
            ((long)blockLength).MustBeLessThanOrEqualTo(pieceLength);
            (pieceLength % blockLength).MustBeEqualTo(0);
            bitField.CannotBeNull();
            bitField.Length.MustBeEqualTo(pieceHashes.Count());

            this.PieceLength = pieceLength;
            this.BlockLength = blockLength;
            this.BlockCount = (int)(pieceLength / blockLength);

            this.TorrentInfoHash = torrentInfoHash;
            this.TorrentLength = torrentLength;

            this.pieceHashes = pieceHashes;

            this.BitField = bitField;

            for (int i = 0; i < this.BitField.Length; i++)
            {
                if (this.BitField[i] != PieceStatus.Ignore)
                {
                    this.piecesCount++;
                }

                if (bitField[i] == PieceStatus.Present)
                {
                    this.presentPiecesCount++;
                }
            }

            this.checkouts = new Dictionary<int, DateTime>();

            // setup checkout timer
            this.timer = new System.Timers.Timer();
            this.timer.Interval = this.timerTimeout.TotalMilliseconds;
            this.timer.Elapsed += this.Timer_Elapsed;
            this.timer.Enabled = true;
            this.timer.Start();
        }
        private PieceManager()
        {
        }
        public event EventHandler<PieceCompletedEventArgs> PieceCompleted;
        public event EventHandler<PieceRequestedEventArgs> PieceRequested;
        public PieceStatus[] BitField
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
        public decimal CompletedPercentage
        {
            get;
            private set;
        }
        public bool IsComplete
        {
            get
            {
                this.CheckIfObjectIsDisposed();

                return this.presentPiecesCount == this.piecesCount;
            }
        }
        public bool IsDisposed
        {
            get;
            private set;
        }
        public bool IsEndGame
        {
            get
            {
                this.CheckIfObjectIsDisposed();

                return this.CompletedPercentage >= 0.95m;
            }
        }
        public int PieceCount
        {
            get
            {
                this.CheckIfObjectIsDisposed();

                return this.BitField.Length;
            }
        }
        public long PieceLength
        {
            get;
            private set;
        }
        public string TorrentInfoHash
        {
            get;
            private set;
        }
        public long TorrentLength
        {
            get;
            private set;
        }
        public Piece CheckOut(int pieceIndex, byte[] pieceData = null, bool[] bitField = null)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThanOrEqualTo(this.PieceCount);
            pieceData.IsNotNull().Then(() => pieceData.LongLength.MustBeEqualTo(this.GetPieceLength(pieceIndex)));
            bitField.IsNotNull().Then(() => bitField.Length.MustBeEqualTo(this.GetBlockCount(pieceIndex)));

            this.CheckIfObjectIsDisposed();

            Piece piece = null;
            string hash;
            long pieceLength = this.PieceLength;
            int blockCount = this.BlockCount;

            if (pieceData != null)
            {
                Array.Clear(pieceData, 0, pieceData.Length);
            }

            if (bitField != null)
            {
                Array.Clear(bitField, 0, bitField.Length);
            }

            lock (this.locker)
            {
                // only missing pieces can be checked out
                if (this.BitField[pieceIndex] == PieceStatus.Missing ||
                    (this.BitField[pieceIndex] == PieceStatus.CheckedOut &&
                     this.IsEndGame))
                {
                    hash = this.pieceHashes.ElementAt(pieceIndex);
                    pieceLength = this.GetPieceLength(pieceIndex);
                    blockCount = this.GetBlockCount(pieceIndex);

                    piece = new Piece(pieceIndex, hash, pieceLength, this.BlockLength, blockCount, pieceData, bitField);
                    piece.Completed += this.Piece_Completed;
                    piece.Corrupted += this.Piece_Corrupted;

                    this.BitField[pieceIndex] = PieceStatus.CheckedOut;

                    if (!this.checkouts.ContainsKey(pieceIndex))
                    {
                        this.checkouts.Add(pieceIndex, DateTime.UtcNow);
                    }
                }
            }

            return piece;
        }
        public void Dispose()
        {
            this.CheckIfObjectIsDisposed();

            if (this.timer != null)
            {
                this.timer.Stop();
                this.timer.Enabled = false;
                this.timer.Dispose();
                this.timer = null;
            }
        }
        public int GetBlockCount(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThan(this.PieceCount);

            this.CheckIfObjectIsDisposed();

            long pieceLength = this.GetPieceLength(pieceIndex);
            long blockLength = this.BlockLength;
            long remainder = pieceLength % blockLength;
            long blockCount;

            blockCount = (pieceLength - remainder) / blockLength;
            blockCount += remainder > 0 ? 1 : 0;

            return (int)blockCount;
        }
        public int GetBlockLength(int pieceIndex, int blockIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThan(this.PieceCount);
            blockIndex.MustBeGreaterThanOrEqualTo(0);
            blockIndex.MustBeLessThan(this.BlockCount);

            long pieceLength;
            long blockCount;

            this.CheckIfObjectIsDisposed();

            blockCount = this.GetBlockCount(pieceIndex);

            if (blockIndex == blockCount - 1)
            {
                pieceLength = this.GetPieceLength(pieceIndex);

                if (pieceLength % this.BlockLength != 0)
                {
                    // last block can be shorter
                    return (int)(pieceLength % this.BlockLength);
                }
            }

            return this.BlockLength;
        }
        public Piece GetPiece(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThan(this.PieceCount);

            this.CheckIfObjectIsDisposed();

            PieceRequestedEventArgs e = new PieceRequestedEventArgs(pieceIndex);

            this.OnPieceRequested(this, e);

            if (e.PieceData != null)
            {
                return new Piece(pieceIndex, this.pieceHashes.ElementAt(pieceIndex), this.PieceLength, this.BlockLength, this.BlockCount);
            }
            else
            {
                return null;
            }
        }
        public long GetPieceLength(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThan(this.PieceCount);

            this.CheckIfObjectIsDisposed();

            if (pieceIndex == this.PieceCount - 1)
            {
                if (this.TorrentLength % this.PieceLength != 0)
                {
                    // last piece can be shorter
                    return this.TorrentLength % this.PieceLength;
                }
            }

            return this.PieceLength;
        }
        private void CheckIfObjectIsDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }
        private void OnPieceCompleted(object sender, PieceCompletedEventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.PieceCompleted != null)
            {
                this.PieceCompleted(sender, e);
            }
        }
        private void OnPieceRequested(object sender, PieceRequestedEventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.PieceRequested != null)
            {
                this.PieceRequested(sender, e);
            }
        }
        private void Piece_Completed(object sender, PieceCompletedEventArgs e)
        {
            lock (this.locker)
            {
                // only pieces not yet downloaded can be checked in
                if (this.BitField[e.PieceIndex] == PieceStatus.Missing ||
                    this.BitField[e.PieceIndex] == PieceStatus.CheckedOut)
                {
                    this.BitField[e.PieceIndex] = PieceStatus.Present;

                    this.presentPiecesCount++;
                    this.CompletedPercentage = (decimal)this.presentPiecesCount / (decimal)this.piecesCount;

                    if (this.checkouts.ContainsKey(e.PieceIndex))
                    {
                        this.checkouts.Remove(e.PieceIndex);
                    }

                    this.OnPieceCompleted(this, new PieceCompletedEventArgs(e.PieceIndex, e.PieceData));
                }
            }
        }
        private void Piece_Corrupted(object sender, EventArgs e)
        {
            // Ignore
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DateTime checkoutTime;
            int pieceIndex;
            HashSet<int> checkoutsToRemove = new HashSet<int>();

            Thread.CurrentThread.Name = "piece manager checker";

            this.timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;

            lock (this.locker)
            {
                foreach (var checkOut in this.checkouts)
                {
                    pieceIndex = checkOut.Key;
                    checkoutTime = checkOut.Value;

                    if (DateTime.UtcNow - checkoutTime > this.checkoutTimeout)
                    {
                        checkoutsToRemove.Add(checkOut.Key);
                    }
                }

                foreach (var checkoutToRemove in checkoutsToRemove)
                {
                    this.checkouts.Remove(checkoutToRemove);

                    // checkout timeout -> mark piece as missing, giving other peers a chance to download it
                    this.BitField[checkoutToRemove] = PieceStatus.Missing;
                }
            }

            this.timer.Interval = this.timerTimeout.TotalMilliseconds;
        }
    }
}
