using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TorrentFlow.Data
{
    [Serializable]
    public class SessionData
    {
        [JsonInclude]
        public List<TorrentSessionItem> Torrents { get; set; } = new List<TorrentSessionItem>();
    }
}