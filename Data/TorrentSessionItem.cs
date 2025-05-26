using System;
using MonoTorrent.Client;

namespace TorrentFlow.Data
{
    [Serializable]
    public class TorrentSessionItem
    {
        public string TorrentFile { get; set; }
        public string SavePath { get; set; }
        public TorrentState State { get; set; }
    }
}