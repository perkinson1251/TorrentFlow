using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;

namespace TorrentFlow.TorrentClientLibrary;

public sealed class Peer : IDisposable
{
    private readonly object locker = new();
    private PeerCommunicator communicator;
    private Queue<PeerMessage> downloadMessageQueue = new();
    private readonly object downloadMessageQueueLocker = new();
    private decimal downloadSpeed;
    private bool isDownloading;
    private bool isKeepingAlive;
    private bool isUploading;
    private DateTime? lastMessageReceivedTime;
    private DateTime? lastMessageSentTime;
    private readonly string localPeerId;
    private readonly PieceManager pieceManager;
    private long previouslyDownloaded;
    private long previouslyUploaded;
    private Queue<PeerMessage> sendMessageQueue = new();
    private readonly object sendMessageQueueLocker = new();
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private Queue<PeerMessage> uploadMessageQueue = new();
    private readonly object uploadMessageQueueLocker = new();
    private decimal uploadSpeed;

    public Peer(PeerCommunicator communicator, PieceManager pieceManager, string localPeerId, string peerId = null)
    {
        communicator.CannotBeNull();
        pieceManager.CannotBeNull();
        localPeerId.CannotBeNullOrEmpty();

        PeerId = peerId;

        this.localPeerId = localPeerId;

        BitField = new bool[pieceManager.PieceCount];

        HandshakeState = peerId == null ? HandshakeState.SentButNotReceived : HandshakeState.SendAndReceived;
        SeedingState = SeedingState.Choked;
        LeechingState = LeechingState.Uninterested;

        Downloaded = 0;
        Uploaded = 0;

        this.communicator = communicator;
        this.communicator.MessageReceived += Communicator_MessageReceived;
        this.communicator.CommunicationError += Communicator_CommunicationError;

        this.pieceManager = pieceManager;
        this.pieceManager.PieceCompleted += PieceManager_PieceCompleted;

        Endpoint = this.communicator.Endpoint;

        StartSending();
        StartDownloading();
        StartUploading();
        StartKeepingConnectionAlive();

        // send handshake
        EnqueueSendMessage(new HandshakeMessage(this.pieceManager.TorrentInfoHash, localPeerId));
    }

    private Peer()
    {
    }

    public bool[] BitField { get; }

    public long Downloaded { get; private set; }

    public decimal DownloadSpeed
    {
        get
        {
            UpdateTrafficParameters(0, 0);

            return downloadSpeed;
        }
    }

    public IPEndPoint Endpoint { get; }

    public HandshakeState HandshakeState { get; private set; }

    public bool IsDisposed { get; private set; }

    public LeechingState LeechingState { get; private set; }

    public string PeerId { get; private set; }

    public SeedingState SeedingState { get; private set; }

    public long Uploaded { get; private set; }

