using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol
{
    public sealed class TrackingFailedEventArgs : EventArgs
    {
        public TrackingFailedEventArgs(Uri trackingUri, string failureReason)
        {
            trackingUri.CannotBeNull();
            failureReason.CannotBeNullOrEmpty();

            this.TrackerUri = trackingUri;
            this.FailureReason = failureReason;
        }
        private TrackingFailedEventArgs()
        {
        }
        public string FailureReason
        {
            get;
            private set;
        }
        public Uri TrackerUri
        {
            get;
            private set;
        }
    }
}
