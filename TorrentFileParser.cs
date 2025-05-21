using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TorrentFlow
{
    public class TorrentFileParser
    {
        public async Task<TorrentFileContent?> ParseFileAsync(string filePath)
        {
            try
            {
                byte[] allFileBytes = await File.ReadAllBytesAsync(filePath);
                object decodedObject = BEncoding.Decode(allFileBytes); // Декодуємо весь файл

                if (decodedObject is not Dictionary<string, object> rootDictionary)
                {
                    System.Diagnostics.Debug.WriteLine("Помилка парсингу: кореневий об'єкт не є словником.");
                    return null;
                }

                var torrentContent = new TorrentFileContent();

                // Парсинг основних полів (залишається без змін)
                if (rootDictionary.TryGetValue("announce", out var announceObj) && announceObj is byte[] announceBytes)
                    torrentContent.Announce = Encoding.UTF8.GetString(announceBytes);

                if (rootDictionary.TryGetValue("announce-list", out var announceListObj) && announceListObj is List<object> announceListRaw)
                {
                    torrentContent.AnnounceList = new List<List<string>>();
                    foreach (var tierObj in announceListRaw)
                    {
                        if (tierObj is List<object> tierRaw)
                        {
                            var tierList = new List<string>();
                            foreach (var trackerObj in tierRaw)
                            {
                                if (trackerObj is byte[] trackerBytes)
                                {
                                    tierList.Add(Encoding.UTF8.GetString(trackerBytes));
                                }
                            }
                            if (tierList.Any())
                                torrentContent.AnnounceList.Add(tierList);
                        }
                    }
                }
                
                if (rootDictionary.TryGetValue("creation date", out var creationDateObj) && creationDateObj is long creationDate)
                    torrentContent.CreationDate = creationDate;
                
                if (rootDictionary.TryGetValue("comment", out var commentObj) && commentObj is byte[] commentBytes)
                    torrentContent.Comment = Encoding.UTF8.GetString(commentBytes);

                if (rootDictionary.TryGetValue("created by", out var createdByObj) && createdByObj is byte[] createdByBytes)
                    torrentContent.CreatedBy = Encoding.UTF8.GetString(createdByBytes);
                
                if (rootDictionary.TryGetValue("encoding", out var encodingObj) && encodingObj is byte[] encodingBytes)
                    torrentContent.Encoding = Encoding.UTF8.GetString(encodingBytes);

                // Парсинг словника "info"
                if (rootDictionary.TryGetValue("info", out var infoObjFromParsed) && infoObjFromParsed is Dictionary<string, object> infoDictionary)
                {
                    // Отримуємо сирі байти info-словника більш надійним способом
                    byte[]? rawInfoBytes = ExtractBEncodedValueBytes(allFileBytes, "info");
                    if (rawInfoBytes != null && rawInfoBytes.Length > 0)
                    {
                        torrentContent.Info.RawInfoBytes = rawInfoBytes;
                        using var sha1 = SHA1.Create();
                        torrentContent.InfoHash = sha1.ComputeHash(torrentContent.Info.RawInfoBytes);
                        System.Diagnostics.Debug.WriteLine($"InfoHash розрахований: {BitConverter.ToString(torrentContent.InfoHash).Replace("-", "")}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Помилка: не вдалося виділити сирі байти для info-словника. InfoHash не буде розрахований.");
                    }

                    // Решта парсингу infoDictionary використовує вже розпарсені дані
                    if (infoDictionary.TryGetValue("name", out var nameObj) && nameObj is byte[] nameBytes)
                        torrentContent.Info.Name = Encoding.UTF8.GetString(nameBytes);
                    
                    if (infoDictionary.TryGetValue("piece length", out var pieceLengthObj) && pieceLengthObj is long pieceLength)
                        torrentContent.Info.PieceLength = pieceLength;
                    
                    if (infoDictionary.TryGetValue("pieces", out var piecesObj) && piecesObj is byte[] pieces)
                        torrentContent.Info.Pieces = pieces;
                    
                    if (infoDictionary.TryGetValue("private", out var privateObj) && privateObj is long privateVal)
                        torrentContent.Info.Private = privateVal == 1;

                    if (infoDictionary.TryGetValue("length", out var lengthObj) && lengthObj is long singleFileLength) // Один файл
                    {
                        torrentContent.Info.Length = singleFileLength;
                    }
                    else if (infoDictionary.TryGetValue("files", out var filesObj) && filesObj is List<object> filesListRaw) // Декілька файлів
                    {
                        torrentContent.Info.Files = new List<TorrentFileInfo>();
                        foreach (var fileEntryObj in filesListRaw)
                        {
                            if (fileEntryObj is Dictionary<string, object> fileEntryDict)
                            {
                                var torrentFile = new TorrentFileInfo();
                                if (fileEntryDict.TryGetValue("length", out var fileLengthObj) && fileLengthObj is long fileLength)
                                    torrentFile.Length = fileLength;
                                
                                if (fileEntryDict.TryGetValue("path", out var pathObj) && pathObj is List<object> pathListRaw)
                                {
                                    foreach (var pathSegmentObj in pathListRaw)
                                    {
                                        if (pathSegmentObj is byte[] pathSegmentBytes)
                                            torrentFile.Path.Add(Encoding.UTF8.GetString(pathSegmentBytes));
                                    }
                                }
                                torrentContent.Info.Files.Add(torrentFile);
                            }
                        }
                    }
                }
                else
                {
                     System.Diagnostics.Debug.WriteLine("Помилка парсингу: словник 'info' не знайдено або він має неправильний тип.");
                    return null;
                }
                return torrentContent;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Критична помилка парсингу файлу {filePath}: {ex}");
                return null;
            }
        }

        // Замінюємо старий GetRawInfoDictionaryBytes на новий підхід
        private byte[]? ExtractBEncodedValueBytes(byte[] allFileBytes, string keyToFind)
        {
            var keyPattern = Encoding.ASCII.GetBytes($"{keyToFind.Length}:{keyToFind}");
            int currentPosition = 0;

            // Шукаємо ключ "info" у кореневому словнику
            // Це передбачає, що кореневий елемент - словник, що починається з 'd'
            if (allFileBytes.Length == 0 || allFileBytes[currentPosition++] != 'd') return null; // Має починатися зі словника

            while (currentPosition < allFileBytes.Length && allFileBytes[currentPosition] != 'e')
            {
                // Парсимо ключ
                int keyStartPos = currentPosition;
                AdvancePastBEncodedString(allFileBytes, ref currentPosition);
                if (currentPosition == -1) return null; // Помилка парсингу ключа
                
                int keyLength = currentPosition - keyStartPos;
                byte[] currentKeyBytes = new byte[keyLength];
                Array.Copy(allFileBytes, keyStartPos, currentKeyBytes, 0, keyLength);

                bool match = currentKeyBytes.SequenceEqual(keyPattern);

                // Парсимо значення
                int valueStartPos = currentPosition;
                AdvancePastBEncodedObject(allFileBytes, ref currentPosition);
                if (currentPosition == -1) return null; // Помилка парсингу значення
                
                if (match)
                {
                    int valueLength = currentPosition - valueStartPos;
                    byte[] valueBytes = new byte[valueLength];
                    Array.Copy(allFileBytes, valueStartPos, valueBytes, 0, valueLength);
                    return valueBytes;
                }
            }
            return null; // Ключ "info" не знайдено в кореневому словнику
        }

        // Допоміжний метод для пропуску BEncoded об'єкта та оновлення позиції
        private void AdvancePastBEncodedObject(byte[] bytes, ref int position)
        {
            if (position >= bytes.Length) { position = -1; return; }

            byte type = bytes[position];
            switch ((char)type)
            {
                case 'i': // Число
                    AdvancePastBEncodedInteger(bytes, ref position);
                    break;
                case 'l': // Список
                    AdvancePastBEncodedList(bytes, ref position);
                    break;
                case 'd': // Словник
                    AdvancePastBEncodedDictionary(bytes, ref position);
                    break;
                default: // Має бути рядок (байти)
                    if (char.IsDigit((char)type))
                    {
                        AdvancePastBEncodedString(bytes, ref position);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Невідомий тип BEncoding '{ (char)type }' на позиції {position}");
                        position = -1; // Помилка
                    }
                    break;
            }
        }

        private void AdvancePastBEncodedInteger(byte[] bytes, ref int position)
        {
            if (bytes[position++] != 'i') { position = -1; return; } // 'i'
            while (position < bytes.Length && bytes[position++] != 'e') { /* Пропускаємо цифри */ }
            if (position > bytes.Length || bytes[position-1] != 'e') { position = -1; return; } // Перевірка 'e'
        }

        private void AdvancePastBEncodedString(byte[] bytes, ref int position)
        {
            int colonPos = -1;
            for (int i = position; i < bytes.Length; i++)
            {
                if (bytes[i] == ':')
                {
                    colonPos = i;
                    break;
                }
                if (!char.IsDigit((char)bytes[i])) {position = -1; return;} // Довжина має бути числом
            }

            if (colonPos == -1) { position = -1; return; }

            string lenStr = Encoding.ASCII.GetString(bytes, position, colonPos - position);
            if (!int.TryParse(lenStr, out int length) || length < 0) { position = -1; return; }
            
            position = colonPos + 1 + length;
            if (position > bytes.Length) { position = -1; return; } // Вихід за межі масиву
        }

        private void AdvancePastBEncodedList(byte[] bytes, ref int position)
        {
            if (bytes[position++] != 'l') { position = -1; return; } // 'l'
            while (position < bytes.Length && bytes[position] != 'e')
            {
                AdvancePastBEncodedObject(bytes, ref position);
                if (position == -1) return; // Помилка у вкладеному об'єкті
            }
            if (position >= bytes.Length || bytes[position++] != 'e') { position = -1; return; } // 'e'
        }

        private void AdvancePastBEncodedDictionary(byte[] bytes, ref int position)
        {
            if (bytes[position++] != 'd') { position = -1; return; } // 'd'
            while (position < bytes.Length && bytes[position] != 'e')
            {
                AdvancePastBEncodedString(bytes, ref position); // Ключ
                if (position == -1) return;
                AdvancePastBEncodedObject(bytes, ref position); // Значення
                if (position == -1) return;
            }
            if (position >= bytes.Length || bytes[position++] != 'e') { position = -1; return; } // 'e'
        }
    }
}