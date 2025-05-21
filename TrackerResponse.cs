using System.Collections.Generic;
using System.Net; // Для IPEndPoint

namespace TorrentFlow
{
    public class PeerInfo
    {
        public string? PeerId { get; set; } // Не завжди присутній у компактній відповіді
        public IPEndPoint? EndPoint { get; set; }

        public PeerInfo(IPAddress address, ushort port, string? peerId = null)
        {
            EndPoint = new IPEndPoint(address, port);
            PeerId = peerId;
        }
        public override string ToString()
        {
            return EndPoint?.ToString() ?? "Invalid Peer";
        }
    }

    public class TrackerResponse
    {
        public string? FailureReason { get; set; }
        public string? WarningMessage { get; set; } // Необов'язково
        public int Interval { get; set; } // Інтервал переоголошення в секундах
        public int? MinInterval { get; set; } // Мінімальний інтервал
        public string? TrackerId { get; set; } // Необов'язково
        public int Complete { get; set; } // Кількість сідерів
        public int Incomplete { get; set; } // Кількість лічерів
        public List<PeerInfo> Peers { get; set; } = new List<PeerInfo>();

        public bool RequestFailed => !string.IsNullOrEmpty(FailureReason);
    }
}