using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Exceptions;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class PersistenceManager : IDisposable
    {
        private Dictionary<TorrentFileInfo, FileStream> files;
        private object locker = new object();
        private IEnumerable<string> pieceHashes;
        private long pieceLength;
        private long torrentLength;
        public PersistenceManager(string directoryPath, long torrentLength, long pieceLength, IEnumerable<string> pieceHashes, IEnumerable<TorrentFileInfo> files)
        {
            directoryPath.CannotBeNullOrEmpty();
            directoryPath.MustBeValidDirectoryPath();
            files.CannotBeNullOrEmpty();
            pieceLength.MustBeGreaterThan(0);
            pieceHashes.CannotBeNullOrEmpty();

            Debug.WriteLine($"creating persistence manager for {Path.GetFullPath(directoryPath)}");

            this.DirectoryPath = directoryPath;
            this.torrentLength = torrentLength;
            this.pieceLength = pieceLength;
            this.pieceHashes = pieceHashes;

            // initialize file handlers
            this.files = new Dictionary<TorrentFileInfo, FileStream>();

            foreach (var file in files)
            {
                if (file.Download)
                {
                    this.CreateFile(Path.Combine(this.DirectoryPath, file.FilePath), file.Length);

                    this.files.Add(file, new FileStream(Path.Combine(this.DirectoryPath, file.FilePath), FileMode.Open, FileAccess.ReadWrite, FileShare.None));
                }
            }
        }
        private PersistenceManager()
        {
        }
        public string DirectoryPath
        {
            get;
            private set;
        }
        public bool IsDisposed
        {
            get;
            private set;
        }
        public void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.IsDisposed = true;

                Debug.WriteLine($"disposing persistence manager for {this.DirectoryPath}");

                foreach (var file in this.files)
                {
                    file.Value.Close();
                    file.Value.Dispose();
                }

                this.files.Clear();
            }
        }
        public byte[] Get(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);

            long pieceStart;
            long pieceEnd;
            long torrentStartOffset = 0;
            long torrentEndOffset;
            long fileOffset;
            byte[] pieceData;
            int pieceOffset = 0;
            int length = 0;

            this.CheckIfObjectIsDisposed();

            lock (this.locker)
            {
                // calculate length of the data read (it could be less than the specified piece length)
                foreach (var file in this.files)
                {
                    torrentEndOffset = torrentStartOffset + file.Key.Length;

                    pieceStart = (torrentStartOffset - (torrentStartOffset % this.pieceLength)) / this.pieceLength;

                    pieceEnd = (torrentEndOffset - (torrentEndOffset % this.pieceLength)) / this.pieceLength;
                    pieceEnd -= torrentEndOffset % this.pieceLength == 0 ? 1 : 0;

                    if (pieceIndex >= pieceStart &&
                        pieceIndex <= pieceEnd)
                    {
                        fileOffset = (pieceIndex - pieceStart) * this.pieceLength;
                        fileOffset -= pieceIndex > pieceStart ? torrentStartOffset % this.pieceLength : 0;

                        length += (int)Math.Min(this.pieceLength - length, file.Key.Length - fileOffset);
                    }
                    else if (pieceIndex < pieceStart)
                    {
                        break;
                    }

                    torrentStartOffset += file.Key.Length;
                }

                if (length > 0)
                {
                    pieceData = new byte[length];
                    torrentStartOffset = 0;
                    length = 0;

                    // read the piece
                    foreach (var file in this.files)
                    {
                        torrentEndOffset = torrentStartOffset + file.Key.Length;

                        pieceStart = (torrentStartOffset - (torrentStartOffset % this.pieceLength)) / this.pieceLength;

                        pieceEnd = (torrentEndOffset - (torrentEndOffset % this.pieceLength)) / this.pieceLength;
                        pieceEnd -= torrentEndOffset % this.pieceLength == 0 ? 1 : 0;

                        if (pieceIndex >= pieceStart &&
                            pieceIndex <= pieceEnd)
                        {
                            fileOffset = (pieceIndex - pieceStart) * this.pieceLength;
                            fileOffset -= pieceIndex > pieceStart ? torrentStartOffset % this.pieceLength : 0;

                            length = (int)Math.Min(this.pieceLength - pieceOffset, file.Key.Length - fileOffset);

                            if (file.Key.Download)
                            {
                                this.Read(file.Value, fileOffset, length, pieceData, pieceOffset);
                            }

                            pieceOffset += length;
                        }
                        else if (pieceIndex < pieceStart)
                        {
                            break;
                        }

                        torrentStartOffset = torrentEndOffset;
                    }
                }
                else
                {
                    throw new TorrentPersistanceException("File cannot be empty.");
                }
            }

            return pieceData;
        }
        public void Put(IEnumerable<TorrentFileInfo> files, long pieceLength, long pieceIndex, byte[] pieceData)
        {
            files.CannotBeNullOrEmpty();
            pieceLength.MustBeGreaterThan(0);
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceData.CannotBeNullOrEmpty();

            long pieceStart;
            long pieceEnd;
            long torrentStartOffset = 0;
            long torrentEndOffset = 0;
            long fileOffset;
            int pieceOffset = 0;
            int length = 0;

            this.CheckIfObjectIsDisposed();

            lock (this.locker)
            {
                // verify length of the data written
                foreach (var file in files)
                {
                    torrentEndOffset = torrentStartOffset + file.Length;

                    pieceStart = (torrentStartOffset - (torrentStartOffset % pieceLength)) / pieceLength;

                    pieceEnd = (torrentEndOffset - (torrentEndOffset % pieceLength)) / pieceLength;
                    pieceEnd -= torrentEndOffset % pieceLength == 0 ? 1 : 0;

                    if (pieceIndex >= pieceStart &&
                        pieceIndex <= pieceEnd)
                    {
                        fileOffset = (pieceIndex - pieceStart) * pieceLength;
                        fileOffset -= pieceIndex > pieceStart ? torrentStartOffset % pieceLength : 0;

                        length += (int)Math.Min(pieceLength - length, file.Length - fileOffset);
                    }
                    else if (pieceIndex < pieceStart)
                    {
                        break;
                    }

                    torrentStartOffset = torrentEndOffset;
                }

                if (length == pieceData.Length)
                {
                    torrentStartOffset = 0;
                    length = 0;

                    // write the piece
                    foreach (var file in this.files)
                    {
                        torrentEndOffset = torrentStartOffset + file.Key.Length;

                        pieceStart = (torrentStartOffset - (torrentStartOffset % pieceLength)) / pieceLength;

                        pieceEnd = (torrentEndOffset - (torrentEndOffset % pieceLength)) / pieceLength;
                        pieceEnd -= torrentEndOffset % pieceLength == 0 ? 1 : 0;

                        if (pieceIndex >= pieceStart &&
                            pieceIndex <= pieceEnd)
                        {
                            fileOffset = (pieceIndex - pieceStart) * pieceLength;
                            fileOffset -= pieceIndex > pieceStart ? torrentStartOffset % pieceLength : 0;

                            length = (int)Math.Min(pieceLength - pieceOffset, file.Key.Length - fileOffset);

                            if (file.Key.Download)
                            {
                                this.Write(file.Value, fileOffset, length, pieceData, pieceOffset);
                            }

                            pieceOffset += length;
                        }
                        else if (pieceIndex < pieceStart)
                        {
                            break;
                        }

                        torrentStartOffset = torrentEndOffset;
                    }
                }
                else
                {
                    throw new TorrentPersistanceException("Invalid length.");
                }
            }
        }
        public PieceStatus[] Verify()
        {
            PieceStatus[] bitField = new PieceStatus[this.pieceHashes.Count()];
            long pieceStart;
            long pieceEnd;
            long previousPieceIndex = 0;
            long torrentStartOffset = 0;
            long torrentEndOffset;
            long fileOffset;
            byte[] pieceData = new byte[this.pieceLength];
            int pieceOffset = 0;
            int length = 0;
            bool ignore = false;
            bool download = false;

            this.CheckIfObjectIsDisposed();

            lock (this.locker)
            {
                foreach (var file in this.files)
                {
                    torrentEndOffset = torrentStartOffset + file.Key.Length;

                    pieceStart = (torrentStartOffset - (torrentStartOffset % this.pieceLength)) / this.pieceLength;

                    pieceEnd = (torrentEndOffset - (torrentEndOffset % this.pieceLength)) / this.pieceLength;
                    pieceEnd -= torrentEndOffset % this.pieceLength == 0 ? 1 : 0;

                    Debug.WriteLine($"verifying file {file.Value.Name}");

                    for (long pieceIndex = pieceStart; pieceIndex <= pieceEnd; pieceIndex++)
                    {
                        if (pieceIndex > previousPieceIndex)
                        {
                            bitField[previousPieceIndex] = this.GetStatus(ignore, download, this.pieceHashes.ElementAt((int)previousPieceIndex), pieceData.CalculateSha1Hash(0, pieceOffset).ToHexaDecimalString());

                            previousPieceIndex = pieceIndex;
                            pieceOffset = 0;
                            ignore = false;
                            download = false;
                        }

                        fileOffset = (pieceIndex - pieceStart) * this.pieceLength;
                        fileOffset -= pieceIndex > pieceStart ? torrentStartOffset % this.pieceLength : 0;

                        length = (int)Math.Min(this.pieceLength - pieceOffset, file.Key.Length - fileOffset);

                        if (file.Key.Download)
                        {
                            this.Read(file.Value, fileOffset, length, pieceData, pieceOffset);

                            download = true;
                        }
                        else
                        {
                            ignore = true;
                        }

                        ignore = ignore && !file.Key.Download;
                        download = download || file.Key.Download;

                        pieceOffset += length;
                    }

                    torrentStartOffset = torrentEndOffset;
                }

                // last piece
                bitField[previousPieceIndex] = this.GetStatus(ignore, download, this.pieceHashes.ElementAt((int)previousPieceIndex), pieceData.CalculateSha1Hash(0, pieceOffset).ToHexaDecimalString());
            }

            return bitField;
        }
        private void CheckIfObjectIsDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("TorrentClient");
            }
        }
        private void CreateFile(string filePath, long fileLength)
        {
            filePath.CannotBeNullOrEmpty();
            filePath.MustBeValidFilePath();
            // fileLength.MustBeGreaterThan(0); // Розгляньте, чи можуть бути файли нульової довжини в торенті.
            // Якщо так, змініть на MustBeGreaterThanOrEqualTo(0)
            // Або залиште MustBeGreaterThan(0), якщо це дійсно вимога.
            // Поточна TorrentFileInfo вимагає length > 0.

            this.CheckIfObjectIsDisposed();

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Debug.WriteLine($"creating directory {directory}");
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (stream.Length != fileLength)
                {
                    Debug.WriteLine($"Setting length of file {filePath} to {fileLength}. Previous length: {stream.Length}");
                    try
                    {
                        stream.SetLength(fileLength);
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine($"Error setting file length for {filePath}: {ex.Message}");
                        throw new TorrentPersistanceException($"Error setting file length for {filePath}: {ex.Message}", ex);
                    }
                }
            }
        }
        private PieceStatus GetStatus(bool ignore, bool download, string pieceHash, string calculatedPieceHash)
        {
            pieceHash.CannotBeNullOrEmpty();
            calculatedPieceHash.CannotBeNullOrEmpty();
            pieceHash.Length.MustBeEqualTo(calculatedPieceHash.Length);

            if (download &&
                !ignore)
            {
                for (int i = 0; i < pieceHash.Length; i++)
                {
                    if (pieceHash[i] != calculatedPieceHash[i])
                    {
                        return PieceStatus.Missing;
                    }
                }

                return PieceStatus.Present;
            }
            else if (download &&
                     ignore)
            {
                return PieceStatus.Partial;
            }
            else if (!download &&
                     ignore)
            {
                return PieceStatus.Ignore;
            }
            else
            {
                throw new TorrentPersistanceException("Invalid piece status.");
            }
        }
        private void Read(FileStream stream, long offset, int length, byte[] buffer, int bufferOffset)
        {
            stream.CannotBeNull();
            offset.MustBeGreaterThanOrEqualTo(0);
            length.MustBeGreaterThan(0);
            buffer.CannotBeNullOrEmpty();
            bufferOffset.MustBeGreaterThanOrEqualTo(0);
            bufferOffset.MustBeLessThanOrEqualTo(buffer.Length - length);

            if (stream.Length >= offset + length)
            {
                stream.Position = offset;
                stream.Read(buffer, bufferOffset, length);
            }
            else
            {
                throw new TorrentPersistanceException("Incorrect file length.");
            }
        }
        private void Write(FileStream stream, long offset, int length, byte[] buffer, int bufferOffset = 0)
        {
            stream.CannotBeNull();
            offset.MustBeGreaterThanOrEqualTo(0);
            buffer.CannotBeNullOrEmpty();
            length.MustBeGreaterThan(0);
            length.MustBeLessThanOrEqualTo(buffer.Length);
            bufferOffset.MustBeGreaterThanOrEqualTo(0);
            bufferOffset.MustBeLessThanOrEqualTo((int)(buffer.Length - length));

            if (stream.Length >= offset + length)
            {
                stream.Position = offset;
                stream.Write(buffer, bufferOffset, length);
            }
            else
            {
                throw new TorrentPersistanceException("Incorrect file length.");
            }
        }
    }
}
