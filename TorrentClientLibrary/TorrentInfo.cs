using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.BEncoding;
using TorrentFlow.TorrentClientLibrary.Exceptions;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TorrentInfo
    {
        private BEncodedDictionary dictionary;
        private TorrentInfo()
        {
        }
        private TorrentInfo(BEncodedDictionary dictionary, string infoHash, long pieceLength, IEnumerable<string> piecesHashValues, bool isPrivate, IEnumerable<Uri> announceList, DateTime? creationDate, string comment, string createdBy, Encoding encoding, IEnumerable<TorrentFileInfo> files)
        {
            dictionary.CannotBeNull();
            infoHash.CannotBeNullOrEmpty();
            infoHash.Length.MustBeEqualTo(40);
            pieceLength.MustBeGreaterThan(0);
            piecesHashValues.CannotBeNullOrEmpty();
            announceList.CannotBeNullOrEmpty();
            encoding.CannotBeNull();
            files.CannotBeNullOrEmpty();

            this.dictionary = dictionary;
            this.InfoHash = infoHash;
            this.PieceLength = pieceLength;
            this.PieceHashes = piecesHashValues;
            this.IsPrivate = isPrivate;
            this.AnnounceList = announceList;
            this.CreationDate = creationDate;
            this.Comment = comment;
            this.CreatedBy = createdBy;
            this.Encoding = encoding;
            this.Files = files;
            this.Length = files.Sum(x => x.Length);
            this.PiecesCount = piecesHashValues.Count();
            this.BlockLength = PieceMessage.DefaultBlockLength;
            this.BlocksCount = (int)(pieceLength / PieceMessage.DefaultBlockLength);
        }
        public IEnumerable<Uri> AnnounceList
        {
            get;
            private set;
        }
        public int BlockLength
        {
            get;
            private set;
        }
        public int BlocksCount
        {
            get;
            private set;
        }
        public string Comment
        {
            get;
            private set;
        }
        public string CreatedBy
        {
            get;
            private set;
        }
        public DateTime? CreationDate
        {
            get;
            private set;
        }
        public Encoding Encoding
        {
            get;
            private set;
        }
        public IEnumerable<TorrentFileInfo> Files
        {
            get;
            private set;
        }
        public string InfoHash
        {
            get;
            private set;
        }
        public bool IsPrivate
        {
            get;
            private set;
        }
        public long Length
        {
            get;
            private set;
        }
        public IEnumerable<string> PieceHashes
        {
            get;
            private set;
        }
        public long PieceLength
        {
            get;
            private set;
        }
        public int PiecesCount
        {
            get;
            private set;
        }
        public static bool TryLoad(byte[] data, out TorrentInfo torrentInfo)
        {
            data.CannotBeNullOrEmpty();

            BEncodedValue value;
            BEncodedDictionary general;
            BEncodedDictionary info;
            List<TorrentFileInfo> files = new List<TorrentFileInfo>();
            long pieceLength;
            List<string> pieceHashes = new List<string>();
            bool isPrivate = false;
            Uri tmpUri;
            List<Uri> announceList = new List<Uri>();
            DateTime? creationDate = null;
            string comment = null;
            string createdBy = null;
            Encoding encoding = Encoding.ASCII;
            string filePath;
            long fileLength = 0;
            string fileHash;
            string tmpString;
            BEncodedString infoKey = new BEncodedString("info");
            BEncodedString pieceLengthKey = new BEncodedString("piece length");
            BEncodedString piecesKey = new BEncodedString("pieces");
            BEncodedString privateKey = new BEncodedString("private");
            BEncodedString nameKey = new BEncodedString("name");
            BEncodedString lengthKey = new BEncodedString("length");
            BEncodedString md5sumKey = new BEncodedString("md5sum");
            BEncodedString filesKey = new BEncodedString("files");
            BEncodedString pathKey = new BEncodedString("path");
            BEncodedString announceKey = new BEncodedString("announce");
            BEncodedString announceListKey = new BEncodedString("announce-list");
            BEncodedString creationDateKey = new BEncodedString("creation date");
            BEncodedString commentKey = new BEncodedString("comment");
            BEncodedString createdByKey = new BEncodedString("created by");
            BEncodedString encodingKey = new BEncodedString("encoding");

            torrentInfo = null;

            try
            {
                value = BEncodedValue.Decode(data);
            }
            catch (BEncodingException)
            {
                return false;
            }

            if (value is BEncodedDictionary)
            {
                general = value as BEncodedDictionary;

                if (general.ContainsKey(infoKey) &&
                    general[infoKey] is BEncodedDictionary)
                {
                    info = general[infoKey] as BEncodedDictionary;

                    // piece length
                    if (info.ContainsKey(pieceLengthKey) &&
                        info[pieceLengthKey] is BEncodedNumber)
                    {
                        pieceLength = info[pieceLengthKey].As<BEncodedNumber>().Number;
                    }
                    else
                    {
                        return false;
                    }

                    // pieces
                    if (info.ContainsKey(piecesKey) &&
                        info[piecesKey] is BEncodedString &&
                        info[piecesKey].As<BEncodedString>().TextBytes.Length % 20 == 0)
                    {
                        for (int i = 0; i < info[piecesKey].As<BEncodedString>().TextBytes.Length; i += 20)
                        {
                            byte[] tmpBytes = new byte[20];

                            Array.Copy(info[piecesKey].As<BEncodedString>().TextBytes, i, tmpBytes, 0, tmpBytes.Length);

                            pieceHashes.Add(tmpBytes.ToHexaDecimalString());
                        }

                        if (pieceHashes.Count == 0)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }

                    // is private
                    if (info.ContainsKey(privateKey) &&
                        info[privateKey] is BEncodedNumber)
                    {
                        isPrivate = info[privateKey].As<BEncodedNumber>().Number == 1;
                    }

                    // files
                    if (info.ContainsKey(nameKey) &&
                        info[nameKey] is BEncodedString &&
                        info.ContainsKey(lengthKey) &&
                        info[lengthKey] is BEncodedNumber)
                    {
                        // single file
                        filePath = info[nameKey].As<BEncodedString>().Text;
                        fileLength = info[lengthKey].As<BEncodedNumber>().Number;

                        if (info.ContainsKey(md5sumKey) &&
                            info[md5sumKey] is BEncodedString)
                        {
                            fileHash = info[md5sumKey].As<BEncodedString>().Text;
                        }
                        else
                        {
                            fileHash = null;
                        }

                        files.Add(new TorrentFileInfo(filePath, fileHash, fileLength));
                    }
                    else if (info.ContainsKey(nameKey) &&
                             info[nameKey] is BEncodedString &&
                             info.ContainsKey(filesKey) &&
                             info[filesKey] is BEncodedList)
                    {
                        tmpString = info[nameKey].As<BEncodedString>().Text;

                        // multi file
                        foreach (var item in info[filesKey].As<BEncodedList>())
                        {
                            if (item is BEncodedDictionary &&
                                item.As<BEncodedDictionary>().ContainsKey(pathKey) &&
                                item.As<BEncodedDictionary>()[pathKey] is BEncodedList &&
                                item.As<BEncodedDictionary>()[pathKey].As<BEncodedList>().All(x => x is BEncodedString) &&
                                item.As<BEncodedDictionary>().ContainsKey(lengthKey) &&
                                item.As<BEncodedDictionary>()[lengthKey] is BEncodedNumber)
                            {
                                filePath = Path.Combine(tmpString, Path.Combine(item.As<BEncodedDictionary>()[pathKey].As<BEncodedList>().Select(x => x.As<BEncodedString>().Text).ToArray()));
                                fileLength = item.As<BEncodedDictionary>()[lengthKey].As<BEncodedNumber>().Number;

                                if (item.As<BEncodedDictionary>().ContainsKey(md5sumKey) &&
                                    item.As<BEncodedDictionary>()[md5sumKey] is BEncodedString)
                                {
                                    fileHash = item.As<BEncodedDictionary>()[md5sumKey].As<BEncodedString>().Text;
                                }
                                else
                                {
                                    fileHash = null;
                                }

                                files.Add(new TorrentFileInfo(filePath, fileHash, fileLength));
                            }
                            else
                            {
                                return false;
                            }
                        }

                        if (files.Count == 0)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                // announce
                if (general.ContainsKey(announceKey) &&
                    general[announceKey] is BEncodedString &&
                    Uri.TryCreate(general[announceKey].As<BEncodedString>().Text, UriKind.Absolute, out tmpUri))
                {
                    announceList.Add(tmpUri);
                }
                else
                {
                    return false;
                }

                // announce list
                if (general.ContainsKey(announceListKey) &&
                    general[announceListKey] is BEncodedList)
                {
                    foreach (var item in general[announceListKey].As<BEncodedList>())
                    {
                        if (item is BEncodedList)
                        {
                            foreach (var item2 in item.As<BEncodedList>())
                            {
                                if (Uri.TryCreate(item2.As<BEncodedString>().Text, UriKind.Absolute, out tmpUri))
                                {
                                    announceList.Add(tmpUri);
                                }
                            }
                        }
                    }

                    announceList = announceList.Select(x => x.AbsoluteUri).Distinct().Select(x => new Uri(x)).ToList();
                }

                // creation adte
                if (general.ContainsKey(creationDateKey) &&
                    general[creationDateKey] is BEncodedNumber)
                {
                    creationDate = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(general[creationDateKey].As<BEncodedNumber>().Number).ToLocalTime();
                }

                // comment
                if (general.ContainsKey(commentKey) &&
                    general[commentKey] is BEncodedString)
                {
                    comment = general[commentKey].As<BEncodedString>().Text;
                }

                // created by
                if (general.ContainsKey(createdByKey) &&
                    general[createdByKey] is BEncodedString)
                {
                    createdBy = general[createdByKey].As<BEncodedString>().Text;
                }

                // encoding
                if (general.ContainsKey(encodingKey) &&
                    general[encodingKey] is BEncodedString)
                {
                    if (general[encodingKey].As<BEncodedString>().Text == "UTF8")
                    {
                        encoding = Encoding.UTF8;
                    }
                }

                torrentInfo = new TorrentInfo(
                    general,
                    info.Encode().CalculateSha1Hash().ToHexaDecimalString(),
                    pieceLength,
                    new ReadOnlyCollection<string>(pieceHashes),
                    isPrivate,
                    new ReadOnlyCollection<Uri>(announceList),
                    creationDate,
                    comment,
                    createdBy,
                    encoding,
                    new ReadOnlyCollection<TorrentFileInfo>(files));

                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool TryLoad(string torrentInfoFilePath, out TorrentInfo torrentInfo)
        {
            torrentInfoFilePath.MustBeValidFilePath();
            torrentInfoFilePath.MustFileExist();

            return TryLoad(File.ReadAllBytes(torrentInfoFilePath), out torrentInfo);
        }
        public byte[] Encode()
        {
            return this.dictionary.Encode();
        }
        public int GetBlockCount(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThan(this.PiecesCount);

            return (int)Math.Ceiling((decimal)this.GetPieceLength(pieceIndex) / (decimal)this.BlockLength);
        }
        public int GetBlockLength(int pieceIndex, int blockIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThan(this.PiecesCount);
            blockIndex.MustBeGreaterThanOrEqualTo(0);
            blockIndex.MustBeLessThan(this.GetBlockCount(pieceIndex));

            long pieceLength;
            long blockCount;

            blockCount = this.GetBlockCount(pieceIndex);

            if (blockIndex == blockCount - 1)
            {
                pieceLength = this.GetPieceLength(pieceIndex);

                if (pieceLength % this.BlockLength != 0)
                {
                    // last block can be shorter
                    return (int)(pieceLength % this.BlockLength);
                }
            }

            return this.BlockLength;
        }
        public int GetEndPieceIndex(string filePath)
        {
            filePath.CannotBeNullOrEmpty();

            TorrentFileInfo info;
            int pieceStart = 0;
            int pieceEnd;

            for (int i = 0; i < this.Files.Count(); i++)
            {
                info = this.Files.ElementAt(i);

                pieceEnd = pieceStart + (int)Math.Ceiling((decimal)info.Length / (decimal)this.PieceLength) - 1;

                if (info.FilePath == filePath)
                {
                    return pieceEnd;
                }

                pieceStart = pieceEnd + 1;
            }

            throw new TorrentInfoException("File path is not present.");
        }
        public TorrentFileInfo GetFile(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThanOrEqualTo(this.PiecesCount);

            TorrentFileInfo info;
            int pieceStart = 0;
            int pieceEnd;

            for (int i = 0; i < this.Files.Count(); i++)
            {
                info = this.Files.ElementAt(i);

                pieceEnd = pieceStart + (int)Math.Ceiling((decimal)info.Length / (decimal)this.PieceLength) - 1;

                if (pieceIndex >= pieceStart &&
                    pieceIndex <= pieceEnd)
                {
                    return info;
                }
                else
                {
                    pieceStart = pieceEnd + 1;
                }
            }

            throw new TorrentInfoException($"Piece {pieceIndex} not found.");
        }
        public long GetPieceLength(int pieceIndex)
        {
            pieceIndex.MustBeGreaterThanOrEqualTo(0);
            pieceIndex.MustBeLessThan(this.PiecesCount);

            if (pieceIndex == this.PiecesCount - 1)
            {
                if (this.Length % this.PieceLength != 0)
                {
                    // last piece can be shorter
                    return this.Length % this.PieceLength;
                }
            }

            return this.PieceLength;
        }
        public int GetStartPieceIndex(string filePath)
        {
            filePath.CannotBeNullOrEmpty();

            TorrentFileInfo info;
            int pieceStart = 0;
            int pieceEnd;

            for (int i = 0; i < this.Files.Count(); i++)
            {
                info = this.Files.ElementAt(i);

                pieceEnd = pieceStart + (int)Math.Ceiling((decimal)info.Length / (decimal)this.PieceLength) - 1;

                if (info.FilePath == filePath)
                {
                    return pieceStart;
                }

                pieceStart = pieceEnd + 1;
            }

            throw new TorrentInfoException($"File {filePath} does not exist.");
        }
        public void Save(string torrentInfoFilePath)
        {
            torrentInfoFilePath.MustBeValidFilePath();

            if (File.Exists(torrentInfoFilePath))
            {
                File.Delete(torrentInfoFilePath);
            }

            File.WriteAllBytes(torrentInfoFilePath, this.dictionary.Encode());
        }
    }
}