    public decimal UploadSpeed
    {
        get
        {
            UpdateTrafficParameters(0, 0);

            return uploadSpeed;
        }
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            Debug.WriteLine($"disposing peer {Endpoint}");

            if (communicator != null &&
                !communicator.IsDisposed)
            {
                communicator.Dispose();
                communicator = null;
            }
        }
    }

    public event EventHandler<PeerCommunicationErrorEventArgs> CommunicationErrorOccurred;

    private void CheckIfObjectIsDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
    }

    private void Communicator_CommunicationError(object sender, CommunicationErrorEventArgs e)
    {
        OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs(e.ErrorMessage, true));
    }

    private void Communicator_MessageReceived(object sender, PeerMessgeReceivedEventArgs e)
    {
        ProcessRecievedMessage(e.Message);

        UpdateTrafficParameters(e.Message.Length, 0);
    }

    private IEnumerable<PeerMessage> DequeueDownloadMessages()
    {
        IEnumerable<PeerMessage> messages;

        lock (downloadMessageQueueLocker)
        {
            messages = downloadMessageQueue;

            // TODO: optimize this by using a wait handler
            downloadMessageQueue = new Queue<PeerMessage>();
        }

        return messages;
    }

    private IEnumerable<PeerMessage> DequeueSendMessages()
    {
        IEnumerable<PeerMessage> messages;

        lock (sendMessageQueueLocker)
        {
            messages = sendMessageQueue;

            sendMessageQueue = new Queue<PeerMessage>();
        }

        return messages;
    }

    private IEnumerable<PeerMessage> DequeueUploadMessages()
    {
        IEnumerable<PeerMessage> messages;

        lock (uploadMessageQueueLocker)
        {
            messages = uploadMessageQueue;

            uploadMessageQueue = new Queue<PeerMessage>();
        }

        return messages;
    }

    // У файлі TorrentFlow/TorrentClientLibrary/PeerWireProtocol/Peer.cs
    private void Download()
    {
        var timeout = TimeSpan.FromMilliseconds(250);
        var chokeTimeout = TimeSpan.FromSeconds(10);
        var stopwatch = Stopwatch.StartNew(); // Ініціалізуємо stopwatch тут
        PieceMessage pm;
        Piece piece = null;
        var bitFieldData = Array.Empty<bool>();
        var pieceData = Array.Empty<byte>();
        var unchokeMessagesSent = 0;

        if (!isDownloading)
        {
            isDownloading = true;

            // Перевірка перед доступом до this.pieceManager при ініціалізації this.communicator.PieceData
            var initialPieceManager = pieceManager;
            if (initialPieceManager == null || initialPieceManager.IsDisposed)
            {
                Debug.WriteLine(
                    $"Peer {Endpoint} downloader: PieceManager is disposed or null at the start. Aborting download thread.");
                isDownloading = false;
                return; // Не починаємо потік, якщо PieceManager вже недоступний
            }

            // Також потрібна перевірка this.communicator
            if (communicator == null || communicator.IsDisposed)
            {
                Debug.WriteLine(
                    $"Peer {Endpoint} downloader: Communicator is disposed or null at the start. Aborting download thread.");
                isDownloading = false;
                return;
            }

            communicator.PieceData = new byte[initialPieceManager.PieceLength];

            while (!IsDisposed) // Головний цикл потоку
            {
                var currentPieceManager = pieceManager; // Отримуємо актуальне посилання на кожній ітерації

                if (currentPieceManager == null || currentPieceManager.IsDisposed)
                {
                    Debug.WriteLine(
                        $"Peer {Endpoint} downloader: PieceManager became disposed or null. Exiting download loop.");
                    break;
                }

                if (currentPieceManager.IsComplete)
                {
                    Debug.WriteLine($"Peer {Endpoint} downloader: All pieces complete. Exiting download loop.");
                    break;
                }

                // Обробка повідомлень з черги
                foreach (var message in DequeueDownloadMessages())
                {
                    if (IsDisposed) break; // Перевірка після отримання повідомлення, перед обробкою

                    if (message is PieceMessage pieceMsg) // Використовуємо pieceMsg для ясності
                    {
                        // Важливо: currentPieceManager все ще може бути null або disposed тут, якщо цикл щойно почався
                        // і DequeueDownloadMessages повернув щось до першої перевірки currentPieceManager
                        // Але основна перевірка на початку циклу while має це покрити.

                        // Логіка обробки PieceMessage (pm було перейменовано на pieceMsg)
                        if (piece != null &&
                            piece.PieceIndex == pieceMsg.PieceIndex && // Використовуємо pieceMsg
                            !piece.BitField[
                                pieceMsg.BlockOffset /
                                currentPieceManager.BlockLength]) // Використовуємо currentPieceManager
                        {
                            piece.PutBlock(pieceMsg.BlockOffset);

                            if (piece.IsCompleted || piece.IsCorrupted) piece = null;
                        }
                    }
                    else if (message is ChokeMessage)
                    {
                        SeedingState = SeedingState.Choked;
                        piece = null;
                    }
                    else if (message is UnchokeMessage)
                    {
                        SeedingState = SeedingState.Unchoked;
                        unchokeMessagesSent = 0;
                    }
                    // Додайте обробку інших типів повідомлень, якщо вони впливають на логіку завантаження
                }

                if (IsDisposed) break; // Перевірка після обробки черги

                // Логіка взаємодії на основі стану (Handshake, Choked, Unchoked)
                if (HandshakeState == HandshakeState.SendAndReceived)
                {
                    if (SeedingState == SeedingState.Choked)
                    {
                        if (stopwatch.Elapsed > chokeTimeout)
                        {
                            EnqueueSendMessage(new InterestedMessage());
                            if (++unchokeMessagesSent > 10)
                            {
                                OnCommunicationErrorOccurred(this,
                                    new PeerCommunicationErrorEventArgs(
                                        $"Choked for more than {TimeSpan.FromSeconds(chokeTimeout.TotalSeconds * 10).TotalSeconds} seconds.",
                                        true));
                                // Якщо CommunicationErrorOccurred викликає Dispose, то IsDisposed стане true
                                if (IsDisposed) break;
                            }

                            stopwatch.Restart();
                        }
                    }
                    else if (SeedingState == SeedingState.Unchoked)
                    {
                        if (piece == null) // Якщо немає активної частини для завантаження
                        {
                            // Знову перевіряємо currentPieceManager перед спробою знайти нову частину
                            if (currentPieceManager == null || currentPieceManager.IsDisposed)
                            {
                                Debug.WriteLine(
                                    $"Peer {Endpoint} downloader: PieceManager is disposed or null before finding new piece. Exiting download loop.");
                                break;
                            }

                            for (var pieceIndex = 0; pieceIndex < BitField.Length; pieceIndex++)
                            {
                                if (IsDisposed) break; // Перевірка всередині циклу пошуку

                                // Перевіряємо currentPieceManager.BitField
                                if (currentPieceManager.BitField[pieceIndex] == PieceStatus.Missing)
                                    if (BitField[pieceIndex] || currentPieceManager.IsEndGame)
                                    {
                                        // Перевірка currentPieceManager перед GetPieceLength / GetBlockCount
                                        if (currentPieceManager == null || currentPieceManager.IsDisposed) break;

                                        var currentPieceLength = currentPieceManager.GetPieceLength(pieceIndex);
                                        var currentBlockCount = currentPieceManager.GetBlockCount(pieceIndex);

                                        pieceData = pieceData.Length == currentPieceLength
                                            ? pieceData
                                            : new byte[currentPieceLength];
                                        bitFieldData = bitFieldData.Length == currentBlockCount
                                            ? bitFieldData
                                            : new bool[currentBlockCount];

                                        // Перевірка currentPieceManager перед CheckOut
                                        if (currentPieceManager == null || currentPieceManager.IsDisposed) break;
                                        piece = currentPieceManager.CheckOut(pieceIndex, pieceData, bitFieldData);

                                        if (piece != null)
                                        {
                                            if (communicator != null && !communicator.IsDisposed)
                                                communicator.PieceData = pieceData;
                                            break; // Знайшли частину, виходимо з циклу пошуку
                                        }
                                    }
                            }

                            if (IsDisposed || (currentPieceManager != null && currentPieceManager.IsDisposed))
                                break; // Перевірка після циклу пошуку


                            if (piece != null)
                            {
                                for (var i = 0; i < piece.BitField.Length; i++)
                                {
                                    if (IsDisposed) break;
                                    if (!piece.BitField[i])
                                        EnqueueSendMessage(new RequestMessage(piece.PieceIndex,
                                            (int)piece.GetBlockOffset(i),
                                            (int)piece.GetBlockLength(piece.GetBlockOffset(i))));
                                }

                                if (IsDisposed) break;
                            }
                        }
                    }
                }

                if (IsDisposed) break; // Перевірка перед сном

                Thread.Sleep(timeout);
            }

            isDownloading = false;
            Debug.WriteLine($"Peer {Endpoint} downloader: Download thread finished. IsDisposed: {IsDisposed}");
        }
    }

    private void EnqueueDownloadMessage(PeerMessage message)
    {
        message.CannotBeNull();

        lock (downloadMessageQueueLocker)
        {
            downloadMessageQueue.Enqueue(message);
        }
    }

    private void EnqueueSendMessage(PeerMessage message)
    {
        message.CannotBeNull();

        lock (sendMessageQueueLocker)
        {
            sendMessageQueue.Enqueue(message);
        }
    }

    private void EnqueueUploadMessage(PeerMessage message)
    {
        message.CannotBeNull();

        lock (uploadMessageQueueLocker)
        {
            uploadMessageQueue.Enqueue(message);
        }
    }

    private void KeepAlive()
    {
        var keepAliveTimeout = TimeSpan.FromSeconds(60);
        var timeout = TimeSpan.FromSeconds(10);

        if (!isKeepingAlive)
        {
            isKeepingAlive = true;

            while (!IsDisposed)
            {
                if (!isDownloading &&
                    !isUploading)
                    break;

                if (lastMessageSentTime == null &&
                    lastMessageReceivedTime == null)
                {
                    Thread.Sleep(timeout);
                }
                else if (DateTime.UtcNow - lastMessageSentTime > keepAliveTimeout ||
                         DateTime.UtcNow - lastMessageReceivedTime > keepAliveTimeout)
                {
                    OnCommunicationErrorOccurred(this,
                        new PeerCommunicationErrorEventArgs($"No message exchanged in over {keepAliveTimeout}.", true));

                    break;
                }
                else
                {
                    Thread.Sleep(timeout);
                }
            }

            isKeepingAlive = false;
        }
    }

    private void OnCommunicationErrorOccurred(object sender, PeerCommunicationErrorEventArgs e)
    {
        sender.CannotBeNull();
        e.CannotBeNull();

        if (CommunicationErrorOccurred != null) CommunicationErrorOccurred(sender, e);
    }

    private void PieceManager_PieceCompleted(object sender, PieceCompletedEventArgs e)
    {
        if (HandshakeState == HandshakeState.SendAndReceived) EnqueueSendMessage(new HaveMessage(e.PieceIndex));
    }

    private void ProcessRecievedMessage(PeerMessage message)
    {
        CheckIfObjectIsDisposed();

        lock (locker)
        {
            Debug.WriteLine($"{Endpoint} <- {message}");

            lastMessageReceivedTime = DateTime.UtcNow;

            if (message is HandshakeMessage)
            {
                ProcessRecievedMessage(message as HandshakeMessage);
            }
            else if (message is ChokeMessage)
            {
                ProcessRecievedMessage(message as ChokeMessage);
            }
            else if (message is UnchokeMessage)
            {
                ProcessRecievedMessage(message as UnchokeMessage);
            }
            else if (message is InterestedMessage)
            {
                ProcessRecievedMessage(message as InterestedMessage);
            }
            else if (message is UninterestedMessage)
            {
                ProcessRecievedMessage(message as UninterestedMessage);
            }
            else if (message is HaveMessage)
            {
                ProcessRecievedMessage(message as HaveMessage);
            }
            else if (message is BitFieldMessage)
            {
                ProcessRecievedMessage(message as BitFieldMessage);
            }
            else if (message is RequestMessage)
            {
                ProcessRecievedMessage(message as RequestMessage);
            }
            else if (message is PieceMessage)
            {
                ProcessRecievedMessage(message as PieceMessage);
            }
            else if (message is CancelMessage)
            {
                ProcessRecievedMessage(message as CancelMessage);
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

        if (HandshakeState == HandshakeState.SendAndReceived)
        {
            if (message.PieceIndex >= 0 &&
                message.PieceIndex < pieceManager.PieceCount &&
                message.BlockOffset >= 0 &&
                message.BlockOffset < pieceManager.PieceLength &&
                message.BlockOffset % pieceManager.BlockLength == 0 &&
                message.BlockLength ==
                pieceManager.GetBlockLength(message.PieceIndex, message.BlockOffset / pieceManager.BlockLength))
                EnqueueUploadMessage(message);
            else
                OnCommunicationErrorOccurred(this,
                    new PeerCommunicationErrorEventArgs("Invalid cancel message.", false));
        }
        else
        {
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
        }
    }

    private void ProcessRecievedMessage(PieceMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
        {
            if (message.PieceIndex >= 0 &&
                message.PieceIndex < pieceManager.PieceCount &&
                message.BlockOffset >= 0 &&
                message.BlockOffset < pieceManager.PieceLength &&
                message.BlockOffset % pieceManager.BlockLength == 0 &&
                message.Data.Length == pieceManager.GetPieceLength(message.PieceIndex))
                EnqueueDownloadMessage(message);
            else
                OnCommunicationErrorOccurred(this,
                    new PeerCommunicationErrorEventArgs("Invalid piece message.", false));
        }
        else
        {
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
        }
    }

    private void ProcessRecievedMessage(RequestMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
        {
            if (message.PieceIndex >= 0 &&
                message.PieceIndex < pieceManager.BlockCount &&
                message.BlockOffset >= 0 &&
                message.BlockOffset < pieceManager.GetBlockCount(message.PieceIndex) &&
                message.BlockOffset / pieceManager.BlockLength == 0 &&
                message.BlockLength ==
                pieceManager.GetBlockLength(message.PieceIndex, message.BlockOffset / pieceManager.BlockLength))
                EnqueueUploadMessage(message);
            else
                OnCommunicationErrorOccurred(this,
                    new PeerCommunicationErrorEventArgs("Invalid request message.", false));
        }
        else
        {
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
        }
    }

    private void ProcessRecievedMessage(BitFieldMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
        {
            if (message.BitField.Length >= pieceManager.BlockCount)
            {
                for (var i = 0; i < BitField.Length; i++) BitField[i] = message.BitField[i];

                // notify downloading thread
                EnqueueDownloadMessage(message);
            }
            else
            {
                OnCommunicationErrorOccurred(this,
                    new PeerCommunicationErrorEventArgs("Invalid bit field message.", true));
            }
        }
        else
        {
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
        }
    }

    private void ProcessRecievedMessage(ChokeMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
            EnqueueDownloadMessage(message);
        else
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
    }

    private void ProcessRecievedMessage(UnchokeMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
            EnqueueDownloadMessage(message);
        else
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
    }

    private void ProcessRecievedMessage(InterestedMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
            EnqueueDownloadMessage(message);
        else
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
    }

    private void ProcessRecievedMessage(UninterestedMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
            EnqueueUploadMessage(message);
        else
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
    }

    private void ProcessRecievedMessage(HaveMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.SendAndReceived)
        {
            if (message.PieceIndex >= 0 &&
                message.PieceIndex < pieceManager.PieceCount)
            {
                BitField[message.PieceIndex] = true;

                // notify downloading thread
                EnqueueDownloadMessage(message);
            }
            else
            {
                OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid have message.", false));
            }
        }
        else
        {
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
        }
    }

    private void ProcessRecievedMessage(HandshakeMessage message)
    {
        message.CannotBeNull();

        if (HandshakeState == HandshakeState.None ||
            HandshakeState == HandshakeState.SentButNotReceived)
        {
            if (message.InfoHash == pieceManager.TorrentInfoHash &&
                message.ProtocolString == HandshakeMessage.ProtocolName &&
                message.PeerId.IsNotNullOrEmpty() &&
                message.PeerId != localPeerId)
            {
                if (HandshakeState == HandshakeState.None)
                {
                    HandshakeState = HandshakeState.ReceivedButNotSent;
                    PeerId = message.PeerId;

                    // send a handshake
                    EnqueueSendMessage(new HandshakeMessage(pieceManager.TorrentInfoHash, localPeerId));

                    // send a bit field
                    EnqueueSendMessage(new BitFieldMessage(pieceManager.BitField.Select(x => x == PieceStatus.Present)
                        .ToArray()));
                }
                else if (HandshakeState == HandshakeState.SentButNotReceived)
                {
                    HandshakeState = HandshakeState.SendAndReceived;
                    PeerId = message.PeerId;

                    // send a bit field
                    EnqueueSendMessage(new BitFieldMessage(pieceManager.BitField.Select(x => x == PieceStatus.Present)
                        .ToArray()));
                }
            }
            else
            {
                OnCommunicationErrorOccurred(this,
                    new PeerCommunicationErrorEventArgs("Invalid handshake message.", true));
            }
        }
        else
        {
            OnCommunicationErrorOccurred(this, new PeerCommunicationErrorEventArgs("Invalid message sequence.", true));
        }
    }

    private void Send()
    {
        var timeout = TimeSpan.FromMilliseconds(250);
        IEnumerable<PeerMessage> messages;

        while (!IsDisposed)
        {
            messages = DequeueSendMessages();

            if (messages.Count() > 0)
                if (communicator != null &&
                    !communicator.IsDisposed)
                {
                    foreach (var message in messages) Debug.WriteLine($"{Endpoint} -> {message}");

                    // send message
                    communicator.Send(messages);

                    UpdateTrafficParameters(0, messages.Sum(x => x.Length));
                }

            lastMessageSentTime = DateTime.UtcNow;

            Thread.Sleep(timeout);
        }
    }

    private void StartDownloading()
    {
        Thread thread;

        if (!isDownloading)
        {
            thread = new Thread(Download);
            thread.IsBackground = true;
            thread.Name = PeerId + " downloader";
            thread.Start();
        }
    }

    private void StartKeepingConnectionAlive()
    {
        Thread thread;

        if (isDownloading ||
            isUploading)
        {
            thread = new Thread(KeepAlive);
            thread.IsBackground = true;
            thread.Name = PeerId + " keeping alive";
            thread.Start();
        }
    }

    private void StartSending()
    {
        Thread thread;

        if (!isDownloading)
        {
            thread = new Thread(Send);
            thread.IsBackground = true;
            thread.Name = PeerId + " sender";
            thread.Start();
        }
    }

    private void StartUploading()
    {
        Thread thread;

        if (!isUploading)
        {
            thread = new Thread(Upload);
            thread.IsBackground = true;
            thread.Name = PeerId + "uploader";
            thread.Start();
        }
    }

    private void UpdateTrafficParameters(long downloaded, long uploaded)
    {
        downloaded.MustBeGreaterThanOrEqualTo(0);
        uploaded.MustBeGreaterThanOrEqualTo(0);

        lock (locker)
        {
            previouslyDownloaded += downloaded;
            previouslyUploaded += uploaded;

            if (stopwatch.Elapsed > TimeSpan.FromSeconds(1))
            {
                downloadSpeed = previouslyDownloaded / (decimal)stopwatch.Elapsed.TotalSeconds;
                uploadSpeed = previouslyUploaded / (decimal)stopwatch.Elapsed.TotalSeconds;

                Downloaded += previouslyDownloaded;
                Uploaded += previouslyUploaded;

                previouslyDownloaded = 0;
                previouslyUploaded = 0;

                stopwatch.Restart();
            }
        }
    }

    private void Upload()
    {
        var timeout = TimeSpan.FromMilliseconds(250);
        Piece piece = null;
        RequestMessage rm;

        if (!isUploading)
        {
            isUploading = true;

            while (!IsDisposed)
            {
                foreach (var message in DequeueUploadMessages())
                    if (message is RequestMessage)
                    {
                        rm = message as RequestMessage;

                        if (piece == null ||
                            piece.PieceIndex != rm.PieceIndex)
                            // get the piece
                            piece = pieceManager.GetPiece(rm.PieceIndex);

                        if (piece != null &&
                            piece.PieceLength > rm.BlockOffset)
                            // return the piece
                            EnqueueSendMessage(new PieceMessage(rm.PieceIndex, rm.BlockOffset,
                                (int)piece.GetBlockLength(rm.PieceIndex), piece.GetBlock(rm.PieceIndex)));
                        // invalid requeste received -> ignore
                    }
                    else if (message is CancelMessage)
                    {
                        // TODO
                    }
                    else if (message is InterestedMessage)
                    {
                        LeechingState = LeechingState.Interested;
                    }
                    else if (message is UninterestedMessage)
                    {
                        LeechingState = LeechingState.Uninterested;
                    }

                Thread.Sleep(timeout);
            }

            isUploading = false;
        }
    }
}