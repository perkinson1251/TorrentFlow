using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net; // Для IPAddress, IPEndPoint
using System.Net.Http;
using System.Text; // Для Encoding
using System.Threading.Tasks;
using System.Web; // Для HttpUtility (потрібно буде перевірити доступність або знайти альтернативу для .NET Core/5+)
                   // Якщо HttpUtility недоступний, використаємо Uri.EscapeDataString для параметрів

namespace TorrentFlow
{
    public class TrackerClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<TrackerResponse?> AnnounceAsync(
            TorrentFileContent torrentContent,
            string peerId,
            ushort listeningPort,
            long uploaded,
            long downloaded,
            string? eventType = null) // eventType може бути "started", "stopped", "completed"
        {
            if (torrentContent.InfoHash == null || torrentContent.InfoHash.Length != 20)
            {
                System.Diagnostics.Debug.WriteLine("TrackerClient: Некоректний InfoHash.");
                return new TrackerResponse { FailureReason = "Некоректний InfoHash для запиту до трекера." };
            }

            string? trackerUrl = torrentContent.Announce; // Поки що беремо тільки головний трекер
            if (string.IsNullOrEmpty(trackerUrl))
            {
                // Можна спробувати взяти з announce-list, якщо Announce порожній
                if (torrentContent.AnnounceList != null && torrentContent.AnnounceList.Any())
                {
                    var firstTier = torrentContent.AnnounceList.FirstOrDefault();
                    if (firstTier != null && firstTier.Any())
                    {
                        trackerUrl = firstTier.FirstOrDefault();
                    }
                }
            }
            
            if (string.IsNullOrEmpty(trackerUrl))
            {
                 System.Diagnostics.Debug.WriteLine("TrackerClient: URL трекера не вказано.");
                return new TrackerResponse { FailureReason = "URL трекера не вказано в торент-файлі." };
            }

            long left = torrentContent.TotalSize - downloaded;
            if (left < 0) left = 0; // На випадок, якщо downloaded > TotalSize через помилку

            // Формування URL-закодованого info_hash
            // Кожен байт info_hash має бути URL-закодований.
            var infoHashUrlEncoded = new StringBuilder();
            foreach (byte b in torrentContent.InfoHash)
            {
                infoHashUrlEncoded.Append('%');
                infoHashUrlEncoded.AppendFormat("{0:X2}", b);
            }
            
            // Peer ID також має бути URL-закодований, якщо містить спецсимволи.
            // Стандартний peer_id зазвичай генерується з ASCII символів, безпечних для URL,
            // але для надійності краще кодувати.
            string peerIdUrlEncoded = Uri.EscapeDataString(peerId);


            var queryParams = new Dictionary<string, string?>
            {
                { "info_hash", infoHashUrlEncoded.ToString() },
                { "peer_id", peerIdUrlEncoded },
                { "port", listeningPort.ToString() },
                { "uploaded", uploaded.ToString() },
                { "downloaded", downloaded.ToString() },
                { "left", left.ToString() },
                { "compact", "1" }, // Завжди запитуємо компактну відповідь
                { "numwant", "50" } // Бажана кількість пірів (типове значення)
            };

            if (!string.IsNullOrEmpty(eventType))
            {
                queryParams["event"] = eventType;
            }

            var uriBuilder = new UriBuilder(trackerUrl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query); // Використовуємо HttpUtility для зручності
                                                                      // Якщо HttpUtility.ParseQueryString недоступний (наприклад, у .NET Standard без дод. пакунків),
                                                                      // можна формувати рядок запиту вручну або через інший механізм.
            foreach(var param in queryParams)
            {
                if (param.Value != null) query[param.Key] = param.Value;
            }
            uriBuilder.Query = query.ToString();
            
            string fullRequestUrl = uriBuilder.ToString();
            System.Diagnostics.Debug.WriteLine($"Tracker Announce URL: {fullRequestUrl}");

            try
            {
                var responseBytes = await _httpClient.GetByteArrayAsync(fullRequestUrl);
                var decodedResponse = BEncoding.Decode(responseBytes);

                if (decodedResponse is Dictionary<string, object> responseDict)
                {
                    return ParseTrackerResponse(responseDict);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("TrackerClient: Відповідь від трекера не є словником.");
                    return new TrackerResponse { FailureReason = "Не вдалося розпарсити відповідь від трекера (не словник)." };
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"TrackerClient: Помилка HTTP-запиту до трекера: {ex.Message}");
                return new TrackerResponse { FailureReason = $"Помилка HTTP: {ex.Message}" };
            }
            catch (Exception ex) // Інші помилки (включаючи BEncoding помилки)
            {
                System.Diagnostics.Debug.WriteLine($"TrackerClient: Загальна помилка при запиті до трекера: {ex.Message}");
                return new TrackerResponse { FailureReason = $"Помилка: {ex.Message}" };
            }
        }

