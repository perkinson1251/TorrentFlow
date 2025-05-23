using System;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class KeepAliveMessage : PeerMessage
    {
        private const int MessageLength = 0;
        private const int MessageLengthLength = 4;
        public override int Length
        {
            get
            {
                return MessageLengthLength;
            }
        }
        public static bool TryDecode(byte[] buffer, ref int offset, out KeepAliveMessage message)
        {
            int messageLength;

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + MessageLengthLength &&
                offset >= 0)
            {
                messageLength = Message.ReadInt(buffer, ref offset);

                if (messageLength == MessageLength)
                {
                    message = new KeepAliveMessage();
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

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            return obj is KeepAliveMessage;
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.InvariantCulture);
        }
        public override string ToString()
        {
            return "KeepAliveMessage";
        }
    }
}
