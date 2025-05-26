
namespace TorrentFlow.Data
{
    public class Settings
    {
        public string DefaultSaveLocation { get; set; }
        public int MaxDownloadSpeedKBps { get; set; }

        public Settings()
        {
            DefaultSaveLocation = string.Empty;
            MaxDownloadSpeedKBps = 0;
        }
    }
}