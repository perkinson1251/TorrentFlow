using Avalonia.Controls;

namespace TorrentFlow
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        public SettingsWindow(SettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}