using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;
using TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Udp.Messages
{
    public class ErrorMessage : TrackerMessage
    {
        private const int ActionLength = 4;
        private const int TransactionIdLength = 4;
        public ErrorMessage(int transactionId, string errorMessage)
            : base(TrackingAction.Error, transactionId)
        {
            errorMessage.CannotBeNullOrEmpty();

            this.ErrorText = errorMessage;
        }
        public string ErrorText
        {
            get;
            private set;
        }
        public override int Length
        {
            get
            {
                return ActionLength + TransactionIdLength + Encoding.ASCII.GetByteCount(this.ErrorText);
            }
        }
        public static bool TryDecode(byte[] buffer, int offset, out ErrorMessage message)
        {
            int action;
            int transactionId;
            string errorMessage;

            message = null;

            if (buffer != null &&
                buffer.Length >= offset + ActionLength + TransactionIdLength &&
                offset >= 0)
            {
                action = Message.ReadInt(buffer, ref offset);
                transactionId = Message.ReadInt(buffer, ref offset);

                if (action == (int)TrackingAction.Error &&
                    transactionId >= 0)
                {
                    errorMessage = Message.ReadString(buffer, ref offset, buffer.Length - offset);

                    if (errorMessage.IsNotNullOrEmpty())
                    {
                        message = new ErrorMessage(transactionId, errorMessage);
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

            Message.Write(buffer, ref written, (int)this.Action);
            Message.Write(buffer, ref written, this.TransactionId);
            Message.Write(buffer, ref written, this.ErrorText);

            return written - offset;
        }
    }
}
