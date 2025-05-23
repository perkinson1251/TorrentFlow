using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class HaveMessage : PeerMessage
    {
        public const byte MessageId = 4;
        private const int MessageIdLength = 1;
        private const int MessageLength = 5;
        private const int MessageLengthLength = 4;
        private const int PayloadLength = 4;
        private int pieceIndex;
        public HaveMessage(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);

            this.pieceIndex = pieceIndex;
        }
        private HaveMessage()
        {
        }
        public override int Length
        {
            get
            {
                return MessageLengthLength + MessageIdLength + PayloadLength;
            }
        }
        public int PieceIndex
        {
            get
            {
                return this.pieceIndex;
            }
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out HaveMessage message, out bool isIncomplete)
        {
            int messageLength;
            byte messageId;
            int payload;

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
                payload = Message.ReadInt(buffer, ref offsetFrom);

                if (messageLength == MessageLength &&
                    messageId == MessageId &&
                    payload >= 0)
                {
                    if (offsetFrom <= offsetTo)
                    {
                        message = new HaveMessage(payload);
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
            Message.Write(buffer, ref written, this.pieceIndex);

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            HaveMessage msg = obj as HaveMessage;

            if (msg == null)
            {
                return false;
            }
            else
            {
                return this.pieceIndex == msg.pieceIndex;
            }
        }
        public override int GetHashCode()
        {
            return this.pieceIndex.GetHashCode();
        }
        public override string ToString()
        {
            StringBuilder sb;

            sb = new StringBuilder();
            sb.Append("HaveMessage: ");
            sb.Append($"Index = {this.pieceIndex}");

            return sb.ToString();
        }
    }
}
