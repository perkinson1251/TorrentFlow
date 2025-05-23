using System;
using System.Net;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public class AnnounceMessage : TrackerMessage
    {
        private const int ActionLength = 4;
        private const int ConnectionIdLength = 8;
        private const int DownloadedLength = 8;
        private const int InfoHashLength = 20;
        private const int IpAddressLength = 4;
        private const int KeyLength = 4;
        private const int LeftLength = 8;
        private const int NumWantLength = 4;
        private const int PeerIdLength = 20;
        private const int PortLength = 2;
        private const int TrackingEventLength = 4;
        private const int TransactionIdLength = 4;
        private const int UploadedLength = 8;
        public AnnounceMessage(long connectionId, int transactionId, string infoHash, string peerId, long downloaded, long left, long uploaded, TrackingEvent trackingEvent, uint key, int numberWanted, IPEndPoint endpoint)
            : base(TrackingAction.Announce, transactionId)
        {
            infoHash.CannotBeNullOrEmpty();
            infoHash.Length.MustBeEqualTo(40);
            peerId.CannotBeNullOrEmpty();
            peerId.Length.MustBeGreaterThanOrEqualTo(20);
            downloaded.MustBeGreaterThanOrEqualTo(0);
            left.MustBeGreaterThanOrEqualTo(0);
            uploaded.MustBeGreaterThanOrEqualTo(0);
            endpoint.CannotBeNull();

            this.ConnectionId = connectionId;
            this.InfoHash = infoHash;
            this.PeerId = peerId;
            this.Downloaded = downloaded;
            this.Left = left;
            this.Uploaded = uploaded;
            this.TrackingEvent = trackingEvent;
            this.Key = key;
            this.NumberWanted = numberWanted;
            this.Endpoint = endpoint;
        }
        public long ConnectionId
        {
            get;
            private set;
        }
        public long Downloaded
        {
            get;
            private set;
        }
        public IPEndPoint Endpoint
        {
            get;
            private set;
        }
        public string InfoHash
        {
            get;
            private set;
        }
        public uint Key
        {
            get;
            private set;
        }
        public long Left
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return ConnectionIdLength + ActionLength + TransactionIdLength + InfoHashLength + PeerIdLength + DownloadedLength + LeftLength + UploadedLength + TrackingEventLength + IpAddressLength + KeyLength + NumWantLength + PortLength;
            }
        }
        public int NumberWanted
        {
            get;
            private set;
        }
        public string PeerId
        {
            get;
            private set;
        }
        public TrackingEvent TrackingEvent
        {
            get;
            private set;
        }
        public long Uploaded
        {
            get;
            private set;
        }
        public static bool TryDecode(byte[] buffer, int offset, out AnnounceMessage message)
        {
            long connectionId;
            long action;
            int transactionId;
            string infoHash;
            string peerId;
            long downloaded;
            long left;
            long uploaded;
            int trackingEvent;
            int ipaddress;
            uint key;
            int numberWanted;
            ushort port;

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + ConnectionIdLength + ActionLength + TransactionIdLength + InfoHashLength + PeerIdLength + DownloadedLength + LeftLength + UploadedLength + TrackingEventLength + IpAddressLength + KeyLength + NumWantLength + PortLength &&
                offset >= 0)
            {
                connectionId = Message.ReadLong(buffer, ref offset);
                action = Message.ReadInt(buffer, ref offset);
                transactionId = Message.ReadInt(buffer, ref offset);
                infoHash = Message.ReadBytes(buffer, ref offset, 20).ToHexaDecimalString();
                peerId = Message.ToPeerId(Message.ReadBytes(buffer, ref offset, 20));
                downloaded = Message.ReadLong(buffer, ref offset);
                left = Message.ReadLong(buffer, ref offset);
                uploaded = Message.ReadLong(buffer, ref offset);
                trackingEvent = Message.ReadInt(buffer, ref offset);
                ipaddress = Message.ReadInt(buffer, ref offset);
                key = (uint)Message.ReadInt(buffer, ref offset);
                numberWanted = Message.ReadInt(buffer, ref offset);
                port = (ushort)Message.ReadShort(buffer, ref offset);

                if (connectionId >= 0 &&
                    action == (int)TrackingAction.Announce &&
                    transactionId >= 0 &&
                    infoHash.IsNotNullOrEmpty() &&
                    peerId.IsNotNullOrEmpty() &&
                    downloaded >= 0 &&
                    left >= 0 &&
                    uploaded >= 0 &&
                    trackingEvent >= 0 &&
                    trackingEvent <= 3 &&
                    port >= IPEndPoint.MinPort &&
                    port <= IPEndPoint.MaxPort)
                {
                    message = new AnnounceMessage(connectionId, transactionId, infoHash, peerId, downloaded, left, uploaded, (TrackingEvent)trackingEvent, key, numberWanted, new IPEndPoint(new IPAddress(BitConverter.GetBytes(ipaddress)), port));
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

            Message.Write(buffer, ref written, this.ConnectionId);
            Message.Write(buffer, ref written, (int)this.Action);
            Message.Write(buffer, ref written, this.TransactionId);
            Message.Write(buffer, ref written, this.InfoHash.ToByteArray());
            Message.Write(buffer, ref written, Message.FromPeerId(this.PeerId));
            Message.Write(buffer, ref written, this.Downloaded);
            Message.Write(buffer, ref written, this.Left);
            Message.Write(buffer, ref written, this.Uploaded);
            Message.Write(buffer, ref written, (int)this.TrackingEvent);
            Message.Write(buffer, ref written, this.Endpoint.Address == IPAddress.Loopback ? 0 : BitConverter.ToInt32(this.Endpoint.Address.GetAddressBytes(), 0));
            Message.Write(buffer, ref written, this.Key);
            Message.Write(buffer, ref written, this.NumberWanted);
            Message.Write(buffer, ref written, (ushort)this.Endpoint.Port);

            return written - offset;
        }
        public override string ToString()
        {
            return "UdpTrackerAnnounceMessage";
        }
    }
}
