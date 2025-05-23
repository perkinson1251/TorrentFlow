namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages
{
    public enum TrackingEvent : int
    {
        None = 0,
        Started = 2,
        Stopped = 3,
        Completed = 1
    }
}
