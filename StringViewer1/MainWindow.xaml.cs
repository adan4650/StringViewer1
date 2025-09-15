using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace StringViewer1
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                var strings = ExtractPrintableStrings(dlg.FileName);
                StringListBox.ItemsSource = strings;
            }
        }

        // Extract contiguous printable ASCII strings (length >= 4)
        private List<string> ExtractPrintableStrings(string filePath)
        {
            var result = new List<string>();
            const int minLength = 4;
            byte[] bytes = File.ReadAllBytes(filePath);
            int start = -1;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= 32 && b <= 126)
                {
                    if (start == -1) start = i;
                }
                else
                {
                    if (start != -1 && i - start >= minLength)
                        result.Add(Encoding.ASCII.GetString(bytes, start, i - start));
                    start = -1;
                }
            }
            if (start != -1 && bytes.Length - start >= minLength)
                result.Add(Encoding.ASCII.GetString(bytes, start, bytes.Length - start));
            return result;
        }
    }
}