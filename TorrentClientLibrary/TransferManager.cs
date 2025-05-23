using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Http;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp;
// Використовуємо повний шлях до TrackingEvent, щоб уникнути неоднозначності
using UdpTrackingEvent = TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages.TrackingEvent;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TransferManager : IDisposable
    {
        private long downloaded = 0;
        private IDictionary<IPEndPoint, Peer> peers = new Dictionary<IPEndPoint, Peer>();
        private PersistenceManager persistenceManager;
        public PieceManager pieceManager;
        private ThrottlingManager throttlingManager;
        private IDictionary<Uri, Tracker> trackers = new Dictionary<Uri, Tracker>();
        private long uploaded = 0;

        private readonly object peersLock = new object();
        private readonly object trackersLock = new object();

        // Подія для сповіщення про критичні помилки на рівні TransferManager
        public event EventHandler<TrackingFailedEventArgs> TransferManagerFailed;


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
                    tracker.TrackingEvent = UdpTrackingEvent.Started;
                    tracker.Announcing += this.Tracker_Announcing;
                    tracker.Announced += this.Tracker_Announced;
                    tracker.TrackingFailed += this.Tracker_TrackingFailed; // Підписуємось на подію від Tracker
                    tracker.BytesLeftToDownload = this.TorrentInfo.Length - this.Downloaded;
                    tracker.WantedPeerCount = 30;

                    this.trackers.Add(trackerUri, tracker);
                }
                else
                {
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
                return this.pieceManager?.CompletedPercentage ?? 0m;
            }
        }

        public long Downloaded
        {
            get
            {
                lock (this.peersLock)
                {
                    return this.downloaded + (this.peers?.Values.Sum(x => x.Downloaded) ?? 0);
                }
            }
        }

        public decimal DownloadSpeed
        {
            get
            {
                lock (this.peersLock)
                {
                    return this.peers?.Values.Sum(x => x.DownloadSpeed) ?? 0m;
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

                lock (this.peersLock)
                {
                    return this.peers?.Values.Count(x => x.LeechingState == LeechingState.Interested) ?? 0;
                }
            }
        }

        public int PeerCount
        {
            get
            {
                this.CheckIfObjectIsDisposed();

                lock (this.peersLock)
                {
                    return this.peers?.Count ?? 0;
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

                lock (this.peersLock)
                {
                    return this.peers?.Values.Count(x => x.SeedingState == SeedingState.Unchoked) ?? 0;
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
                lock (this.peersLock)
                {
                    return this.uploaded + (this.peers?.Values.Sum(x => x.Uploaded) ?? 0);
                }
            }
        }

        public decimal UploadSpeed
        {
            get
            {
                lock (this.peersLock)
                {
                    return this.peers?.Values.Sum(x => x.UploadSpeed) ?? 0m;
                }
            }
        }

        public void AddLeecher(TcpClient tcp, string peerId)
        {
            tcp.CannotBeNull();
            peerId.CannotBeNull();

            Peer peer;
            int maxLeechers = 10;

            lock (this.peersLock)
            {
                if (this.IsDisposed || this.peers == null)
                {
                    tcp.Close();
                    return;
                }

                var remoteEndPoint = tcp.Client.RemoteEndPoint as IPEndPoint;
                if (remoteEndPoint == null)
                {
                    tcp.Close(); // Неможливо отримати ендпоінт
                    return;
                }


                if (!this.peers.ContainsKey(remoteEndPoint))
                {
                    // LeechingPeerCount використовує той самий peersLock, тому додаткове блокування не потрібне
                    if (this.LeechingPeerCount < maxLeechers)
                    {
                        TorrentInfo localTorrentInfo = this.TorrentInfo;
                        PieceManager localPieceManager = this.pieceManager;
                        ThrottlingManager localThrottlingManager = this.throttlingManager;

                        if (localTorrentInfo == null || localPieceManager == null || localThrottlingManager == null)
                        {
                            Debug.WriteLine($"Cannot add leecher {remoteEndPoint}, critical manager components are null.");
                            tcp.Close();
                            return;
                        }
                        Debug.WriteLine($"adding leeching peer {remoteEndPoint} to torrent {localTorrentInfo.InfoHash}");

                        tcp.ReceiveBufferSize = (int)Math.Max(localTorrentInfo.BlockLength, localTorrentInfo.PieceHashes.Count()) + 100;
                        tcp.SendBufferSize = tcp.ReceiveBufferSize;
                        tcp.Client.ReceiveTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
                        tcp.Client.SendTimeout = tcp.Client.ReceiveTimeout;
                        
                        peer = new Peer(new PeerCommunicator(localThrottlingManager, tcp), localPieceManager, this.PeerId, peerId);
                        peer.CommunicationErrorOccurred += this.Peer_CommunicationErrorOccurred;

                        this.peers.Add(remoteEndPoint, peer);
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

                string infoHashForDebug = "UNKNOWN (potentially disposed)";
                try { infoHashForDebug = this.TorrentInfo?.InfoHash ?? infoHashForDebug; } catch { /* Ignore */ }
                Debug.WriteLine($"disposing torrent manager for torrent {infoHashForDebug}");

                this.Stop();

                lock (this.trackersLock)
                {
                    if (this.trackers != null)
                    {
                        this.trackers.Clear();
                        this.trackers = null;
                    }
                }

                lock (this.peersLock)
                {
                    this.peers = null;
                }
                
                this.pieceManager = null; 

                PersistenceManager localPersistenceManager = this.persistenceManager;
                this.persistenceManager = null;
                if (localPersistenceManager != null)
                {
                    try
                    {
                        localPersistenceManager.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error disposing persistenceManager: {ex.Message}");
                    }
                }
            }
        }

        public void Start()
        {
            this.CheckIfObjectIsDisposed();

            this.StartTime = DateTime.UtcNow;
            
            TorrentInfo localTorrentInfo = this.TorrentInfo;
            if (localTorrentInfo == null) {
                 Debug.WriteLine($"Cannot start torrent manager: TorrentInfo is null.");
                 // Викликаємо нову подію TransferManagerFailed
                 TransferManagerFailed?.Invoke(this, new TrackingFailedEventArgs(null, "TorrentInfo is not initialized."));
                 return;
            }

            PersistenceManager localPersistenceManager = this.persistenceManager;
            if (localPersistenceManager == null)
            {
                Debug.WriteLine($"Cannot start torrent manager for torrent {localTorrentInfo.InfoHash}: persistenceManager is null.");
                TransferManagerFailed?.Invoke(this, new TrackingFailedEventArgs(null, "Persistence manager is not initialized."));
                return;
            }
            
            Debug.WriteLine($"starting torrent manager for torrent {localTorrentInfo.InfoHash}");

            OnTorrentHashing(this, EventArgs.Empty);
            
            PieceStatus[] verifiedPieces;
            try
            {
                verifiedPieces = localPersistenceManager.Verify();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error verifying pieces for torrent {localTorrentInfo.InfoHash}: {ex.Message}");
                TransferManagerFailed?.Invoke(this, new TrackingFailedEventArgs(null, $"Error verifying local files: {ex.Message}"));
                return;
            }

            this.pieceManager = new PieceManager(localTorrentInfo.InfoHash, localTorrentInfo.Length, localTorrentInfo.PieceHashes, localTorrentInfo.PieceLength, localTorrentInfo.BlockLength, verifiedPieces);
            this.pieceManager.PieceCompleted += this.PieceManager_PieceCompleted;
            this.pieceManager.PieceRequested += this.PieceManager_PieceRequested;
            
            lock (this.trackersLock)
            {
                if (this.trackers != null)
                {
                    foreach (var tracker in this.trackers.Values)
                    {
                        tracker.StartTracking();
                    }
                }
            }

            OnTorrentStarted(this, EventArgs.Empty);

            if (this.pieceManager.IsComplete)
            {
                OnTorrentSeeding(this, EventArgs.Empty);
            }
            else
            {
                OnTorrentLeeching(this, EventArgs.Empty);
            }
        }

        public void Stop()
        {
            string infoHashForDebug = "UNKNOWN";
            if (!this.IsDisposed && this.TorrentInfo != null) {
                infoHashForDebug = this.TorrentInfo.InfoHash;
            }
            Debug.WriteLine($"stopping torrent manager for torrent {infoHashForDebug} (IsDisposed: {this.IsDisposed})");

            List<Peer> peersToProcess = new List<Peer>();
            lock (this.peersLock)
            {
                if (this.peers != null)
                {
                    peersToProcess.AddRange(this.peers.Values);
                    this.peers.Clear();
                }
            }
            foreach (var peer in peersToProcess)
            {
                try { peer.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Error disposing peer {peer.Endpoint}: {ex.Message}"); }
                this.downloaded += peer.Downloaded;
                this.uploaded += peer.Uploaded;
            }

            List<Tracker> trackersToProcess = new List<Tracker>();
            lock (this.trackersLock)
            {
                if (this.trackers != null)
                {
                    trackersToProcess.AddRange(this.trackers.Values);
                }
            }
            foreach (var tracker in trackersToProcess)
            {
                try { tracker.StopTracking(); } catch (Exception ex) { Debug.WriteLine($"Error stopping tracker {tracker.TrackerUri}: {ex.Message}"); }
            }

            OnTorrentStopped(this, EventArgs.Empty);

            PieceManager localPieceManager = this.pieceManager;
            if (localPieceManager != null)
            {
                try { localPieceManager.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Error disposing pieceManager: {ex.Message}"); }
            }
        }
        
        private void AddSeeder(IPEndPoint endpoint)
        {
            endpoint.CannotBeNull();
            TcpClient tcp = null;

            if (this.IsDisposed) {
                 Debug.WriteLine($"AddSeeder: TransferManager for {endpoint} is disposed. Aborting.");
                 return;
            }

            TorrentInfo localTorrentInfo = this.TorrentInfo;
            if (localTorrentInfo == null && !this.IsDisposed)
            {
                 Debug.WriteLine($"AddSeeder: TorrentInfo is null for {endpoint} but manager is not disposed. Aborting seeder add.");
                 return;
            }

            lock (this.peersLock)
            {
                if (this.IsDisposed || this.peers == null)
                {
                    Debug.WriteLine($"AddSeeder: TransferManager is disposed or peers collection is null for {endpoint} (inside lock). Aborting.");
                    return;
                }
                
                if (localTorrentInfo == null) { // Повторна перевірка всередині локу (малоймовірно, але безпечніше)
                     Debug.WriteLine($"AddSeeder: localTorrentInfo became null for {endpoint} (inside lock). Aborting.");
                     return;
                }

                if (!this.peers.ContainsKey(endpoint))
                {
                    tcp = new TcpClient();
                    try
                    {
                        tcp.ReceiveBufferSize = (int)Math.Max(localTorrentInfo.BlockLength, localTorrentInfo.PieceHashes.Count()) + 100;
                        tcp.SendBufferSize = tcp.ReceiveBufferSize;
                        tcp.Client.ReceiveTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
                        tcp.Client.SendTimeout = tcp.Client.ReceiveTimeout;
                        
                        var asyncConnectData = new AsyncConnectData(endpoint, tcp);
                        tcp.BeginConnect(endpoint.Address, endpoint.Port, this.PeerConnected, asyncConnectData);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AddSeeder: Exception during BeginConnect setup or call for {endpoint}. Message: {ex.Message}");
                        tcp?.Close();
                    }
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
            e.CannotBeNull();
            TorrentHashing?.Invoke(sender, e);
        }

        private void OnTorrentLeeching(object sender, EventArgs e)
        {
            e.CannotBeNull();
            TorrentLeeching?.Invoke(sender, e);
        }

        private void OnTorrentSeeding(object sender, EventArgs e)
        {
            e.CannotBeNull();
            TorrentSeeding?.Invoke(sender, e);
        }

        private void OnTorrentStarted(object sender, EventArgs e)
        {
            e.CannotBeNull();
            TorrentStarted?.Invoke(sender, e);
        }

        private void OnTorrentStopped(object sender, EventArgs e)
        {
            e.CannotBeNull();
            TorrentStopped?.Invoke(sender, e);
        }
        
        private void Peer_CommunicationErrorOccurred(object sender, PeerCommunicationErrorEventArgs e)
        {
            Peer peer = sender as Peer;
            if (peer == null) {
                 Debug.WriteLine($"Peer_CommunicationErrorOccurred: Sender is not a Peer object.");
                 return;
            }
             TorrentInfo localTorrentInfo = this.TorrentInfo; // Для безпечного логування
             string infoHash = localTorrentInfo?.InfoHash ?? "UNKNOWN";


            if (e.IsFatal)
            {
                Debug.WriteLine($"fatal communication error occurred for peer {peer.Endpoint} on torrent {infoHash}: {e.ErrorMessage}");

                bool removed = false;
                lock (this.peersLock)
                {
                    if (this.IsDisposed || this.peers == null) return;

                    this.downloaded += peer.Downloaded;
                    this.uploaded += peer.Uploaded;
                    
                    if (this.peers.ContainsKey(peer.Endpoint))
                    {
                        removed = this.peers.Remove(peer.Endpoint);
                    }
                }
                if(removed) {
                    try { peer.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Error disposing peer {peer.Endpoint} after fatal error: {ex.Message}");}
                }
            }
            else
            {
                Debug.WriteLine($"communication error occurred for peer {peer.Endpoint} on torrent {infoHash}: {e.ErrorMessage}");
            }
        }

        private void PeerConnected(IAsyncResult ar)
        {
            AsyncConnectData data = ar.AsyncState as AsyncConnectData;
            if (data == null)
            {
                Debug.WriteLine("PeerConnected: AsyncState is null or not AsyncConnectData. Aborting callback.");
                return;
            }

            IPEndPoint endpoint = data.Endpoint;
            TcpClient tcp = data.Tcp;

            if (this.IsDisposed)
            {
                tcp?.Close();
                Debug.WriteLine($"PeerConnected: TransferManager for {endpoint} is already disposed. Closing connection.");
                return;
            }

            try
            {
                tcp.EndConnect(ar);

                if (this.IsDisposed)
                {
                    tcp?.Close();
                    Debug.WriteLine($"PeerConnected: TransferManager for {endpoint} disposed after EndConnect. Closing connection.");
                    return;
                }
                
                var localPieceManager = this.pieceManager;
                var localTorrentInfo = this.TorrentInfo;
                var localThrottlingManager = this.throttlingManager;

                if (localPieceManager == null || localTorrentInfo == null || localThrottlingManager == null)
                {
                    Debug.WriteLine($"PeerConnected: Critical members (pieceManager, TorrentInfo, or throttlingManager) are null for {endpoint}. Aborting peer add.");
                    tcp?.Close();
                    return;
                }

                lock (this.peersLock)
                {
                    if (this.IsDisposed || this.peers == null)
                    {
                        tcp?.Close();
                        Debug.WriteLine($"PeerConnected: TransferManager for {endpoint} disposed or peers collection is null before adding. Closing connection.");
                        return;
                    }

                    if (this.peers.ContainsKey(endpoint))
                    {
                        tcp.Close();
                    }
                    else
                    {
                        Debug.WriteLine($"adding seeding peer {endpoint} to torrent {localTorrentInfo.InfoHash}");

                        var peerCommunicator = new PeerCommunicator(localThrottlingManager, tcp);
                        var peer = new Peer(peerCommunicator, localPieceManager, this.PeerId);
                        peer.CommunicationErrorOccurred += this.Peer_CommunicationErrorOccurred;

                        this.peers.Add(endpoint, peer);
                    }
                }
            }
            catch (ObjectDisposedException ode) 
            {
                Debug.WriteLine($"PeerConnected: ObjectDisposedException for {endpoint}. TcpClient or related object was disposed. Message: {ode.Message}");
            }
            catch (SocketException se)
            {
                Debug.WriteLine($"PeerConnected: SocketException for {endpoint} (e.g., connection refused). Message: {se.Message}");
                tcp?.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PeerConnected: Unhandled Exception for {endpoint}. Type: {ex.GetType().FullName}. Message: {ex.Message}. StackTrace: {ex.StackTrace}");
                tcp?.Close();
            }
        }
        
        private void PieceManager_PieceCompleted(object sender, PieceCompletedEventArgs e)
        {
            if (this.IsDisposed)
            {
                Debug.WriteLine($"TransferManager is disposed. Ignoring PieceCompleted event for torrent {this.TorrentInfo?.InfoHash}.");
                return;
            }
            
            var currentPieceManager = this.pieceManager; 
            var currentPersistenceManager = this.persistenceManager;
            var currentTorrentInfo = this.TorrentInfo;

            if (currentPieceManager == null || currentPersistenceManager == null || currentTorrentInfo == null)
            {
                Debug.WriteLine($"PieceManager_PieceCompleted: Critical component is null for torrent {currentTorrentInfo?.InfoHash}. Piece may have completed during stop/dispose. Ignoring.");
                return;
            }
            
            Debug.WriteLine($"piece {e.PieceIndex} completed for torrent {currentTorrentInfo.InfoHash}");

            try
            {
                 currentPersistenceManager.Put(currentTorrentInfo.Files, currentTorrentInfo.PieceLength, e.PieceIndex, e.PieceData);
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Error in PieceManager_PieceCompleted while putting piece {e.PieceIndex} for torrent {currentTorrentInfo.InfoHash}: {ex.Message}");
                return;
            }

            if (currentPieceManager.IsComplete)
            {
                OnTorrentSeeding(this, EventArgs.Empty);
            }
            else
            {
                OnTorrentLeeching(this, EventArgs.Empty);
            }
        }

        private void PieceManager_PieceRequested(object sender, PieceRequestedEventArgs e)
        {
            if (this.IsDisposed)
            {
                Debug.WriteLine($"TransferManager is disposed. Ignoring PieceRequested event for torrent {this.TorrentInfo?.InfoHash}.");
                e.PieceData = null;
                return;
            }

            var currentPersistenceManager = this.persistenceManager;
            var currentTorrentInfo = this.TorrentInfo;

            if (currentPersistenceManager == null || currentTorrentInfo == null)
            {
                 Debug.WriteLine($"PieceManager_PieceRequested: persistenceManager or TorrentInfo is null for torrent {currentTorrentInfo?.InfoHash}. Cannot get piece data.");
                 e.PieceData = null;
                 return;
            }
            
            Debug.WriteLine($"piece {e.PieceIndex} requested for torrent {currentTorrentInfo.InfoHash}");
            
            try
            {
                e.PieceData = currentPersistenceManager.Get(e.PieceIndex);
            }
            catch (Exception ex)
            {
                 Debug.WriteLine($"Error in PieceManager_PieceRequested while getting piece {e.PieceIndex} for torrent {currentTorrentInfo.InfoHash}: {ex.Message}");
                 e.PieceData = null;
            }
        }
        
        private void Tracker_Announced(object sender, AnnouncedEventArgs e)
        {
            if (this.IsDisposed) return;

            lock (this.peersLock)
            {
                if (this.IsDisposed || this.peers == null) return;

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
             if (this.IsDisposed) return;

            Tracker tracker = sender as Tracker;
            if (tracker == null) return;

            PieceManager localPieceManager = this.pieceManager;
            TorrentInfo localTorrentInfo = this.TorrentInfo;

            if (localPieceManager == null || localTorrentInfo == null) {
                Debug.WriteLine($"Tracker_Announcing: pieceManager or TorrentInfo is null. Cannot update tracker {tracker.TrackerUri}.");
                return;
            }

            tracker.BytesDownloaded = this.Downloaded;
            tracker.BytesLeftToDownload = localTorrentInfo.Length - this.Downloaded;
            tracker.BytesUploaded = this.Uploaded;
            
            tracker.TrackingEvent = localPieceManager.IsComplete ? 
                UdpTrackingEvent.Completed : 
                UdpTrackingEvent.Started;
        }

        // Цей метод викликається, коли Tracker повідомляє про помилку.
        // Він вже підписаний у конструкторі: tracker.TrackingFailed += this.Tracker_TrackingFailed;
        private void Tracker_TrackingFailed(object sender, TrackingFailedEventArgs e)
        {
            if (this.IsDisposed) return;
            
            TorrentInfo localTorrentInfo = this.TorrentInfo;
            string infoHash = localTorrentInfo?.InfoHash ?? "UNKNOWN";

            Debug.WriteLine($"tracking failed for tracker {e.TrackerUri} for torrent {infoHash}: \"{e.FailureReason}\"");
            
            Tracker trackerToDispose = sender as Tracker;
            if (trackerToDispose != null) {
                lock(this.trackersLock) {
                    // Перевіряємо, чи трекер все ще в колекції, перш ніж намагатися його видалити або звільнити
                    if (this.trackers != null && this.trackers.Values.Contains(trackerToDispose)) { // Змінено ContainsValue на LINQ Contains
                        // Можна розглянути видалення трекера з колекції this.trackers тут, якщо це доречно
                        // this.trackers.Remove(trackerToDispose.TrackerUri); // Якщо TrackerUri є ключем
                    }
                }
                try {
                     trackerToDispose.Dispose();
                } catch (Exception ex) {
                    Debug.WriteLine($"Error disposing tracker {e.TrackerUri} after tracking failed: {ex.Message}");
                }
            }
        }
    }
}