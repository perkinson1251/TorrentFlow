using System;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public class HandshakeMessage : PeerMessage
    {
        public const string ProtocolName = "BitTorrent protocol";
        private const byte ExtendedMessagingFlag = 0x10;
        private const byte FastPeersFlag = 0x04;
        private const int InfoHashLength = 20;
        private const int NameLengthLength = 1;
        private const int PeerIdLength = 20;
        private const int ReservedLength = 8;
        private static readonly byte[] ZeroedBits = new byte[8];
        public HandshakeMessage(string infoHash, string peerId, string protocolString = ProtocolName, bool supportsFastPeer = false, bool supportsExtendedMessaging = false)
        {
            infoHash.CannotBeNullOrEmpty();
            infoHash.Length.MustBeEqualTo(40);
            peerId.CannotBeNullOrEmpty();
            peerId.Length.MustBeGreaterThanOrEqualTo(20);
            protocolString.CannotBeNullOrEmpty();

            this.InfoHash = infoHash;
            this.PeerId = peerId;
            this.ProtocolString = protocolString;
            this.ProtocolStringLength = protocolString.Length;
            this.SupportsFastPeer = supportsFastPeer;
            this.SupportsExtendedMessaging = supportsExtendedMessaging;
        }
        private HandshakeMessage()
        {
        }
        public string InfoHash
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return NameLengthLength + this.ProtocolString.Length + ReservedLength + InfoHashLength + PeerIdLength;
            }
        }
        public string PeerId
        {
            get;
            private set;
        }
        public string ProtocolString
        {
            get;
            private set;
        }
        public int ProtocolStringLength
        {
            get;
            private set;
        }
        public bool SupportsExtendedMessaging
        {
            get;
            private set;
        }
        public bool SupportsFastPeer
        {
            get;
            private set;
        }
        public static bool TryDecode(byte[] buffer, ref int offsetFrom, int offsetTo, out HandshakeMessage message, out bool isIncomplete)
        {
            byte protocolStringLength;
            string protocolString;
            bool supportsExtendedMessaging;
            bool supportsFastPeer;
            string infoHash;
            string peerId;

            message = null;
            isIncomplete = false;

            if (buffer != null &&
                buffer.Length > offsetFrom + NameLengthLength + ReservedLength + InfoHashLength + PeerIdLength &&
                offsetFrom >= 0 &&
                offsetTo >= offsetFrom &&
                offsetTo <= buffer.Length)
            {
                protocolStringLength = Message.ReadByte(buffer, ref offsetFrom); // first byte is length

                if (buffer.Length >= offsetFrom + protocolStringLength + ReservedLength + InfoHashLength + PeerIdLength)
                {
                    protocolString = Message.ReadString(buffer, ref offsetFrom, protocolStringLength);

                    // increment offset first so that the indices are consistent between Encoding and Decoding
                    offsetFrom += ReservedLength;

                    supportsExtendedMessaging = (ExtendedMessagingFlag & buffer[offsetFrom - 3]) == ExtendedMessagingFlag;
                    supportsFastPeer = (FastPeersFlag & buffer[offsetFrom - 1]) == FastPeersFlag;

                    infoHash = Message.ReadBytes(buffer, ref offsetFrom, 20).ToHexaDecimalString();
                    peerId = Message.ToPeerId(Message.ReadBytes(buffer, ref offsetFrom, 20));

                    if (protocolStringLength == 19 &&
                        protocolString == ProtocolName &&
                        infoHash.Length == 40 &&
                        peerId != null &&
                        peerId.Length >= 20 &&
                        peerId.IsNotNullOrEmpty())
                    {
                        if (offsetFrom <= offsetTo)
                        {
                            message = new HandshakeMessage(infoHash, peerId, protocolString, supportsFastPeer, supportsExtendedMessaging);
                        }
                        else
                        {
                            isIncomplete = true;
                        }
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

            Message.Write(buffer, ref written, (byte)this.ProtocolString.Length);
            Message.Write(buffer, ref written, this.ProtocolString);
            Message.Write(buffer, ref written, ZeroedBits);

            if (this.SupportsExtendedMessaging)
            {
                buffer[written - 3] |= ExtendedMessagingFlag;
            }

            if (this.SupportsFastPeer)
            {
                buffer[written - 1] |= FastPeersFlag;
            }

            Message.Write(buffer, ref written, this.InfoHash.ToByteArray());
            Message.Write(buffer, ref written, Message.FromPeerId(this.PeerId));

            return this.CheckWritten(written - offset);
        }
        public override bool Equals(object obj)
        {
            HandshakeMessage msg = obj as HandshakeMessage;

            if (msg == null)
            {
                return false;
            }
            else
            {
                if (this.InfoHash != msg.InfoHash)
                {
                    return false;
                }
                else
                {
                    return this.InfoHash == msg.InfoHash &&
                           this.PeerId == msg.PeerId &&
                           this.ProtocolString == msg.ProtocolString &&
                           this.SupportsFastPeer == msg.SupportsFastPeer &&
                           this.SupportsExtendedMessaging == msg.SupportsExtendedMessaging;
                }
            }
        }
        public override int GetHashCode()
        {
            return this.InfoHash.GetHashCode(StringComparison.InvariantCulture) ^
                   this.PeerId.GetHashCode(StringComparison.InvariantCulture) ^
                   this.ProtocolString.GetHashCode(StringComparison.InvariantCulture) ^
                   this.SupportsFastPeer.GetHashCode() ^
                   this.SupportsExtendedMessaging.GetHashCode();
        }
        public override string ToString()
        {
            StringBuilder sb;

            sb = new System.Text.StringBuilder();
            sb.Append("HandshakeMessage: ");
            sb.Append($"PeerID = {this.PeerId}, ");
            sb.Append($"InfoHash = {this.InfoHash}, ");
            sb.Append($"FastPeer = {this.SupportsFastPeer}, ");
            sb.Append($"ExtendedMessaging = {this.SupportsExtendedMessaging}");

            return sb.ToString();
        }
    }
}
