namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
	public enum HandshakeState
	{
		None = 0,
		SentButNotReceived = 1,
		ReceivedButNotSent = 2,
		SendAndReceived = 3
	}
}