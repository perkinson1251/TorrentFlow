using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class UnchokeMessage : PeerMessage
    {
        public const byte MessageId = 1;
        private const int MessageIdLength = 1;
        private const int MessageLength = 1;
        private const int MessageLengthLength = 4;
        private const int PayloadLength = 0;
        public UnchokeMessage()
        {
        }
        public override int Length
        {
            get
            {
                return MessageLengthLength + MessageIdLength + PayloadLength;
            }
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out UnchokeMessage message, out bool isIncomplete)
        {
            int messageLength;
            byte messageId;

            message = null;
            isIncomplete = false;

            if (buffer != null &&
                buffer.Length >= offsetFrom + MessageLengthLength + MessageIdLength + PayloadLength &&
                offsetFrom >= 0)
            {
                messageLength = Message.ReadInt(buffer, ref offsetFrom);
                messageId = Message.ReadByte(buffer, ref offsetFrom);

                if (messageLength == MessageLength &&
                    messageId == MessageId)
                {
                    if (offsetFrom <= offsetTo)
                    {
                        message = new UnchokeMessage();
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

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            return obj is UnchokeMessage;
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.InvariantCulture);
        }
        public override string ToString()
        {
            return "UnChokeMessage";
        }
    }
}
