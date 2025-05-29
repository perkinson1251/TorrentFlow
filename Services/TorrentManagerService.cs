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
            CacheDirectory = _fastResumeDirectory,
            AllowPortForwarding = true,
            AllowLocalPeerDiscovery = true
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
        if (engine == null) await InitializeEngine(currentDownloadSpeedLimit / 1024);
        var magnetLink = MagnetLink.Parse(link);
        var metadataMemory = await engine.DownloadMetadataAsync(magnetLink, CancellationToken.None);

        if (metadataMemory.IsEmpty) throw new Exception("Unable to retrieve torrent metadata from the magnet link.");
        return metadataMemory.ToArray();
    }

    public async Task<TorrentManager> StartTorrentAsync(Torrent torrent, string savePath, bool startOnAdd)
    {
        if (engine == null) await InitializeEngine(currentDownloadSpeedLimit / 1024);

        if (activeTorrents.TryGetValue(torrent.Name, out var managerFromDict))
        {
            if (startOnAdd && managerFromDict.State != TorrentState.Downloading &&
                managerFromDict.State != TorrentState.Seeding && managerFromDict.State != TorrentState.Hashing)
            {
                Console.WriteLine($"Torrent '{torrent.Name}' found in dictionary, ensuring it is started.");
                await managerFromDict.StartAsync();
            }

            return managerFromDict;
        }

        var managerFromEngine =
            engine.Torrents.FirstOrDefault(t =>
                t.Torrent?.Name == torrent.Name);
        if (managerFromEngine != null)
        {
            Console.WriteLine($"Torrent '{torrent.Name}' found in engine, adding to activeTorrents dictionary.");
            activeTorrents[torrent.Name] = managerFromEngine;
            if (startOnAdd && managerFromEngine.State != TorrentState.Downloading &&
                managerFromEngine.State != TorrentState.Seeding && managerFromEngine.State != TorrentState.Hashing)
            {
                Console.WriteLine($"Ensuring torrent '{torrent.Name}' from engine is started.");
                await managerFromEngine.StartAsync();
            }

            return managerFromEngine;
        }

        Console.WriteLine($"Adding new torrent '{torrent.Name}' to engine.");
        var fastResumePath = GetFastResumeFilePath(torrent.Name);
        var torrentSettings = new TorrentSettingsBuilder
        {
            CreateContainingDirectory = true
        }.ToSettings();

        TorrentManager manager;

        manager = await engine.AddAsync(torrent, savePath, torrentSettings);
        Console.WriteLine($"Torrent '{torrent.Name}' added to engine. State: {manager.State}");

        if (File.Exists(fastResumePath))
        {
            Console.WriteLine($"Attempting to load fast resume for '{torrent.Name}'.");
            try
            {
                using (var stream = File.OpenRead(fastResumePath))
                {
                    if (FastResume.TryLoad(stream, out var output))
                    {
                        await manager.LoadFastResumeAsync(output);
                        Console.WriteLine(
                            $"Fast resume loaded for '{torrent.Name}'. State after fast resume: {manager.State}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to parse fast resume data for '{torrent.Name}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error loading fast resume for {torrent.Name}: {ex.Message}. Proceeding without fast resume.");
            }
        }

        activeTorrents[torrent.Name] = manager;

        if (startOnAdd)
        {
            Console.WriteLine(
                $"Starting torrent '{torrent.Name}' as per startOnAdd=true. Current state before start: {manager.State}");
            await manager.StartAsync();
            Console.WriteLine($"Torrent '{torrent.Name}' start initiated. State after StartAsync: {manager.State}");
        }
        else
        {
            Console.WriteLine(
                $"Hashing torrent '{torrent.Name}' without starting as per startOnAdd=false. Current state: {manager.State}");
            await manager.HashCheckAsync(false);
            Console.WriteLine(
                $"Torrent '{torrent.Name}' hash check initiated. State after HashCheckAsync(false): {manager.State}");
        }

        return manager;
    }

    public async Task ResumeTorrentAsync(string torrentName)
    {
        if (activeTorrents.TryGetValue(torrentName, out var manager))
            if (manager.State != TorrentState.Downloading && manager.State != TorrentState.Seeding)
            {
                Console.WriteLine($"Resuming torrent '{torrentName}'. Current state: {manager.State}");
                await manager.StartAsync();
                Console.WriteLine($"Torrent '{torrentName}' resume initiated. State after StartAsync: {manager.State}");
            }
    }

    public async Task PauseTorrentAsync(string torrentName)
    {
        if (activeTorrents.TryGetValue(torrentName, out var manager))
            if (manager.State == TorrentState.Downloading || manager.State == TorrentState.Seeding)
            {
                Console.WriteLine($"Pausing torrent '{torrentName}'. Current state: {manager.State}");
                await manager.PauseAsync();
                Console.WriteLine($"Torrent '{torrentName}' pause initiated. State after PauseAsync: {manager.State}");
            }
    }

    public async Task DeleteTorrentAsync(string torrentName, bool deleteFiles = false)
    {
        TorrentManager manager; 

        if (activeTorrents.TryGetValue(torrentName, out manager))
        {
            Console.WriteLine($"Deleting torrent '{torrentName}'. Delete files: {deleteFiles}");

            activeTorrents.Remove(torrentName);
            Console.WriteLine($"Torrent '{torrentName}' preemptively removed from activeTorrents dictionary.");

            var fastResumePath =
                GetFastResumeFilePath(torrentName);
            if (File.Exists(fastResumePath))
                try
                {
                    File.Delete(fastResumePath);
                    Console.WriteLine($"Fast resume file deleted for '{torrentName}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not delete fast resume file {fastResumePath}: {ex.Message}");
                }

            if (manager.State != TorrentState.Stopping && manager.State != TorrentState.Stopped)
            {
                Console.WriteLine(
                    $"Stopping torrent '{torrentName}' before removal from engine. Current state: {manager.State}");
                try
                {
                    await manager.StopAsync();
                    Console.WriteLine($"Torrent '{torrentName}' stopped. State after StopAsync: {manager.State}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping torrent '{torrentName}': {ex.Message}");
                }
            }

            if (engine != null && engine.Torrents.Contains(manager))
            {
                Console.WriteLine($"Removing torrent '{torrentName}' from engine.");
                try
                {
                    var mode = deleteFiles ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly;
                    await engine.RemoveAsync(manager, mode);
                    Console.WriteLine($"Torrent '{torrentName}' removed from engine with mode {mode}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error removing torrent '{torrentName}' from engine: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine(
                    $"Torrent manager instance '{torrentName}' was not found in the engine's list, or engine is null. Skipping engine.RemoveAsync.");
            }
        }
        else
        {
            Console.WriteLine($"Torrent '{torrentName}' not found in activeTorrents dictionary for deletion.");
        }
    }

    public async Task SaveAllTorrentsStateAsync()
    {
        Console.WriteLine($"Saving state for {activeTorrents.Count} torrents.");
        foreach (var kvp in activeTorrents.ToList())
        {
            var manager = kvp.Value;
            if (manager.State != TorrentState.Stopped &&
                manager.State != TorrentState.Error &&
                manager.State != TorrentState.Stopping)
                try
                {
                    if (manager.Torrent != null && manager.Torrent.InfoHashes != null)
                    {
                        var fastResume = await manager.SaveFastResumeAsync();
                        var filePath = GetFastResumeFilePath(kvp.Key);
                        await File.WriteAllBytesAsync(filePath, fastResume.Encode());
                        Console.WriteLine(
                            $"Fast resume saved for {kvp.Key}. State: {manager.State}, Progress: {manager.Progress}");
                    }
                }
                catch (InvalidOperationException ioe) when (ioe.Message.Contains(
                                                                "The torrent is currently in the metadata download stage") ||
                                                            ioe.Message.Contains("The torrent has not been hashed yet"))
                {
                    Console.WriteLine($"Skipping fast resume for {kvp.Key} (metadata/unhashed): {ioe.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving fast resume for {kvp.Key}: {ex.Message}");
                }
        }
    }

    private string GetFastResumeFilePath(string torrentName)
    {
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
        var sanitizedTorrentName = new string(torrentName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        const int maxFileNameLength = 255;
        if (sanitizedTorrentName.Length > maxFileNameLength)
            sanitizedTorrentName = sanitizedTorrentName.Substring(0, maxFileNameLength);
        return Path.Combine(_fastResumeDirectory, sanitizedTorrentName + ".resume");
    }
}