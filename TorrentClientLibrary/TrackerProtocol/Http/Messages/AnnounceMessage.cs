using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Http.Messages
{
    public class AnnounceMessage
    {
        public AnnounceMessage(string infohash, string peerId, int port, long bytesUploaded, long bytesDownloaded, long bytesLeft, int peersWantedCount, TrackingEvent trackingEvent)
        {
            infohash.CannotBeNullOrEmpty();
            infohash.Length.MustBeEqualTo(40);
            peerId.CannotBeNullOrEmpty();
            peerId.Length.MustBeGreaterThanOrEqualTo(20);
            port.MustBeGreaterThanOrEqualTo(IPEndPoint.MinPort);
            port.MustBeLessThanOrEqualTo(IPEndPoint.MaxPort);
            bytesUploaded.MustBeGreaterThanOrEqualTo(0);
            bytesDownloaded.MustBeGreaterThanOrEqualTo(0);
            bytesLeft.MustBeGreaterThanOrEqualTo(0);
            peersWantedCount.MustBeGreaterThanOrEqualTo(0);

            this.BytesDownloaded = bytesDownloaded;
            this.BytesUploaded = bytesUploaded;
            this.BytesLeft = bytesLeft;
            this.TrackingEvent = trackingEvent;
            this.InfoHash = infohash;
            this.PeerId = peerId;
            this.Port = port;
            this.PeersWantedCount = peersWantedCount;
        }
        private AnnounceMessage()
        {
        }
        public long BytesDownloaded
        {
            get;
            private set;
        }
        public long BytesLeft
        {
            get;
            private set;
        }
        public long BytesUploaded
        {
            get;
            private set;
        }
        public string InfoHash
        {
            get;
            private set;
        }
        public string PeerId
        {
            get;
            private set;
        }
        public int PeersWantedCount
        {
            get;
            private set;
        }
        public int Port
        {
            get;
            private set;
        }
        public TrackingEvent TrackingEvent
        {
            get;
            private set;
        }
        public string Encode()
        {
            Dictionary<string, object> parameters;

            parameters = new Dictionary<string, object>();
            parameters.Add("info_hash", HttpUtility.UrlEncode(this.InfoHash.ToByteArray()));
            parameters.Add("peer_id", Encoding.ASCII.GetString(Message.FromPeerId(this.PeerId)));
            parameters.Add("port", this.Port);
            parameters.Add("uploaded", this.BytesUploaded);
            parameters.Add("downloaded", this.BytesDownloaded);
            parameters.Add("left", this.BytesLeft);
            parameters.Add("numwant", this.PeersWantedCount);
            parameters.Add("event", this.TrackingEvent.ToString().ToLower(CultureInfo.InvariantCulture));

            return string.Join("&", parameters.Select(x => $"{x.Key}={x.Value}"));
        }
    }
}
