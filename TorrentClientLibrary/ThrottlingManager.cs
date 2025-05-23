using System;
using System.Diagnostics;
using System.Threading;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class ThrottlingManager
    {
        private decimal minReadTime;
        private decimal minWriteTime;
        private long read = 0;
        private decimal readDelta = 0;
        private object readingLocker = new object();
        private long readLimit;
        private Stopwatch readStopwatch = new Stopwatch();
        private decimal writeDelta = 0;
        private long writeLimit;
        private Stopwatch writeStopwatch = new Stopwatch();
        private object writingLocker = new object();
        private long written = 0;
        public ThrottlingManager()
        {
            this.ReadSpeedLimit = int.MaxValue;
            this.WriteSpeedLimit = int.MaxValue;
        }
        public decimal ReadSpeed
        {
            get;
            private set;
        }
        public long ReadSpeedLimit
        {
            get
            {
                return this.readLimit;
            }

            set
            {
                value.MustBeGreaterThan(0);

                this.readLimit = value;
                this.readDelta = value;

                this.minReadTime = this.CalculateMinExecutionTime(value);
            }
        }
        public decimal WriteSpeed
        {
            get;
            private set;
        }
        public long WriteSpeedLimit
        {
            get
            {
                return this.writeLimit;
            }

            set
            {
                value.MustBeGreaterThan(0);

                this.writeLimit = value;
                this.writeDelta = value;

                this.minWriteTime = this.CalculateMinExecutionTime(value);
            }
        }
        public void Read(long bytesRead)
        {
            bytesRead.MustBeGreaterThanOrEqualTo(0);

            decimal wait;

            lock (this.readingLocker)
            {
                if (!this.readStopwatch.IsRunning)
                {
                    this.readStopwatch.Start();
                }

                this.read += bytesRead;

                if (this.read > this.readDelta)
                {
                    this.readStopwatch.Stop();

                    this.ReadSpeed = this.read / (decimal)this.readStopwatch.Elapsed.TotalSeconds;

                    wait = (this.read / this.readDelta) * this.minReadTime;
                    wait = wait - this.readStopwatch.ElapsedMilliseconds;

                    if (wait > 0)
                    {
                        Thread.Sleep((int)Math.Round(wait));
                    }

                    this.read = 0;
                    this.readStopwatch.Restart();
                }
            }
        }
        public void Write(long bytesWritten)
        {
            bytesWritten.MustBeGreaterThanOrEqualTo(0);

            decimal wait;

            lock (this.writingLocker)
            {
                if (!this.writeStopwatch.IsRunning)
                {
                    this.writeStopwatch.Start();
                }

                this.written += bytesWritten;

                if (this.written > this.writeDelta)
                {
                    this.writeStopwatch.Stop();

                    this.WriteSpeed = this.written / (decimal)this.writeStopwatch.Elapsed.TotalSeconds;

                    wait = (this.written / this.writeDelta) * this.minWriteTime;
                    wait = wait - this.writeStopwatch.ElapsedMilliseconds;

                    if (wait > 0)
                    {
                        Thread.Sleep((int)Math.Round(wait));
                    }

                    this.written = 0;
                    this.writeStopwatch.Restart();
                }
            }
        }
        private decimal CalculateMinExecutionTime(decimal speed)
        {
            return 1000m * this.readDelta / speed;
        }
    }
}
