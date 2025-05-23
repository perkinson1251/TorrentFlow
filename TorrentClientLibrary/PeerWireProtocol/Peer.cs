using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class Peer : IDisposable
    {
        private readonly object locker = new object();
        private PeerCommunicator communicator;
        private object downloadMessageQueueLocker = new object();
        private Queue<PeerMessage> downloadMessageQueue = new Queue<PeerMessage>();
        private decimal downloadSpeed = 0;
        private bool isDownloading = false;
        private bool isKeepingAlive = false;
        private bool isUploading = false;
        private DateTime? lastMessageReceivedTime;
        private DateTime? lastMessageSentTime;
        private string localPeerId;
        private PieceManager pieceManager;
        private long previouslyDownloaded = 0;
        private long previouslyUploaded = 0;
        private object sendMessageQueueLocker = new object();
        private Queue<PeerMessage> sendMessageQueue = new Queue<PeerMessage>();
        private Stopwatch stopwatch = Stopwatch.StartNew();
        private object uploadMessageQueueLocker = new object();
        private Queue<PeerMessage> uploadMessageQueue = new Queue<PeerMessage>();
        private decimal uploadSpeed = 0;
        public Peer(PeerCommunicator communicator, PieceManager pieceManager, string localPeerId, string peerId = null)
        {
            communicator.CannotBeNull();
            pieceManager.CannotBeNull();
            localPeerId.CannotBeNullOrEmpty();

            this.PeerId = peerId;

            this.localPeerId = localPeerId;

            this.BitField = new bool[pieceManager.PieceCount];

            this.HandshakeState = peerId == null ? HandshakeState.SentButNotReceived : HandshakeState.SendAndReceived;
            this.SeedingState = SeedingState.Choked;
            this.LeechingState = LeechingState.Uninterested;

            this.Downloaded = 0;
            this.Uploaded = 0;

            this.communicator = communicator;
            this.communicator.MessageReceived += this.Communicator_MessageReceived;
            this.communicator.CommunicationError += this.Communicator_CommunicationError;

            this.pieceManager = pieceManager;
            this.pieceManager.PieceCompleted += this.PieceManager_PieceCompleted;

            this.Endpoint = this.communicator.Endpoint;

            this.StartSending();
            this.StartDownloading();
            this.StartUploading();
            this.StartKeepingConnectionAlive();

            // send handshake
            this.EnqueueSendMessage(new HandshakeMessage(this.pieceManager.TorrentInfoHash, localPeerId, HandshakeMessage.ProtocolName));
        }
        private Peer()
        {
        }
        public event EventHandler<PeerCommunicationErrorEventArgs> CommunicationErrorOccurred;
        public bool[] BitField
        {
            get;
            private set;
        }
        public long Downloaded
        {
            get;
            private set;
        }
        public decimal DownloadSpeed
        {
            get
            {
                this.UpdateTrafficParameters(0, 0);

                return this.downloadSpeed;
            }
        }
        public IPEndPoint Endpoint
        {
            get;
            private set;
        }
        public HandshakeState HandshakeState
        {
            get;
            private set;
        }
        public bool IsDisposed
        {
            get;
            private set;
        }
        public LeechingState LeechingState
        {
            get;
            private set;
        }
        public string PeerId
        {
            get;
            private set;
        }
        public SeedingState SeedingState
        {
            get;
            private set;
        }
        public long Uploaded
        {
            get;
            private set;
        }
        public decimal UploadSpeed
        {
            get
            {
                this.UpdateTrafficParameters(0, 0);

                return this.uploadSpeed;
            }
        }
        public void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.IsDisposed = true;

                Debug.WriteLine($"disposing peer {this.Endpoint}");

                if (this.communicator != null &&
                    !this.communicator.IsDisposed)
                {
                    this.communicator.Dispose();
                    this.communicator = null;
                }
            }
        }
        private void CheckIfObjectIsDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }
        private void Communicator_CommunicationError(object sender, CommunicationErrorEventArgs e)
        {
            this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs(e.ErrorMessage, true));
        }
        private void Communicator_MessageReceived(object sender, PeerMessgeReceivedEventArgs e)
        {
            this.ProcessRecievedMessage(e.Message);

            this.UpdateTrafficParameters(e.Message.Length, 0);
        }
        private IEnumerable<PeerMessage> DequeueDownloadMessages()
        {
            IEnumerable<PeerMessage> messages;

            lock (this.downloadMessageQueueLocker)
            {
                messages = this.downloadMessageQueue;

                // TODO: optimize this by using a wait handler
                this.downloadMessageQueue = new Queue<PeerMessage>();
            }

            return messages;
        }
        private IEnumerable<PeerMessage> DequeueSendMessages()
        {
            IEnumerable<PeerMessage> messages;

            lock (this.sendMessageQueueLocker)
            {
                messages = this.sendMessageQueue;

                this.sendMessageQueue = new Queue<PeerMessage>();
            }

            return messages;
        }
        private IEnumerable<PeerMessage> DequeueUploadMessages()
        {
            IEnumerable<PeerMessage> messages;

            lock (this.uploadMessageQueueLocker)
            {
                messages = this.uploadMessageQueue;

                this.uploadMessageQueue = new Queue<PeerMessage>();
            }

            return messages;
        }
        private void Download()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(250);
            TimeSpan chokeTimeout = TimeSpan.FromSeconds(10);
            Stopwatch stopwatch = Stopwatch.StartNew();
            PieceMessage pm;
            Piece piece = null;
            bool[] bitFieldData = Array.Empty<bool>();
            byte[] pieceData = Array.Empty<byte>();
            int unchokeMessagesSent = 0;

            if (!this.isDownloading)
            {
                this.isDownloading = true;

                this.communicator.PieceData = new byte[this.pieceManager.PieceLength];

                while (!this.IsDisposed)
                {
                    if (this.pieceManager.IsComplete)
                    {
                        break;
                    }

                    // process messages
                    foreach (PeerMessage message in this.DequeueDownloadMessages())
                    {
                        if (message is PieceMessage)
                        {
                            pm = message as PieceMessage;

                            if (piece != null &&
                                piece.PieceIndex == pm.PieceIndex &&
                                piece.BitField[pm.BlockOffset / this.pieceManager.BlockLength] == false)
                            {
                                // update piece
                                piece.PutBlock(pm.BlockOffset);

                                if (piece.IsCompleted ||
                                    piece.IsCorrupted)
                                {
                                    // remove piece in order to start a next one
                                    piece = null;
                                }
                            }
                        }
                        else if (message is ChokeMessage)
                        {
                            this.SeedingState = SeedingState.Choked;

                            piece = null;
                        }
                        else if (message is UnchokeMessage)
                        {
                            this.SeedingState = SeedingState.Unchoked;

                            unchokeMessagesSent = 0;
                        }
                    }

                    if (this.HandshakeState == HandshakeState.SendAndReceived)
                    {
                        if (this.SeedingState == SeedingState.Choked)
                        {
                            if (stopwatch.Elapsed > chokeTimeout)
                            {
                                // choked -> send interested
                                this.EnqueueSendMessage(new InterestedMessage());

                                if (++unchokeMessagesSent > 10)
                                {
                                    this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs($"Choked for more than {TimeSpan.FromSeconds(chokeTimeout.TotalSeconds * 10)}.", true));
                                }

                                stopwatch.Restart();
                            }
                            else
                            {
                                Thread.Sleep(timeout);
                            }
                        }
                        else if (this.SeedingState == SeedingState.Unchoked)
                        {
                            if (piece == null)
                            {
                                // find a missing piece
                                for (int pieceIndex = 0; pieceIndex < this.BitField.Length; pieceIndex++)
                                {
                                    if (this.pieceManager.BitField[pieceIndex] == PieceStatus.Missing)
                                    {
                                        if (this.BitField[pieceIndex] ||
                                            this.pieceManager.IsEndGame)
                                        {
                                            pieceData = pieceData.Length == this.pieceManager.GetPieceLength(pieceIndex) ? pieceData : new byte[this.pieceManager.GetPieceLength(pieceIndex)];
                                            bitFieldData = bitFieldData.Length == this.pieceManager.GetBlockCount(pieceIndex) ? bitFieldData : new bool[this.pieceManager.GetBlockCount(pieceIndex)];

                                            // check it out
                                            piece = this.pieceManager.CheckOut(pieceIndex, pieceData, bitFieldData);

                                            if (piece != null)
                                            {
                                                this.communicator.PieceData = pieceData;

                                                break;
                                            }
                                        }
                                    }
                                }

                                if (piece != null)
                                {
                                    // request blocks from the missing piece
                                    for (int i = 0; i < piece.BitField.Length; i++)
                                    {
                                        if (!piece.BitField[i])
                                        {
                                            this.EnqueueSendMessage(new RequestMessage(piece.PieceIndex, (int)piece.GetBlockOffset(i), (int)piece.GetBlockLength(piece.GetBlockOffset(i))));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Thread.Sleep(timeout);
                }

                this.isDownloading = false;
            }
        }
        private void EnqueueDownloadMessage(PeerMessage message)
        {
            message.CannotBeNull();

            lock (this.downloadMessageQueueLocker)
            {
                this.downloadMessageQueue.Enqueue(message);
            }
        }
        private void EnqueueSendMessage(PeerMessage message)
        {
            message.CannotBeNull();

            lock (this.sendMessageQueueLocker)
            {
                this.sendMessageQueue.Enqueue(message);
            }
        }
        private void EnqueueUploadMessage(PeerMessage message)
        {
            message.CannotBeNull();

            lock (this.uploadMessageQueueLocker)
            {
                this.uploadMessageQueue.Enqueue(message);
            }
        }
        private void KeepAlive()
        {
            TimeSpan keepAliveTimeout = TimeSpan.FromSeconds(60);
            TimeSpan timeout = TimeSpan.FromSeconds(10);

            if (!this.isKeepingAlive)
            {
                this.isKeepingAlive = true;

                while (!this.IsDisposed)
                {
                    if (!this.isDownloading &&
                        !this.isUploading)
                    {
                        break;
                    }
                    else if (this.lastMessageSentTime == null &&
                             this.lastMessageReceivedTime == null)
                    {
                        Thread.Sleep(timeout);
                    }
                    else if (DateTime.UtcNow - this.lastMessageSentTime > keepAliveTimeout ||
                             DateTime.UtcNow - this.lastMessageReceivedTime > keepAliveTimeout)
                    {
                        this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs($"No message exchanged in over {keepAliveTimeout}.", true));

                        break;
                    }
                    else
                    {
                        Thread.Sleep(timeout);
                    }
                }

                this.isKeepingAlive = false;
            }
        }
        private void OnCommunicationErrorOccurred(object sender, PeerCommunicationErrorEventArgs e)
        {
            sender.CannotBeNull();
            e.CannotBeNull();

            if (this.CommunicationErrorOccurred != null)
            {
                this.CommunicationErrorOccurred(sender, e);
            }
        }
        private void PieceManager_PieceCompleted(object sender, PieceCompletedEventArgs e)
        {
            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                this.EnqueueSendMessage(new HaveMessage(e.PieceIndex));
            }
        }
        private void ProcessRecievedMessage(PeerMessage message)
        {
            this.CheckIfObjectIsDisposed();

            lock (this.locker)
            {
                Debug.WriteLine($"{this.Endpoint} <- {message}");

                this.lastMessageReceivedTime = DateTime.UtcNow;

                if (message is HandshakeMessage)
                {
                    this.ProcessRecievedMessage(message as HandshakeMessage);
                }
                else if (message is ChokeMessage)
                {
                    this.ProcessRecievedMessage(message as ChokeMessage);
                }
                else if (message is UnchokeMessage)
                {
                    this.ProcessRecievedMessage(message as UnchokeMessage);
                }
                else if (message is InterestedMessage)
                {
                    this.ProcessRecievedMessage(message as InterestedMessage);
                }
                else if (message is UninterestedMessage)
                {
                    this.ProcessRecievedMessage(message as UninterestedMessage);
                }
                else if (message is HaveMessage)
                {
                    this.ProcessRecievedMessage(message as HaveMessage);
                }
                else if (message is BitFieldMessage)
                {
                    this.ProcessRecievedMessage(message as BitFieldMessage);
                }
                else if (message is RequestMessage)
                {
                    this.ProcessRecievedMessage(message as RequestMessage);
                }
                else if (message is PieceMessage)
                {
                    this.ProcessRecievedMessage(message as PieceMessage);
                }
                else if (message is CancelMessage)
                {
                    this.ProcessRecievedMessage(message as CancelMessage);
                }
                else if (message is PortMessage)
                {
                    // TODO
                }
                else if (message is KeepAliveMessage)
                {
                    // do nothing
                }
            }
        }
        private void ProcessRecievedMessage(CancelMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                if (message.PieceIndex >= 0 &&
                    message.PieceIndex < this.pieceManager.PieceCount &&
                    message.BlockOffset >= 0 &&
                    message.BlockOffset < this.pieceManager.PieceLength &&
                    message.BlockOffset % this.pieceManager.BlockLength == 0 &&
                    message.BlockLength == this.pieceManager.GetBlockLength(message.PieceIndex, message.BlockOffset / this.pieceManager.BlockLength))
                {
                    this.EnqueueUploadMessage(message);
                }
                else
                {
                    this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid cancel message.", false));
                }
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(PieceMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                if (message.PieceIndex >= 0 &&
                    message.PieceIndex < this.pieceManager.PieceCount &&
                    message.BlockOffset >= 0 &&
                    message.BlockOffset < this.pieceManager.PieceLength &&
                    message.BlockOffset % this.pieceManager.BlockLength == 0 &&
                    message.Data.Length == this.pieceManager.GetPieceLength(message.PieceIndex))
                {
                    this.EnqueueDownloadMessage(message);
                }
                else
                {
                    this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid piece message.", false));
                }
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(RequestMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                if (message.PieceIndex >= 0 &&
                    message.PieceIndex < this.pieceManager.BlockCount &&
                    message.BlockOffset >= 0 &&
                    message.BlockOffset < this.pieceManager.GetBlockCount(message.PieceIndex) &&
                    message.BlockOffset / this.pieceManager.BlockLength == 0 &&
                    message.BlockLength == this.pieceManager.GetBlockLength(message.PieceIndex, message.BlockOffset / this.pieceManager.BlockLength))
                {
                    this.EnqueueUploadMessage(message);
                }
                else
                {
                    this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid request message.", false));
                }
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(BitFieldMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                if (message.BitField.Length >= this.pieceManager.BlockCount)
                {
                    for (int i = 0; i < this.BitField.Length; i++)
                    {
                        this.BitField[i] = message.BitField[i];
                    }

                    // notify downloading thread
                    this.EnqueueDownloadMessage(message);
                }
                else
                {
                    this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid bit field message.", true));
                }
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(ChokeMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                this.EnqueueDownloadMessage(message);
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(UnchokeMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                this.EnqueueDownloadMessage(message);
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(InterestedMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                this.EnqueueDownloadMessage(message);
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(UninterestedMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                this.EnqueueUploadMessage(message);
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(HaveMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.SendAndReceived)
            {
                if (message.PieceIndex >= 0 &&
                    message.PieceIndex < this.pieceManager.PieceCount)
                {
                    this.BitField[message.PieceIndex] = true;

                    // notify downloading thread
                    this.EnqueueDownloadMessage(message);
                }
                else
                {
                    this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid have message.", false));
                }
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void ProcessRecievedMessage(HandshakeMessage message)
        {
            message.CannotBeNull();

            if (this.HandshakeState == HandshakeState.None ||
                this.HandshakeState == HandshakeState.SentButNotReceived)
            {
                if (message.InfoHash == this.pieceManager.TorrentInfoHash &&
                    message.ProtocolString == HandshakeMessage.ProtocolName &&
                    message.PeerId.IsNotNullOrEmpty() &&
                    message.PeerId != this.localPeerId)
                {
                    if (this.HandshakeState == HandshakeState.None)
                    {
                        this.HandshakeState = HandshakeState.ReceivedButNotSent;
                        this.PeerId = message.PeerId;

                        // send a handshake
                        this.EnqueueSendMessage(new HandshakeMessage(this.pieceManager.TorrentInfoHash, this.localPeerId));

                        // send a bit field
                        this.EnqueueSendMessage(new BitFieldMessage(this.pieceManager.BitField.Select(x => x == PieceStatus.Present).ToArray()));
                    }
                    else if (this.HandshakeState == HandshakeState.SentButNotReceived)
                    {
                        this.HandshakeState = HandshakeState.SendAndReceived;
                        this.PeerId = message.PeerId;

                        // send a bit field
                        this.EnqueueSendMessage(new BitFieldMessage(this.pieceManager.BitField.Select(x => x == PieceStatus.Present).ToArray()));
                    }
                }
                else
                {
                    this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid handshake message.", true));
                }
            }
            else
            {
                this.OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
            }
        }
        private void Send()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(250);
            IEnumerable<PeerMessage> messages;

            while (!this.IsDisposed)
            {
                messages = this.DequeueSendMessages();

                if (messages.Count() > 0)
                {
                    if (this.communicator != null &&
                        !this.communicator.IsDisposed)
                    {
                        foreach (var message in messages)
                        {
                            Debug.WriteLine($"{this.Endpoint} -> {message}");
                        }

                        // send message
                        this.communicator.Send(messages);

                        this.UpdateTrafficParameters(0, messages.Sum(x => x.Length));
                    }
                }

                this.lastMessageSentTime = DateTime.UtcNow;

                Thread.Sleep(timeout);
            }
        }
        private void StartDownloading()
        {
            Thread thread;

            if (!this.isDownloading)
            {
                thread = new Thread(this.Download);
                thread.IsBackground = true;
                thread.Name = this.PeerId + " downloader";
                thread.Start();
            }
        }
        private void StartKeepingConnectionAlive()
        {
            Thread thread;

            if (this.isDownloading ||
                this.isUploading)
            {
                thread = new Thread(this.KeepAlive);
                thread.IsBackground = true;
                thread.Name = this.PeerId + " keeping alive";
                thread.Start();
            }
        }
        private void StartSending()
        {
            Thread thread;

            if (!this.isDownloading)
            {
                thread = new Thread(this.Send);
                thread.IsBackground = true;
                thread.Name = this.PeerId + " sender";
                thread.Start();
            }
        }
        private void StartUploading()
        {
            Thread thread;

            if (!this.isUploading)
            {
                thread = new Thread(this.Upload);
                thread.IsBackground = true;
                thread.Name = this.PeerId + "uploader";
                thread.Start();
            }
        }
        private void UpdateTrafficParameters(long downloaded, long uploaded)
        {
            downloaded.MustBeGreaterThanOrEqualTo(0);
            uploaded.MustBeGreaterThanOrEqualTo(0);

            lock (this.locker)
            {
                this.previouslyDownloaded += downloaded;
                this.previouslyUploaded += uploaded;

                if (this.stopwatch.Elapsed > TimeSpan.FromSeconds(1))
                {
                    this.downloadSpeed = (decimal)this.previouslyDownloaded / (decimal)this.stopwatch.Elapsed.TotalSeconds;
                    this.uploadSpeed = (decimal)this.previouslyUploaded / (decimal)this.stopwatch.Elapsed.TotalSeconds;

                    this.Downloaded += this.previouslyDownloaded;
                    this.Uploaded += this.previouslyUploaded;

                    this.previouslyDownloaded = 0;
                    this.previouslyUploaded = 0;

                    this.stopwatch.Restart();
                }
            }
        }
        private void Upload()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(250);
            Piece piece = null;
            RequestMessage rm;

            if (!this.isUploading)
            {
                this.isUploading = true;

                while (!this.IsDisposed)
                {
                    foreach (PeerMessage message in this.DequeueUploadMessages())
                    {
                        if (message is RequestMessage)
                        {
                            rm = message as RequestMessage;

                            if (piece == null ||
                                piece.PieceIndex != rm.PieceIndex)
                            {
                                // get the piece
                                piece = this.pieceManager.GetPiece(rm.PieceIndex);
                            }

                            if (piece != null &&
                                piece.PieceLength > rm.BlockOffset)
                            {
                                // return the piece
                                this.EnqueueSendMessage(new PieceMessage(rm.PieceIndex, rm.BlockOffset, (int)piece.GetBlockLength(rm.PieceIndex), piece.GetBlock(rm.PieceIndex)));
                            }
                            else
                            {
                                // invalid requeste received -> ignore
                            }
                        }
                        else if (message is CancelMessage)
                        {
                            // TODO
                        }
                        else if (message is InterestedMessage)
                        {
                            this.LeechingState = LeechingState.Interested;
                        }
                        else if (message is UninterestedMessage)
                        {
                            this.LeechingState = LeechingState.Uninterested;
                        }
                    }

                    Thread.Sleep(timeout);
                }

                this.isUploading = false;
            }
        }
    }
}
