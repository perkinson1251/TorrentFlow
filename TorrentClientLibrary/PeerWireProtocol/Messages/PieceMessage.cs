using System;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class PieceMessage : PeerMessage
    {
        public const byte MessageId = 7;
        private const int BlockOffsetLength = 4;
        private const int MessageIdLength = 1;
        private const int MessageLengthLength = 4;
        private const int PieiceIndexLength = 4;
        public PieceMessage(int pieceIndex, int blockOffset, int blockDataLength, byte[] data)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            blockOffset.MustBeGreaterThanOrEqualTo(0);
            blockDataLength.MustBeGreaterThan(0);
            data.CannotBeNullOrEmpty();

            this.PieceIndex = pieceIndex;
            this.BlockOffset = blockOffset;
            this.BlockDataLength = blockDataLength;
            this.Data = data;
        }
        private PieceMessage()
        {
        }
        public int BlockDataLength
        {
            get;
            private set;
        }
        public int BlockOffset
        {
            get;
            private set;
        }
        public byte[] Data
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return MessageLengthLength + MessageIdLength + PieiceIndexLength + BlockOffsetLength + this.BlockDataLength;
            }
        }
        public int PieceIndex
        {
            get;
            private set;
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out PieceMessage message, out bool isIncomplete, byte[] destination = null)
        {
            int messageLength;
            byte messageId;
            int pieceIndex;
            int blockOffset;
            int blockDataLength = 0;
            int destinationOffset = 0;
            int offsetFrom2 = offsetFrom;

            message = null;
            isIncomplete = false;

            if (buffer != null &&
                buffer.Length > offsetFrom2 + MessageLengthLength + MessageIdLength + PieiceIndexLength + BlockOffsetLength &&
                offsetFrom2 >= 0 &&
                offsetFrom2 < buffer.Length &&
                offsetTo >= offsetFrom2 &&
                offsetTo <= buffer.Length)
            {
                messageLength = Message.ReadInt(buffer, ref offsetFrom2);
                messageId = Message.ReadByte(buffer, ref offsetFrom2);
                pieceIndex = Message.ReadInt(buffer, ref offsetFrom2);
                blockOffset = Message.ReadInt(buffer, ref offsetFrom2);
                blockDataLength = messageLength - MessageIdLength - PieiceIndexLength - BlockOffsetLength;

                if (messageLength > MessageIdLength + PieiceIndexLength + BlockOffsetLength &&
                    messageId == MessageId &&
                    pieceIndex >= 0 &&
                    blockOffset >= 0 &&
                    blockDataLength >= 0)
                {
                    if (offsetFrom2 + blockDataLength <= offsetTo)
                    {
                        if (destination == null)
                        {
                            destination = new byte[blockDataLength];
                            destinationOffset = 0;
                        }
                        else
                        {
                            destinationOffset = blockOffset;
                        }

                        Message.Copy(buffer, offsetFrom2, destination, ref destinationOffset, blockDataLength);

                        message = new PieceMessage(pieceIndex, blockOffset, blockDataLength, destination);
                        offsetFrom = offsetFrom2 + blockDataLength;
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

            Message.Write(buffer, ref written, MessageIdLength + PieiceIndexLength + BlockOffsetLength + this.Data.Length);
            Message.Write(buffer, ref written, MessageId);
            Message.Write(buffer, ref written, this.PieceIndex);
            Message.Write(buffer, ref written, this.BlockOffset);
            Message.Write(buffer, ref written, this.Data);

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            PieceMessage msg = obj as PieceMessage;

            if (msg == null)
            {
                return false;
            }
            else if (this.PieceIndex == msg.PieceIndex &&
                     this.BlockOffset == msg.BlockOffset &&
                     this.Data.ToHexaDecimalString() == msg.Data.ToHexaDecimalString())
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public override int GetHashCode()
        {
            int hash;

            hash = this.PieceIndex.GetHashCode() ^
                   this.BlockOffset.GetHashCode() ^
                   this.Data.ToHexaDecimalString().GetHashCode(StringComparison.InvariantCulture);

            return hash;
        }
        public override string ToString()
        {
            return $"PieceMessage: PieceIndex = {this.PieceIndex}, BlockOffset = {this.BlockOffset}, BlockData = byte[{this.Data.Length}]";
        }
    }
}
