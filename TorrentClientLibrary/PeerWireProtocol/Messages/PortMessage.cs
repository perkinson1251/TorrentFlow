using System.Net;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class PortMessage : PeerMessage
    {
        public const byte MessageId = 9;
        private const int MessageIdLength = 1;
        private const int MessageLength = 3;
        private const int MessageLengthLength = 4;
        private const int PayloadLength = 2;
        public PortMessage(ushort port)
        {
            ((int)port).MustBeGreaterThanOrEqualTo(IPEndPoint.MinPort);
            ((int)port).MustBeLessThanOrEqualTo(IPEndPoint.MaxPort);

            this.Port = port;
        }
        private PortMessage()
        {
        }
        public override int Length
        {
            get
            {
                return MessageLengthLength + MessageIdLength + PayloadLength;
            }
        }
        public ushort Port
        {
            get;
            private set;
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out PortMessage message, out bool isIncomplete)
        {
            int messageLength;
            byte messageId;
            ushort port;

            message = null;
            isIncomplete = false;

            if (buffer != null &&
                buffer.Length >= offsetFrom + MessageLengthLength + MessageIdLength + PayloadLength &&
                offsetFrom >= 0 &&
                offsetTo >= offsetFrom &&
                offsetTo <= buffer.Length)
            {
                messageLength = Message.ReadInt(buffer, ref offsetFrom);
                messageId = Message.ReadByte(buffer, ref offsetFrom);
                port = (ushort)Message.ReadShort(buffer, ref offsetFrom);

                if (messageLength == MessageLength &&
                    messageId == MessageId &&
                    port >= IPEndPoint.MinPort &&
                    port <= IPEndPoint.MaxPort)
                {
                    if (offsetFrom <= offsetTo)
                    {
                        message = new PortMessage(port);
                    }
                    else
                    {
                        isIncomplete = true;
                    }
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

            Message.Write(buffer, ref written, MessageLength);
            Message.Write(buffer, ref written, MessageId);
            Message.Write(buffer, ref written, this.Port);

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            PortMessage msg = obj as PortMessage;

            return msg == null ? false : this.Port == msg.Port;
        }
        public override int GetHashCode()
        {
            return this.Port.GetHashCode();
        }
        public override string ToString()
        {
            return $"PortMessage: Port = {this.Port}";
        }
    }
}
