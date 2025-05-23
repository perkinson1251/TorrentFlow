using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class RequestMessage : PeerMessage
    {
        public const byte MessageId = 6;
        private const int BlockLengthLength = 4;
        private const int BlockOffsetLength = 4;
        private const int MessageIdLength = 1;
        private const int MessageLength = 13;
        private const int MessageLengthLength = 4;
        private const int PieceIndexLength = 4;
        public RequestMessage(int pieceIndex, int blockOffset, int blockLength)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            blockOffset.MustBeGreaterThanOrEqualTo(0);
            blockLength.MustBeGreaterThanOrEqualTo(0);

            this.PieceIndex = pieceIndex;
            this.BlockOffset = blockOffset;
            this.BlockLength = blockLength;
        }
        private RequestMessage()
        {
        }
        public int BlockLength
        {
            get;
            private set;
        }
        public int BlockOffset
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return MessageLengthLength + MessageIdLength + PieceIndexLength + BlockOffsetLength + BlockLengthLength;
            }
        }
        public int PieceIndex
        {
            get;
            private set;
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out RequestMessage message, out bool isIncomplete)
        {
            int messageLength;
            byte messageId;
            int pieceIndex;
            int blockOffset;
            int blockLength;

            message = null;
            isIncomplete = false;

            if (buffer != null &&
                buffer.Length >= offsetFrom + MessageLengthLength + MessageIdLength + PieceIndexLength + BlockOffsetLength + BlockLengthLength &&
                offsetFrom >= 0 &&
                offsetTo >= offsetFrom &&
                offsetTo <= buffer.Length)
            {
                messageLength = Message.ReadInt(buffer, ref offsetFrom);
                messageId = Message.ReadByte(buffer, ref offsetFrom);
                pieceIndex = Message.ReadInt(buffer, ref offsetFrom);
                blockOffset = Message.ReadInt(buffer, ref offsetFrom);
                blockLength = Message.ReadInt(buffer, ref offsetFrom);

                if (messageLength == MessageIdLength + PieceIndexLength + BlockOffsetLength + BlockLengthLength &&
                    messageId == MessageId &&
                    pieceIndex >= 0 &&
                    blockOffset >= 0 &&
                    blockLength >= 0)
                {
                    if (offsetFrom <= offsetTo)
                    {
                        message = new RequestMessage(pieceIndex, blockOffset, blockLength);
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
            Message.Write(buffer, ref written, this.PieceIndex);
            Message.Write(buffer, ref written, this.BlockOffset);
            Message.Write(buffer, ref written, this.BlockLength);

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            RequestMessage msg = obj as RequestMessage;

            return msg == null ? false : this.PieceIndex == msg.PieceIndex &&
                                         this.BlockOffset == msg.BlockOffset &&
                                         this.BlockLength == msg.BlockLength;
        }
        public override int GetHashCode()
        {
            return this.PieceIndex.GetHashCode() ^
                   this.BlockLength.GetHashCode() ^
                   this.BlockOffset.GetHashCode();
        }
        public override string ToString()
        {
            StringBuilder sb;

            sb = new System.Text.StringBuilder();
            sb.Append("RequestMessage: ");
            sb.Append($"PieceIndex = {this.PieceIndex}, ");
            sb.Append($"BlockOffset = {this.BlockOffset}, ");
            sb.Append($"BlockLength = {this.BlockLength}");

            return sb.ToString();
        }
    }
}
