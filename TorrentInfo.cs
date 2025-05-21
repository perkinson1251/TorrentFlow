using CommunityToolkit.Mvvm.ComponentModel; // Потрібно буде додати NuGet пакунок CommunityToolkit.Mvvm

namespace TorrentFlow
{
    public partial class TorrentInfo : ObservableObject
    {
        [ObservableProperty]
        private string? _name;

        [ObservableProperty]
        private string? _size; // Можна використовувати більш конкретний тип, наприклад, long, і форматувати його

        [ObservableProperty]
        private double _progress; // Від 0.0 до 100.0

        [ObservableProperty]
        private string? _status;

        [ObservableProperty]
        private string? _downloadSpeed;

        [ObservableProperty]
        private string? _uploadSpeed;

        [ObservableProperty]
        private string? _peers; // Наприклад, "10/25" (підключені/всього)
        
        public TorrentFileContent? FullTorrentContent { get; set; } 
    }
}