        private TrackerResponse ParseTrackerResponse(Dictionary<string, object> responseDict)
        {
            var trackerResponse = new TrackerResponse();

            if (responseDict.TryGetValue("failure reason", out var failureObj) && failureObj is byte[] failureBytes)
            {
                trackerResponse.FailureReason = Encoding.UTF8.GetString(failureBytes);
                System.Diagnostics.Debug.WriteLine($"Tracker error: {trackerResponse.FailureReason}");
                return trackerResponse; // Якщо є помилка, інші поля можуть бути відсутні
            }

            if (responseDict.TryGetValue("warning message", out var warningObj) && warningObj is byte[] warningBytes)
                trackerResponse.WarningMessage = Encoding.UTF8.GetString(warningBytes);
            
            if (responseDict.TryGetValue("interval", out var intervalObj) && intervalObj is long interval)
                trackerResponse.Interval = (int)interval;
            
            if (responseDict.TryGetValue("min interval", out var minIntervalObj) && minIntervalObj is long minInterval)
                trackerResponse.MinInterval = (int)minInterval;

            if (responseDict.TryGetValue("tracker id", out var trackerIdObj) && trackerIdObj is byte[] trackerIdBytes)
                trackerResponse.TrackerId = Encoding.UTF8.GetString(trackerIdBytes);

            if (responseDict.TryGetValue("complete", out var completeObj) && completeObj is long complete)
                trackerResponse.Complete = (int)complete;

            if (responseDict.TryGetValue("incomplete", out var incompleteObj) && incompleteObj is long incomplete)
                trackerResponse.Incomplete = (int)incomplete;

            if (responseDict.TryGetValue("peers", out var peersObj))
            {
                if (peersObj is byte[] peersCompact) // Компактний формат
                {
                    if (peersCompact.Length % 6 != 0)
                    {
                        System.Diagnostics.Debug.WriteLine("TrackerClient: Некоректна довжина компактного списку пірів.");
                        trackerResponse.FailureReason = "Некоректний список пірів.";
                        return trackerResponse;
                    }

                    for (int i = 0; i < peersCompact.Length; i += 6)
                    {
                        byte[] ipBytes = new byte[4];
                        Array.Copy(peersCompact, i, ipBytes, 0, 4);
                        // Порт у мережевому порядку (big-endian)
                        ushort port = (ushort)((peersCompact[i + 4] << 8) | peersCompact[i + 5]);
                        trackerResponse.Peers.Add(new PeerInfo(new IPAddress(ipBytes), port));
                    }
                }
                else if (peersObj is List<object> peersList) // Некомпактний формат (словники)
                {
                     System.Diagnostics.Debug.WriteLine("TrackerClient: Отримано некомпактний список пірів. Парсинг не реалізовано повністю.");
                    // Тут потрібно буде розпарсити список словників, кожен з яких містить "peer id", "ip", "port"
                    // Приклад з Seán O'Flynn.htm показує тільки компактний варіант відповіді.
                    // Наразі, більшість трекерів повертають компактний варіант, якщо його запрошено.
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Tracker response: Peers: {trackerResponse.Peers.Count}, Interval: {trackerResponse.Interval}, Seeders: {trackerResponse.Complete}, Leechers: {trackerResponse.Incomplete}");
            return trackerResponse;
        }
    }
}