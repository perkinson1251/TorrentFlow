using System.Collections.Generic;

namespace TorrentFlow.Data
{
    public class Settings
    {
        public string DefaultSaveLocation { get; set; }
        public List<SpeedProfileEntry> SpeedProfiles { get; set; } = new List<SpeedProfileEntry>();
    }
}