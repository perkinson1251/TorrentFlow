using MonoTorrent;
using MonoTorrent.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        }

        private async Task InitializeEngine(int maxDownloadSpeedKBps)
        {
            var settingsBuilder = new EngineSettingsBuilder
            {
                MaximumDownloadRate = maxDownloadSpeedKBps * 1024, 
                CacheDirectory = _fastResumeDirectory
            };
            
            var newEngineSettings = settingsBuilder.ToSettings();

            if (engine == null)
            {
                engine = new ClientEngine(newEngineSettings);
            }
            else
            {
                await engine.UpdateSettingsAsync(newEngineSettings);
            }
        }

        public async Task SetSpeed(int kbSpeedLimit)
        {
            if (kbSpeedLimit < 0) kbSpeedLimit = 0; // Ensure non-negative

            if (engine == null || kbSpeedLimit != currentDownloadSpeedLimit / 1024) // currentDownloadSpeedLimit was in B/s
            {
                currentDownloadSpeedLimit = kbSpeedLimit * 1024; // Store as B/s
                await InitializeEngine(kbSpeedLimit);
            }
        }

        public async Task<Byte[]> LoadMagneticLinkMetadata(string link)
        {
            var magnetLink = MagnetLink.Parse(link);
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
            // TorrentSettings are per-torrent, not engine-wide speed limits.
            TorrentSettings torrentSettings =  new TorrentSettingsBuilder().ToSettings(); 
            TorrentManager manager;

            if (File.Exists(fastResumePath))
            {
                // It's generally recommended to load from fast resume data rather than a .torrent file if available,
                // but here we are adding a .torrent and then applying fast resume.
                manager = await engine.AddAsync(torrent, savePath, torrentSettings);
                try
                {
                    using (var stream = File.OpenRead(fastResumePath))
                    {
                        if (FastResume.TryLoad(stream, out var output))
                        {
                            await manager.LoadFastResumeAsync(output);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading fast resume for {torrent.Name}: {ex.Message}");
                    // Continue without fast resume if loading failed
                }
            }
            else
            {
                manager = await engine.AddAsync(torrent, savePath, torrentSettings);
            }

            activeTorrents[torrent.Name] = manager;

            if (startOnAdd) // HashCheckAsync now takes a bool for whether to start after completion
            {
                await manager.HashCheckAsync(true);
                if (manager.State != TorrentState.Hashing) // If not hashing (i.e. hash was fine or not needed)
                {
                   await manager.StartAsync(); // Start it if instructed
                }
            }
            else
            {
                 await manager.HashCheckAsync(false); // Just hash, don't auto-start
            }
            return manager;
        }

        public async Task ResumeTorrentAsync(string torrentName)
        {
            if (activeTorrents.TryGetValue(torrentName, out var manager))
            {
                if (manager.State != TorrentState.Downloading && manager.State != TorrentState.Seeding)
                {
                    await manager.StartAsync();
                }
            }
        }

        public async Task PauseTorrentAsync(string torrentName)
        {
            if (activeTorrents.TryGetValue(torrentName, out var manager))
            {
                if (manager.State == TorrentState.Downloading || manager.State == TorrentState.Seeding)
                {
                    await manager.PauseAsync();
                }
            }
        }

        public async Task DeleteTorrentAsync(string torrentName, bool deleteFiles = false)
        {
            if (activeTorrents.TryGetValue(torrentName, out var manager))
            {
                string fastResumePath = GetFastResumeFilePath(torrentName);
                if(File.Exists(fastResumePath))
                {
                    try
                    {
                        File.Delete(fastResumePath);
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"Could not delete fast resume file {fastResumePath}: {ex.Message}");
                    }
                }

                activeTorrents.Remove(torrentName);
                
                if(manager.State == TorrentState.Stopping)
                    return;
                    
                await manager.StopAsync(); // Ensure torrent is stopped before removing
                
                // Optionally delete downloaded files
                if(deleteFiles)
                {
                    try
                    {
                        // This requires knowing the save path and potentially multi-file structure
                        // MonoTorrent.TorrentManager.SavePath
                        string savePath = manager.SavePath;
                        if (manager.Torrent != null && manager.Torrent.Files != null && manager.Torrent.Files.Count > 1)
                        {
                            // For multi-file torrents, the save path is a directory containing the files/folders.
                            // The torrent name usually forms the root folder within this save path.
                             string torrentRootFolder = Path.Combine(savePath, manager.Torrent.Name);
                             if(Directory.Exists(torrentRootFolder))
                             {
                                 Directory.Delete(torrentRootFolder, true);
                             }
                             else if (Directory.Exists(savePath) && manager.Torrent.Files.All(f => Path.GetDirectoryName(Path.Combine(savePath, f.Path)) == savePath))
                             {
                                 // Or if all files are directly in savePath (less common for multi-file)
                                 foreach(var file in manager.Torrent.Files)
                                 {
                                     File.Delete(Path.Combine(savePath, file.Path));
                                 }
                             }
                        }
                        else if (manager.Torrent != null && manager.Torrent.Files != null && manager.Torrent.Files.Count == 1)
                        {
                            // Single file torrent
                            File.Delete(Path.Combine(savePath, manager.Torrent.Files[0].Path));
                        }
                        else if (Directory.Exists(Path.Combine(savePath, manager.Torrent.Name))) // Fallback for directory
                        {
                             Directory.Delete(Path.Combine(savePath, manager.Torrent.Name), true);
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting torrent files for {torrentName}: {ex.Message}");
                    }
                }
                await engine.RemoveAsync(manager); // Remove from engine
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
                if (manager.State != TorrentState.Stopped && manager.State != TorrentState.Error) // Only save if it makes sense
                {
                    try
                    {
                        var fastResume = await manager.SaveFastResumeAsync();
                        await File.WriteAllBytesAsync(GetFastResumeFilePath(kvp.Key), fastResume.Encode());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving fast resume for {kvp.Key}: {ex.Message}");
                    }
                }
            }
        }

        private string GetFastResumeFilePath(string torrentName) // torrentName is manager.Torrent.Name
        {
            // Sanitize torrentName to be a valid file name
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            string sanitizedTorrentName = torrentName;
            foreach (char c in invalidChars)
            {
                sanitizedTorrentName = sanitizedTorrentName.Replace(c.ToString(), "_");
            }
            return Path.Combine(_fastResumeDirectory, sanitizedTorrentName + ".resume");
        }
    }
}