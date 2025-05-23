using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public class ConnectResponseMessage : TrackerMessage
    {
        private const int ActionLength = 4;
        private const int ConnectionIdLength = 8;
        private const int TransactionIdLength = 4;
        public ConnectResponseMessage(long connectionId, int transactionId)
            : base(TrackingAction.Connect, transactionId)
        {
            this.ConnectionId = connectionId;
        }
        public long ConnectionId
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return ConnectionIdLength + ActionLength + TransactionIdLength;
            }
        }
        public static bool TryDecode(byte[] buffer, int offset, out ConnectResponseMessage message)
        {
            long connectionId;
            int action;
            int transactionId;

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + ConnectionIdLength + ActionLength + TransactionIdLength &&
                offset >= 0)
            {
                action = Message.ReadInt(buffer, ref offset);
                transactionId = Message.ReadInt(buffer, ref offset);
                connectionId = Message.ReadLong(buffer, ref offset);

                if (action == (int)TrackingAction.Connect &&
                    transactionId >= 0)
                {
                    message = new ConnectResponseMessage(connectionId, transactionId);
                }
            }

            return message != null;
        }
        public override int Encode(byte[] buffer, int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);

            int written = offset;

            Message.Write(buffer, ref written, (int)this.Action);
            Message.Write(buffer, ref written, this.TransactionId);
            Message.Write(buffer, ref written, this.ConnectionId);

            return this.CheckWritten(written - offset);
        }
        public override string ToString()
        {
            return "UdpTrackerConnectResponseMessage";
        }
    }
}
