using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TorrentFlow.Services;

namespace TorrentFlow
{
    public partial class AddTorrentDialog : Window
    {
        private readonly SettingsService _settingsService;
        public string TorrentName { get; private set; }
        public string SelectedDirectory { get; private set; }

        public AddTorrentDialog(string torrentName)
        {
            InitializeComponent();
            DataContext = new AddTorrentDialogViewModel();
            _settingsService = App.GetService<SettingsService>();
            SelectedDirectory = _settingsService.GetSettings().DefaultSaveLocation;
            DirectoryTextBox.Text = SelectedDirectory;
            TorrentNameTextBox.Text = torrentName;
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Save Directory"
            };
            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrWhiteSpace(result))
            {
                SelectedDirectory = result;
                DirectoryTextBox.Text = result;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = (AddTorrentDialogViewModel)DataContext;
            string saveDirectory = viewModel.SelectedDirectory;

            Directory.CreateDirectory(saveDirectory);

            Close(saveDirectory);
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}