using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class ChokeMessage : PeerMessage
    {
        public const byte MessageId = 0;
        private const int MessageIdLength = 1;
        private const int MessageLength = 1;
        private const int MessageLengthLength = 4;
        private const int PayloadLength = 0;
        public ChokeMessage()
        {
        }
        public override int Length
        {
            get
            {
                return MessageLengthLength + MessageIdLength + PayloadLength;
            }
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out ChokeMessage message, out bool isIncomplete)
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
                        message = new ChokeMessage();
                    }
                    else
                    {
                        isIncomplete = true;
                    }
                }
            }

            return message != null;
        }
        public override int Encode(byte[] buffer, int offset = 0)
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
            return obj is ChokeMessage;
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.InvariantCulture);
        }
        public override string ToString()
        {
            return "ChokeMessage";
        }
    }
}
