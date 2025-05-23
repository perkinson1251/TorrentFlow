using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public abstract class TrackerMessage : Message
    {
        public TrackerMessage(TrackingAction action, int transactionId)
        {
            transactionId.MustBeGreaterThanOrEqualTo(0);

            this.Action = action;
            this.TransactionId = transactionId;
        }
        public TrackingAction Action
        {
            get;
            protected set;
        }
        public int TransactionId
        {
            get;
            protected set;
        }
        public static bool TryDecode(byte[] buffer, int offset, MessageType messageType, out TrackerMessage message)
        {
            int action;

            message = null;

            if (buffer.IsNotNullOrEmpty())
            {
                action = messageType == MessageType.Request ? Message.ReadInt(buffer, ref offset) : Message.ReadInt(buffer, ref offset);
                offset = 0;

                if (action == (int)TrackingAction.Connect)
                {
                    if (messageType == MessageType.Request)
                    {
                        ConnectMessage message2;
                        ConnectMessage.TryDecode(buffer, offset, out message2);

                        message = message2;
                    }
                    else
                    {
                        ConnectResponseMessage message2;
                        ConnectResponseMessage.TryDecode(buffer, offset, out message2);

                        message = message2;
                    }
                }
                else if (action == (int)TrackingAction.Announce)
                {
                    if (messageType == MessageType.Request)
                    {
                        AnnounceMessage message2;
                        AnnounceMessage.TryDecode(buffer, offset, out message2);

                        message = message2;
                    }
                    else
                    {
                        AnnounceResponseMessage message2;
                        AnnounceResponseMessage.TryDecode(buffer, offset, out message2);

                        message = message2;
                    }
                }
                else if (action == (int)TrackingAction.Scrape)
                {
                    if (messageType == MessageType.Request)
                    {
                        ScrapeMessage message2;
                        ScrapeMessage.TryDecode(buffer, offset, out message2);

                        message = message2;
                    }
                    else
                    {
                        ScrapeResponseMessage message2;
                        ScrapeResponseMessage.TryDecode(buffer, offset, out message2);

                        message = message2;
                    }
                }
                else if (action == (int)TrackingAction.Error)
                {
                    ErrorMessage message2;
                    ErrorMessage.TryDecode(buffer, offset, out message2);

                    message = message2;
                }
                else
                {
                    // could not decode UDP message
                }
            }

            return message != null;
        }
    }
}
