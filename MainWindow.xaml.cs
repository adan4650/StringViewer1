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
            const int bufferSize = 4096; // 4 KB buffer
            var buffer = new byte[bufferSize];
            var stringBuilder = new StringBuilder();
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        byte b = buffer[i];
                        if (b >= 32 && b <= 126)
                        {
                            stringBuilder.Append((char)b);
                        }
                        else
                        {
                            if (stringBuilder.Length >= minLength)
                                result.Add(stringBuilder.ToString());
                            stringBuilder.Clear();
                        }
                    }
                }
                if (stringBuilder.Length >= minLength)
                    result.Add(stringBuilder.ToString());
            }
            return result;
        }
    }