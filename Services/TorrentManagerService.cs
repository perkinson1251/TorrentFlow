using MonoTorrent;
using MonoTorrent.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TorrentFlow.Services
{
    public class TorrentManagerService
    {
        private ClientEngine engine;
        public Dictionary<string, TorrentManager> activeTorrents;
        private string _fastResumeDirectory = AppDomain.CurrentDomain.BaseDirectory + "cache";
        private int currentDownloadSpeedLimit = 0;

        public TorrentManagerService()
        {
            activeTorrents = new Dictionary<string, TorrentManager>();
            Directory.CreateDirectory(_fastResumeDirectory);
            InitializeEngine(0);
        }

        private async Task InitializeEngine(int maxDownloadSpeed)
        {
            var settings = new EngineSettingsBuilder
            {
                MaximumDownloadRate = maxDownloadSpeed * 1024,
                CacheDirectory = _fastResumeDirectory
            }.ToSettings();
            engine = new ClientEngine(settings);

            foreach (var torrent in activeTorrents)
            {
                await torrent.Value.Engine.UpdateSettingsAsync(settings);
            }
        }

        public async Task SetSpeed(int kbSpeedLimit)
        {
            if (kbSpeedLimit == currentDownloadSpeedLimit)
                return;
            
            currentDownloadSpeedLimit = kbSpeedLimit;
            InitializeEngine(kbSpeedLimit);
        }

        public async Task<Byte[]> LoadMagneticLinkMetadata(string link)
        {
            var magnetLink = MagnetLink.Parse(link);
            var infoHash = magnetLink.InfoHashes.V1;
            TorrentManager manager;
            ReadOnlyMemory<byte> metadataMemory = await engine.DownloadMetadataAsync(magnetLink, CancellationToken.None);

            if (metadataMemory.IsEmpty)
            {
                throw new Exception("Unable to retrieve torrent metadata from the magnet link.");
            }
            return metadataMemory.ToArray();
        }

        public async Task<TorrentManager> StartTorrentAsync(Torrent torrent, string savePath, bool startOnAdd)
        {
            if (activeTorrents.ContainsKey(torrent.Name))
            {
                return activeTorrents[torrent.Name];
            }

            var fastResumePath = GetFastResumeFilePath(torrent.Name);
            TorrentSettings settings =  new TorrentSettings();
            TorrentManager manager;

            if (File.Exists(fastResumePath))
            {
                manager = await engine.AddAsync(torrent, savePath, settings);
                using (var stream = File.OpenRead(fastResumePath))
                {
                    if (FastResume.TryLoad(stream, out var output))
                    {
                        await manager.LoadFastResumeAsync(output);
                    }
                }
            }
            else
            {
                manager = await engine.AddAsync(torrent, savePath, settings);
            }

            activeTorrents[torrent.Name] = manager;

            await manager.HashCheckAsync(startOnAdd);
            return manager;
        }

        public async Task ResumeTorrentAsync(string torrentName)
        {
            if (activeTorrents.ContainsKey(torrentName))
            {
                var manager = activeTorrents[torrentName];
                if (manager.State != TorrentState.Downloading)
                {
                    await manager.StartAsync();
                }
            }
        }

        public async Task PauseTorrentAsync(string torrentName)
        {
            if (activeTorrents.ContainsKey(torrentName))
            {
                var manager = activeTorrents[torrentName];
                if (manager.State == TorrentState.Downloading || manager.State == TorrentState.Seeding)
                {
                    await manager.PauseAsync();
                }
            }
        }

        public async Task DeleteTorrentAsync(string torrentName, bool deleteFiles = false)
        {
            if (activeTorrents.ContainsKey(torrentName))
            {
                //clear fast resume
                if(File.Exists(GetFastResumeFilePath(torrentName)))
                {
                    File.Delete(GetFastResumeFilePath(torrentName));
                }

                var manager = activeTorrents[torrentName];
                activeTorrents.Remove(torrentName);
                
                if(manager.State == TorrentState.Stopping)
                    return;
                    
                await manager.StopAsync();
                await engine.RemoveAsync(manager);
            }
        }

        public float GetProgress(string torrentName)
        {
            return (float)(activeTorrents.ContainsKey(torrentName) ? activeTorrents[torrentName].Progress : 0f);
        }

        public async Task SaveAllTorrentsStateAsync()
        {
            foreach (var kvp in activeTorrents)
            {
                var manager = kvp.Value;
                var fastResume = await manager.SaveFastResumeAsync();
                File.WriteAllBytes(GetFastResumeFilePath(kvp.Key), fastResume.Encode());
            }
        }

        private string GetFastResumeFilePath(string torrentFile)
        {
            return Path.Combine(_fastResumeDirectory, Path.GetFileNameWithoutExtension(torrentFile) + ".resume");
        }
    }
}