using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Styling;

namespace TorrentFlow
{
    public partial class AppSettings : ObservableObject 
    {
        [ObservableProperty]
        private string _downloadPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "TorrentFlowDownloads");

        [ObservableProperty]
        private ThemeVariant _selectedTheme = ThemeVariant.Default;

        [ObservableProperty]
        private long _maxDownloadSpeed = 1024 * 1024 * 5;

        [ObservableProperty]
        private long _maxUploadSpeed = 1024 * 1024 * 1;

        [ObservableProperty]
        private int _maxActiveDownloads = 3;

        [ObservableProperty]
        private int _listeningPort = 6881;
    }
}