namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages
{
	public enum TrackingAction : int
	{
		Connect = 0,
		Announce = 1,
		Scrape = 2,
		Error = 3
	}
}