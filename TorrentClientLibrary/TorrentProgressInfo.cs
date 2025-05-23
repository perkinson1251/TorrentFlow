using System;
using System.Collections.Generic;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol;

namespace TorrentFlow.TorrentClientLibrary
{
    public class TorrentProgressInfo
    {
        public TorrentProgressInfo(string torrentInfoHash, TimeSpan duration, decimal completedPercentage, long downloaded, decimal downloadSpeed, long uploaded, decimal uploadSpeed, int leecherCount, int seederCount)
        {
            torrentInfoHash.CannotBeNullOrEmpty();
            duration.MustBeGreaterThanOrEqualTo(TimeSpan.Zero);
            completedPercentage.MustBeGreaterThanOrEqualTo(0);
            downloaded.MustBeGreaterThanOrEqualTo(0);
            downloadSpeed.MustBeGreaterThanOrEqualTo(0);
            uploaded.MustBeGreaterThanOrEqualTo(0);
            uploadSpeed.MustBeGreaterThanOrEqualTo(0);
            leecherCount.MustBeGreaterThanOrEqualTo(0);
            seederCount.MustBeGreaterThanOrEqualTo(0);

            this.TorrentInfoHash = torrentInfoHash;
            this.Duration = duration;
            this.CompletedPercentage = completedPercentage;
            this.Downloaded = downloaded;
            this.DownloadSpeed = downloadSpeed;
            this.Uploaded = uploaded;
            this.UploadSpeed = uploadSpeed;
            this.LeecherCount = leecherCount;
            this.SeederCount = seederCount;

            this.Trackers = new List<TorrentTrackerInfo>();
            this.Peers = new List<TorrentPeerInfo>();
            this.Pieces = new List<PieceStatus>();
            this.Files = new List<TorrentFileInfo>();
        }
        private TorrentProgressInfo()
        {
        }
        public decimal CompletedPercentage
        {
            get;
            private set;
        }
        public long Downloaded
        {
            get;
            private set;
        }
        public decimal DownloadSpeed
        {
            get;
            private set;
        }
        public TimeSpan Duration
        {
            get;
            private set;
        }
        public IEnumerable<TorrentFileInfo> Files
        {
            get;
            private set;
        }
        public int LeecherCount
        {
            get;
            private set;
        }
        public IEnumerable<TorrentPeerInfo> Peers
        {
            get;
            private set;
        }
        public List<PieceStatus> Pieces
        {
            get;
            private set;
        }
        public int SeederCount
        {
            get;
            private set;
        }
        public string TorrentInfoHash
        {
            get;
            private set;
        }
        public IEnumerable<TorrentTrackerInfo> Trackers
        {
            get;
            private set;
        }
        public long Uploaded
        {
            get;
            private set;
        }
        public decimal UploadSpeed
        {
            get;
            private set;
        }
    }
}
