using Avalonia.Controls;

namespace TorrentFlow
{
    public partial class LoadingDialog : Window
    {
        public LoadingDialog(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }
    }
}