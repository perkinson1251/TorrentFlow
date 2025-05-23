using System;
using System.Diagnostics;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Http.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Http
{
    public sealed class HttpTracker : Tracker
    {
        public HttpTracker(Uri trackerUri, string peerId, string torrentInfoHash, int listeningPort)
            : base(trackerUri, peerId, torrentInfoHash, listeningPort)
        {
        }
        protected override void OnAnnounce()
        {
            AnnounceResponseMessage message;
            Uri uri;

            this.OnAnnouncing(this, EventArgs.Empty);

            try
            {
                uri = this.GetUri();

                Debug.WriteLine($"{this.TrackerUri} -> {uri}");

                if (AnnounceResponseMessage.TryDecode(uri.ExecuteBinaryRequest(), out message))
                {
                    Debug.WriteLine($"{this.TrackerUri} <- {message}");

                    this.UpdateInterval = message.UpdateInterval;

                    this.OnAnnounced(this, new AnnouncedEventArgs(message.UpdateInterval, message.LeecherCount, message.SeederCount, message.Peers));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"could not send message to HTTP tracker {this.TrackerUri} for torrent {this.TorrentInfoHash}: {ex.Message}");
            }
        }
        protected override void OnStart()
        {
        }
        protected override void OnStop()
        {
            this.TrackingEvent = Udp.Messages.Messages.TrackingEvent.Stopped;

            this.OnAnnounce();
        }
        private Uri GetUri()
        {
            string uri;

            uri = this.TrackerUri.ToString();
            uri += "?";
            uri += new AnnounceMessage(this.TorrentInfoHash, this.PeerId, this.ListeningPort, this.BytesUploaded, this.BytesDownloaded, this.BytesLeftToDownload, this.WantedPeerCount, this.TrackingEvent).Encode();

            return new Uri(uri);
        }
    }
}
