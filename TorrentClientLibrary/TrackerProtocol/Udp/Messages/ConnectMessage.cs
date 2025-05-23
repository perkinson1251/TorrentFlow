using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public class ConnectMessage : TrackerMessage
    {
        private const int ActionLength = 4;
        private const long ConnectionId = 0x41727101980;
        private const int ConnectionIdLength = 8;
        private const int TransactionIdLength = 4;
        public ConnectMessage(int transactionId)
            : base(TrackingAction.Connect, transactionId)
        {
        }
        private ConnectMessage()
            : this(DateTime.UtcNow.GetHashCode())
        {
        }
        public override int Length
        {
            get
            {
                return ConnectionIdLength + ActionLength + TransactionIdLength;
            }
        }
        public static bool TryDecode(byte[] buffer, int offset, out ConnectMessage message)
        {
            long connectionId;
            int action;
            int transactionId;

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + ConnectionIdLength + ActionLength + TransactionIdLength &&
                offset >= 0)
            {
                connectionId = Message.ReadLong(buffer, ref offset);
                action = Message.ReadInt(buffer, ref offset);
                transactionId = Message.ReadInt(buffer, ref offset);

                if (connectionId == ConnectMessage.ConnectionId &&
                    action == (int)TrackingAction.Connect &&
                    transactionId >= 0)
                {
                    message = new ConnectMessage(transactionId);
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

            Message.Write(buffer, ref written, ConnectMessage.ConnectionId);
            Message.Write(buffer, ref written, (int)this.Action);
            Message.Write(buffer, ref written, this.TransactionId);

            return this.CheckWritten(written - offset);
        }
        public override string ToString()
        {
            return "UdpTrackerConnectMessage";
        }
    }
}
