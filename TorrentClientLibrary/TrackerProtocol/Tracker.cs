using System;
using System.Diagnostics;
using System.Net;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol
{
    public abstract class Tracker : IDisposable
    {
        private System.Timers.Timer timer;
        public Tracker(Uri trackerUri, string peerId, string torrentInfoHash, int listeningPort)
        {
            trackerUri.CannotBeNull();
            torrentInfoHash.CannotBeNullOrEmpty();
            torrentInfoHash.Length.MustBeEqualTo(40);
            peerId.CannotBeNullOrEmpty();
            peerId.Length.MustBeGreaterThanOrEqualTo(20);
            listeningPort.MustBeGreaterThanOrEqualTo(IPEndPoint.MinPort);
            listeningPort.MustBeGreaterThanOrEqualTo(IPEndPoint.MinPort);

            this.UpdateInterval = TimeSpan.FromMinutes(10);
            this.TrackerUri = trackerUri;
            this.TorrentInfoHash = torrentInfoHash;
            this.ListeningPort = listeningPort;
            this.PeerId = peerId;
        }
        public event EventHandler<AnnouncedEventArgs> Announced;
        public event EventHandler<EventArgs> Announcing;
        public event EventHandler<TrackingFailedEventArgs> TrackingFailed;
        public long BytesDownloaded
        {
            get;
            set;
        }
        public long BytesLeftToDownload
        {
            get;
            set;
        }
        public long BytesUploaded
        {
            get;
            set;
        }
        public bool IsDisposed
        {
            get;
            private set;
        }
        public int ListeningPort
        {
            get;
            private set;
        }
        public string PeerId
        {
            get;
            private set;
        }
        public string TorrentInfoHash
        {
            get;
            private set;
        }
        public Uri TrackerUri
        {
            get;
            private set;
        }
        public TrackingEvent TrackingEvent
        {
            get;
            set;
        }
        public TimeSpan UpdateInterval
        {
            get;
            protected set;
        }
        public int WantedPeerCount
        {
            get;
            set;
        }
        public virtual void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.IsDisposed = true;

                Debug.WriteLine("disposing tracker");

                this.StopTracking();
            }
        }
        public void StartTracking()
        {
            this.CheckIfObjectIsDisposed();

            Debug.WriteLine($"starting tracking {this.TrackerUri} for torrent { this.TorrentInfoHash}");

            this.OnStart();

            this.timer = new System.Timers.Timer();
            this.timer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            this.timer.Elapsed += this.Timer_Elapsed;
            this.timer.Enabled = true;
            this.timer.Start();
        }
        public void StopTracking()
        {
            this.CheckIfObjectIsDisposed();

            Debug.WriteLine($"stopping tracking {this.TrackerUri} for torrent {this.TorrentInfoHash}");

            if (this.timer != null)
            {
                this.timer.Stop();
                this.timer.Enabled = false;
                this.timer.Dispose();
                this.timer = null;
            }

            this.OnStop();
        }
        protected abstract void OnAnnounce();
        protected void OnAnnounced(object sender, AnnouncedEventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.Announced != null)
            {
                this.Announced(sender, e);
            }
        }
        protected void OnAnnouncing(object sender, EventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.Announcing != null)
            {
                this.Announcing(sender, e);
            }
        }
        protected abstract void OnStart();
        protected abstract void OnStop();
        private void CheckIfObjectIsDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }
        public void OnTrackingFailed(object sender, TrackingFailedEventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.TrackingFailed != null)
            {
                this.TrackingFailed(sender, e);
            }
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;

            this.OnAnnounce();

            this.timer.Interval = this.UpdateInterval.TotalMilliseconds;
        }
    }
}
