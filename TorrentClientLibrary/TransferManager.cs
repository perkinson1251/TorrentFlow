using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Http;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TransferManager : IDisposable
    {
        private long downloaded = 0;
        private Dictionary<IPEndPoint, Peer> peers = new Dictionary<IPEndPoint, Peer>();
        private PersistenceManager persistenceManager;
        private PieceManager pieceManager;
        private ThrottlingManager throttlingManager;
        private IDictionary<Uri, Tracker> trackers = new Dictionary<Uri, Tracker>();
        private long uploaded = 0;
        public TransferManager(int listeningPort, TorrentInfo torrentInfo, ThrottlingManager throttlingManager, PersistenceManager persistenceManager)
        {
            listeningPort.MustBeGreaterThanOrEqualTo(IPEndPoint.MinPort);
            listeningPort.MustBeLessThanOrEqualTo(IPEndPoint.MaxPort);
            torrentInfo.CannotBeNull();
            throttlingManager.CannotBeNull();
            persistenceManager.CannotBeNull();

            Tracker tracker = null;

            this.PeerId = "-AB1100-" + "0123456789ABCDEF".Random(24);

            Debug.WriteLine($"creating torrent manager for torrent {torrentInfo.InfoHash}");
            Debug.WriteLine($"local peer id {this.PeerId}");

            this.TorrentInfo = torrentInfo;

            this.throttlingManager = throttlingManager;

            this.persistenceManager = persistenceManager;

            // initialize trackers
            foreach (var trackerUri in torrentInfo.AnnounceList)
            {
                if (trackerUri.Scheme == "http" ||
                    trackerUri.Scheme == "https")
                {
                    tracker = new HttpTracker(trackerUri, this.PeerId, torrentInfo.InfoHash, listeningPort);
                }
                else if (trackerUri.Scheme == "udp")
                {
                    tracker = new UdpTracker(trackerUri, this.PeerId, torrentInfo.InfoHash, listeningPort);
                }

                if (tracker != null)
                {
                    tracker.TrackingEvent = TrackingEvent.Started;
                    tracker.Announcing += this.Tracker_Announcing;
                    tracker.Announced += this.Tracker_Announced;
                    tracker.TrackingFailed += this.Tracker_TrackingFailed;
                    tracker.BytesLeftToDownload = this.TorrentInfo.Length - this.Downloaded;
                    tracker.WantedPeerCount = 30; // we can handle 30 peers at a time

                    this.trackers.Add(trackerUri, tracker);
                }
                else
                {
                    // unsupported tracker protocol
                    Debug.WriteLine($"unsupported tracker protocol {trackerUri.Scheme}");
                }
            }
        }
        private TransferManager()
        {
        }
        public event EventHandler<EventArgs> TorrentHashing;
        public event EventHandler<EventArgs> TorrentLeeching;
        public event EventHandler<EventArgs> TorrentSeeding;
        public event EventHandler<EventArgs> TorrentStarted;
        public event EventHandler<EventArgs> TorrentStopped;
        public decimal CompletedPercentage
        {
            get
            {
                return this.pieceManager.CompletedPercentage;
            }
        }
        public long Downloaded
        {
            get
            {
                lock (((IDictionary)this.peers).SyncRoot)
                {
                    return this.downloaded + this.peers.Values.Sum(x => x.Downloaded);
                }
            }
        }
        public decimal DownloadSpeed
        {
            get
            {
                lock (((IDictionary)this.peers).SyncRoot)
                {
                    return this.peers.Values.Sum(x => x.DownloadSpeed);
                }
            }
        }
        public bool IsDisposed
        {
            get;
            private set;
        }
        public int LeechingPeerCount
        {
            get
            {
                this.CheckIfObjectIsDisposed();

                lock (((IDictionary)this.peers).SyncRoot)
                {
                    return this.peers.Values.Count(x => x.LeechingState == LeechingState.Interested);
                }
            }
        }
        public int PeerCount
        {
            get
            {
                this.CheckIfObjectIsDisposed();

                lock (((IDictionary)this.peers).SyncRoot)
                {
                    return this.peers.Count;
                }
            }
        }
        public string PeerId
        {
            get;
            private set;
        }
        public int SeedingPeerCount
        {
            get
            {
                this.CheckIfObjectIsDisposed();

                lock (((IDictionary)this.peers).SyncRoot)
                {
                    return this.peers.Values.Count(x => x.SeedingState == SeedingState.Unchoked);
                }
            }
        }
        public DateTime StartTime
        {
            get;
            private set;
        }
        public TorrentInfo TorrentInfo
        {
            get;
            private set;
        }
        public long Uploaded
        {
            get
            {
                lock (((IDictionary)this.peers).SyncRoot)
                {
                    return this.uploaded + this.peers.Values.Sum(x => x.Uploaded);
                }
            }
        }
        public decimal UploadSpeed
        {
            get
            {
                lock (((IDictionary)this.peers).SyncRoot)
                {
                    return this.peers.Values.Sum(x => x.UploadSpeed);
                }
            }
        }
        public void AddLeecher(TcpClient tcp, string peerId)
        {
            tcp.CannotBeNull();
            peerId.CannotBeNull();

            Peer peer;
            int maxLeechers = 10;

            lock (((IDictionary)this.peers).SyncRoot)
            {
                if (!this.peers.ContainsKey(tcp.Client.RemoteEndPoint as IPEndPoint))
                {
                    if (this.LeechingPeerCount < maxLeechers)
                    {
                        Debug.WriteLine($"adding leeching peer {tcp.Client.RemoteEndPoint} to torrent {this.TorrentInfo.InfoHash}");

                        // setup tcp client
                        tcp.ReceiveBufferSize = (int)Math.Max(this.TorrentInfo.BlockLength, this.TorrentInfo.PieceHashes.Count()) + 100;
                        tcp.SendBufferSize = tcp.ReceiveBufferSize;
                        tcp.Client.ReceiveTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
                        tcp.Client.SendTimeout = tcp.Client.ReceiveTimeout;

                        // add new peer
                        peer = new Peer(new PeerCommunicator(this.throttlingManager, tcp), this.pieceManager, this.PeerId, peerId);
                        peer.CommunicationErrorOccurred += this.Peer_CommunicationErrorOccurred;

                        this.peers.Add(tcp.Client.RemoteEndPoint as IPEndPoint, peer);
                    }
                    else
                    {
                        tcp.Close();
                    }
                }
                else
                {
                    tcp.Close();
                }
            }
        }
        public void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.IsDisposed = true;

                Debug.WriteLine($"disposing torrent manager for torrent {this.TorrentInfo.InfoHash}");

                this.Stop();

                lock (((IDictionary)this.trackers).SyncRoot)
                {
                    if (this.trackers != null)
                    {
                        this.trackers.Clear();
                        this.trackers = null;
                    }
                }

                lock (((IDictionary)this.peers).SyncRoot)
                {
                    if (this.peers != null)
                    {
                        this.peers.Clear();
                        this.peers = null;
                    }
                }

                if (this.pieceManager != null)
                {
                    this.pieceManager.Dispose();
                    this.pieceManager = null;
                }
            }
        }
        public void Start()
        {
            this.CheckIfObjectIsDisposed();

            this.StartTime = DateTime.UtcNow;

            Debug.WriteLine($"starting torrent manager for torrent {this.TorrentInfo.InfoHash}");

            this.OnTorrentHashing(this, EventArgs.Empty);

            // initialize piece manager
            this.pieceManager = new PieceManager(this.TorrentInfo.InfoHash, this.TorrentInfo.Length, this.TorrentInfo.PieceHashes, this.TorrentInfo.PieceLength, this.TorrentInfo.BlockLength, this.persistenceManager.Verify());
            this.pieceManager.PieceCompleted += this.PieceManager_PieceCompleted;
            this.pieceManager.PieceRequested += this.PieceManager_PieceRequested;

            // start tracking
            lock (((IDictionary)this.trackers).SyncRoot)
            {
                foreach (var tracker in this.trackers.Values)
                {
                    tracker.StartTracking();
                }
            }

            this.OnTorrentStarted(this, EventArgs.Empty);

            if (this.pieceManager.IsComplete)
            {
                this.OnTorrentSeeding(this, EventArgs.Empty);
            }
        }
        public void Stop()
        {
            this.CheckIfObjectIsDisposed();

            Debug.WriteLine($"stopping torrent manager for torrent {this.TorrentInfo.InfoHash}");

            // stop peers
            lock (((IDictionary)this.peers).SyncRoot)
            {
                foreach (var peer in this.peers.Values)
                {
                    peer.Dispose();

                    this.downloaded += peer.Downloaded;
                    this.uploaded += peer.Uploaded;
                }

                this.peers.Clear();
            }

            // stop tracking
            lock (((IDictionary)this.trackers).SyncRoot)
            {
                foreach (var tracker in this.trackers.Values)
                {
                    tracker.StopTracking();
                }
            }

            this.OnTorrentStopped(this, EventArgs.Empty);

            this.pieceManager.Dispose();
            this.pieceManager = null;
        }
        private void AddSeeder(IPEndPoint endpoint)
        {
            endpoint.CannotBeNull();

            TcpClient tcp;

            lock (((IDictionary)this.peers).SyncRoot)
            {
                if (!this.peers.ContainsKey(endpoint))
                {
                    // set up tcp client
                    tcp = new TcpClient();
                    tcp.ReceiveBufferSize = (int)Math.Max(this.TorrentInfo.BlockLength, this.TorrentInfo.PieceHashes.Count()) + 100;
                    tcp.SendBufferSize = tcp.ReceiveBufferSize;
                    tcp.Client.ReceiveTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
                    tcp.Client.SendTimeout = tcp.Client.ReceiveTimeout;
                    tcp.BeginConnect(endpoint.Address, endpoint.Port, this.PeerConnected, new AsyncConnectData(endpoint, tcp));
                }
            }
        }
        private void CheckIfObjectIsDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }
        private void OnTorrentHashing(object sender, EventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.TorrentHashing != null)
            {
                this.TorrentHashing(sender, e);
            }
        }
        private void OnTorrentLeeching(object sender, EventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.TorrentLeeching != null)
            {
                this.TorrentLeeching(sender, e);
            }
        }
        private void OnTorrentSeeding(object sender, EventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.TorrentSeeding != null)
            {
                this.TorrentSeeding(sender, e);
            }
        }
        private void OnTorrentStarted(object sender, EventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.TorrentStarted != null)
            {
                this.TorrentStarted(sender, e);
            }
        }
        private void OnTorrentStopped(object sender, EventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.TorrentStopped != null)
            {
                this.TorrentStopped(sender, e);
            }
        }
        private void Peer_CommunicationErrorOccurred(object sender, PeerCommunicationErrorEventArgs e)
        {
            Peer peer;

            peer = sender as Peer;

            if (e.IsFatal)
            {
                Debug.WriteLine($"fatal communication error occurred for peer {peer.Endpoint} on torrent {this.TorrentInfo.InfoHash}: {e.ErrorMessage}");

                lock (((IDictionary)this.peers).SyncRoot)
                {
                    // update transfer parameters
                    this.downloaded += peer.Downloaded;
                    this.uploaded += peer.Uploaded;

                    // something is wrong with the peer -> remove it from the list and close the connection
                    if (this.peers.ContainsKey(peer.Endpoint))
                    {
                        this.peers.Remove(peer.Endpoint);
                    }

                    // dispose of the peer
                    peer.Dispose();
                }
            }
            else
            {
                Debug.WriteLine($"communication error occurred for peer {peer.Endpoint} on torrent {this.TorrentInfo.InfoHash}: {e.ErrorMessage}");
            }
        }
        private void PeerConnected(IAsyncResult ar)
        {
            AsyncConnectData data;
            TcpClient tcp;
            Peer peer;
            IPEndPoint endpoint;

            data = ar.AsyncState as AsyncConnectData;
            endpoint = data.Endpoint;

            try
            {
                tcp = data.Tcp;
                tcp.EndConnect(ar);

                lock (((IDictionary)this.peers).SyncRoot)
                {
                    if (this.peers.ContainsKey(endpoint))
                    {
                        // peer is already present
                        tcp.Close();
                        tcp = null;
                    }
                    else
                    {
                        Debug.WriteLine($"adding seeding peer {endpoint} to torrent {this.TorrentInfo.InfoHash}");

                        // add new peer
                        peer = new Peer(new PeerCommunicator(this.throttlingManager, tcp), this.pieceManager, this.PeerId);
                        peer.CommunicationErrorOccurred += this.Peer_CommunicationErrorOccurred;

                        this.peers.Add(endpoint, peer);
                    }
                }
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"could not connect to peer {endpoint}: {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"connection to peer {endpoint} was closed: {ex.Message}");
            }
        }
        private void PieceManager_PieceCompleted(object sender, PieceCompletedEventArgs e)
        {
            Debug.WriteLine($"piece {e.PieceIndex} completed for torrent {this.TorrentInfo.InfoHash}");

            // persist piece
            this.persistenceManager.Put(this.TorrentInfo.Files, this.TorrentInfo.PieceLength, e.PieceIndex, e.PieceData);

            if (this.pieceManager.CompletedPercentage == 1)
            {
                this.OnTorrentSeeding(this, EventArgs.Empty);
            }
            else
            {
                this.OnTorrentLeeching(this, EventArgs.Empty);
            }
        }
        private void PieceManager_PieceRequested(object sender, PieceRequestedEventArgs e)
        {
            Debug.WriteLine($"piece {e.PieceIndex} requested for torrent {this.TorrentInfo.InfoHash}");

            // get piece data
            e.PieceData = this.persistenceManager.Get(e.PieceIndex);
        }
        private void Tracker_Announced(object sender, AnnouncedEventArgs e)
        {
            lock (((IDictionary)this.peers).SyncRoot)
            {
                foreach (var endpoint in e.Peers)
                {
                    try
                    {
                        this.AddSeeder(endpoint);
                    }
                    catch (SocketException ex)
                    {
                        Debug.WriteLine($"could not connect to peer {endpoint}: {ex.Message}");
                    }
                }
            }
        }
        private void Tracker_Announcing(object sender, EventArgs e)
        {
            Tracker tracker;

            tracker = sender as Tracker;
            tracker.BytesDownloaded = this.Downloaded;
            tracker.BytesLeftToDownload = this.TorrentInfo.Length - this.Downloaded;
            tracker.BytesUploaded = this.Uploaded;
            tracker.TrackingEvent = this.CompletedPercentage == 1 ? TrackingEvent.Completed : TrackingEvent.Started;
        }
        private void Tracker_TrackingFailed(object sender, TrackingFailedEventArgs e)
        {
            Debug.WriteLine($"tracking failed for tracker {e.TrackerUri} for torrent {this.TorrentInfo.InfoHash}: \"{e.FailureReason}\"");

            sender.As<Tracker>().Dispose();
        }
    }
}
