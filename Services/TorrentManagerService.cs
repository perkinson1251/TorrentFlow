using MonoTorrent;
using MonoTorrent.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TorrentFlow.Services;

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
            engine = new ClientEngine(newEngineSettings);
        else
            await engine.UpdateSettingsAsync(newEngineSettings);
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

    public async Task<byte[]> LoadMagneticLinkMetadata(string link)
    {
        var magnetLink = MagnetLink.Parse(link);
        var metadataMemory = await engine.DownloadMetadataAsync(magnetLink, CancellationToken.None);

        if (metadataMemory.IsEmpty) throw new Exception("Unable to retrieve torrent metadata from the magnet link.");
        return metadataMemory.ToArray();
    }

    public async Task<TorrentManager> StartTorrentAsync(Torrent torrent, string savePath, bool startOnAdd)
    {
        if (activeTorrents.ContainsKey(torrent.Name)) return activeTorrents[torrent.Name];

        var existingManager = engine.Torrents.FirstOrDefault(t => t.Torrent?.Name == torrent.Name);
        if (existingManager != null)
        {
            activeTorrents[torrent.Name] = existingManager;
            return existingManager;
        }

        var fastResumePath = GetFastResumeFilePath(torrent.Name);
        var torrentSettings = new TorrentSettingsBuilder().ToSettings();
        TorrentManager manager;

        if (File.Exists(fastResumePath))
        {
            manager = await engine.AddAsync(torrent, savePath, torrentSettings);
            try
            {
                using (var stream = File.OpenRead(fastResumePath))
                {
                    if (FastResume.TryLoad(stream, out var output)) await manager.LoadFastResumeAsync(output);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading fast resume for {torrent.Name}: {ex.Message}");
            }
        }
        else
        {
            manager = await engine.AddAsync(torrent, savePath, torrentSettings);
        }

        activeTorrents[torrent.Name] = manager;

        if (startOnAdd)
        {
            await manager.HashCheckAsync(true);
            if (manager.State != TorrentState.Hashing) await manager.StartAsync();
        }
        else
        {
            await manager.HashCheckAsync(false);
        }

        return manager;
    }

    public async Task ResumeTorrentAsync(string torrentName)
    {
        if (activeTorrents.TryGetValue(torrentName, out var manager))
            if (manager.State != TorrentState.Downloading && manager.State != TorrentState.Seeding)
                await manager.StartAsync();
    }

    public async Task PauseTorrentAsync(string torrentName)
    {
        if (activeTorrents.TryGetValue(torrentName, out var manager))
            if (manager.State == TorrentState.Downloading || manager.State == TorrentState.Seeding)
                await manager.PauseAsync();
    }

    public async Task DeleteTorrentAsync(string torrentName, bool deleteFiles = false)
    {
        if (activeTorrents.TryGetValue(torrentName, out var manager))
        {
            var fastResumePath = GetFastResumeFilePath(torrentName);
            if (File.Exists(fastResumePath))
                try
                {
                    File.Delete(fastResumePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not delete fast resume file {fastResumePath}: {ex.Message}");
                }

            if (manager.State != TorrentState.Stopping && manager.State != TorrentState.Stopped)
                await manager.StopAsync(); // Ensure torrent is stopped before removing

            await engine.RemoveAsync(manager); // Remove from engine FIRST

            activeTorrents.Remove(torrentName);

            // Optionally delete downloaded files
            if (deleteFiles)
                try
                {
                    // This requires knowing the save path and potentially multi-file structure
                    // MonoTorrent.TorrentManager.SavePath
                    var savePath = manager.SavePath;
                    if (manager.Torrent != null && manager.Torrent.Files != null && manager.Torrent.Files.Count > 1)
                    {
                        // For multi-file torrents, the save path is a directory containing the files/folders.
                        // The torrent name usually forms the root folder within this save path.
                        var torrentRootFolder = Path.Combine(savePath, manager.Torrent.Name);
                        if (Directory.Exists(torrentRootFolder))
                            Directory.Delete(torrentRootFolder, true);
                        else if (Directory.Exists(savePath) && manager.Torrent.Files.All(f =>
                                     Path.GetDirectoryName(Path.Combine(savePath, f.Path)) == savePath))
                            // Or if all files are directly in savePath (less common for multi-file)
                            foreach (var file in manager.Torrent.Files)
                                File.Delete(Path.Combine(savePath, file.Path));
                    }
                    else if (manager.Torrent != null && manager.Torrent.Files != null &&
                             manager.Torrent.Files.Count == 1)
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
    }

    public float GetProgress(string torrentName)
    {
        return (float)(activeTorrents.ContainsKey(torrentName) ? activeTorrents[torrentName].Progress : 0f);
    }

    public Dictionary<string, TorrentManager> GetAllTorrents()
    {
        return activeTorrents;
    }

    public async Task SaveAllTorrentsStateAsync()
    {
        foreach (var kvp in activeTorrents)
        {
            var manager = kvp.Value;
            if (manager.State != TorrentState.Stopped &&
                manager.State != TorrentState.Error &&
                manager.State != TorrentState.Stopping &&
                manager.State != TorrentState.Hashing &&
                manager.Progress > 0.0)
                try
                {
                    var fastResume = await manager.SaveFastResumeAsync();
                    await File.WriteAllBytesAsync(GetFastResumeFilePath(kvp.Key), fastResume.Encode());
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("bitfield") && !ex.Message.Contains("unhashed"))
                        Console.WriteLine($"Error saving fast resume for {kvp.Key}: {ex.Message}");
                }
        }
    }

    private string GetFastResumeFilePath(string torrentName) // torrentName is manager.Torrent.Name
    {
        // Sanitize torrentName to be a valid file name
        var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var sanitizedTorrentName = torrentName;
        foreach (var c in invalidChars) sanitizedTorrentName = sanitizedTorrentName.Replace(c.ToString(), "_");
        return Path.Combine(_fastResumeDirectory, sanitizedTorrentName + ".resume");
    }
}