using Microsoft.Win32;
using System.Windows;

namespace StringViewer1
{
    public partial class MainWindow : Window
    {
        private PageProvider? _provider;
        public PagedFileCollection? Pages { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Open file" };
            if (dlg.ShowDialog(this) == true)
            {
                _provider?.Dispose();
                _provider = new PageProvider(dlg.FileName, pageSize: 64 * 1024, cachePages: 128);
                Pages = new PagedFileCollection(_provider);
                PagesListBox.ItemsSource = Pages;
                FileInfoText.Text = $"{dlg.FileName} — {_provider.FileLength:N0} bytes, pages: {_provider.PageCount}";
            }
        }
    }
}