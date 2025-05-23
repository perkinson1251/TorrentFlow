namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public enum PieceStatus : int
    {
        Missing = 0,
        CheckedOut = 1,
        Present = 2,
        Partial = 3,
        Ignore = 4
    }
}
