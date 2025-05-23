using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class BitFieldMessage : PeerMessage
    {
        public const byte MessageId = 5;
        private const int MessageIdLength = 1;
        private const int MessageLengthLength = 4;
        private readonly int messageLength;
        private readonly int payloadLength;
        public BitFieldMessage(bool[] bitField)
        {
            bitField.CannotBeNull();

            this.payloadLength = (int)Math.Ceiling((decimal)bitField.Length / (decimal)8);
            this.messageLength = MessageIdLength + this.payloadLength;

            this.BitField = bitField;
        }
        public BitFieldMessage(long pieceCount, IEnumerable<int> missingPieces)
        {
            pieceCount.MustBeGreaterThan(0);
            missingPieces.CannotBeNull();

            this.BitField = new bool[pieceCount];

            for (int i = 0; i < pieceCount; i++)
            {
                this.BitField[i] = true;
            }

            foreach (var missingPiece in missingPieces)
            {
                if (missingPiece < pieceCount)
                {
                    this.BitField[missingPiece] = false;
                }
            }

            this.payloadLength = (int)Math.Ceiling((decimal)this.BitField.Length / (decimal)8);
            this.messageLength = MessageIdLength + this.payloadLength;
        }
        public bool[] BitField
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return MessageLengthLength + MessageIdLength + this.payloadLength;
            }
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out BitFieldMessage message, out bool isIncomplete)
        {
            int messageLength;
            byte messageId;
            byte[] payload;

            message = null;
            isIncomplete = false;

            if (buffer != null &&
                buffer.Length > offsetFrom + MessageLengthLength + MessageIdLength &&
                offsetFrom >= 0 &&
                offsetFrom < buffer.Length &&
                offsetTo >= offsetFrom &&
                offsetTo <= buffer.Length)
            {
                messageLength = Message.ReadInt(buffer, ref offsetFrom);
                messageId = Message.ReadByte(buffer, ref offsetFrom);

                if (messageLength > 0 &&
                    messageId == MessageId)
                {
                    if (offsetFrom + messageLength - MessageIdLength <= offsetTo)
                    {
                        payload = Message.ReadBytes(buffer, ref offsetFrom, messageLength - MessageIdLength);

                        if (payload.IsNotNullOrEmpty() &&
                            payload.Length == messageLength - MessageIdLength)
                        {
                            message = new BitFieldMessage(new BitArray(payload).Cast<bool>().ToArray());
                        }
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
            byte[] byteField = new byte[this.payloadLength];
            int written = offset;

            new BitArray(this.BitField).CopyTo(byteField, 0);

            Message.Write(buffer, ref written, this.messageLength);
            Message.Write(buffer, ref written, MessageId);
            Message.Write(buffer, ref written, byteField);

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            BitFieldMessage bf = obj as BitFieldMessage;

            if (bf == null)
            {
                return false;
            }
            else
            {
                return this.BitField.Equals(bf.BitField);
            }
        }
        public override int GetHashCode()
        {
            return this.BitField.GetHashCode();
        }
        public override string ToString()
        {
            StringBuilder sb;

            sb = new StringBuilder();

            for (int i = 0; i < this.BitField.Length; i++)
            {
                sb.Append(this.BitField[i] ? "1" : "0");
            }

            return "BitfieldMessage: Bitfield = " + sb.ToString();
        }
    }
}
