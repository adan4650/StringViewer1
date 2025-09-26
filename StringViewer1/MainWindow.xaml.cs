using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace StringViewer1
{
    public class FileViewModel
    {
        public string FileName { get; set; }
        public string DisplayName => Path.GetFileName(FileName);
        public PageProvider? Provider { get; set; }
        public PagedFileCollection? Pages { get; set; }
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<FileViewModel> _selectedFiles = new ObservableCollection<FileViewModel>();
        private FileViewModel? _currentFile;
        private bool _hasDisplayedContent = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            // Create a ListBox for file selection and bind it
            CreateFileSelectionListBox();
        }

        private void CreateFileSelectionListBox()
        {
            // Add a ListBox above the existing content for file selection
            var fileSelectionListBox = new ListBox
            {
                Name = "FileSelectionListBox",
                ItemsSource = _selectedFiles,
                DisplayMemberPath = "DisplayName",
                Height = 100, // Reduced height
                Margin = new Thickness(12, 50, 26, 200), // Adjusted margins to avoid button overlap
                VerticalAlignment = VerticalAlignment.Top
            };
            
            fileSelectionListBox.SelectionChanged += FileSelectionListBox_SelectionChanged;
            
            // Add to the Grid
            var grid = (Grid)((DockPanel)Content).Children[0];
            grid.Children.Add(fileSelectionListBox);
        }

        private void FileSelectionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is FileViewModel selectedFile)
            {
                _currentFile = selectedFile;
                textBox1.Text = selectedFile.FileName;
                
                if (_hasDisplayedContent)
                {
                    DisplayCurrentFile();
                }
                
                UpdateFileInfo();
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog 
            { 
                Title = "Select file to view",
                Multiselect = false
            };
            
            if (dlg.ShowDialog(this) == true)
            {
                AddFileToSelection(dlg.FileName);
                textBox1.Text = dlg.FileName;
            }
        }

        private void AddAnotherFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog 
            { 
                Title = "Add another file",
                Multiselect = true
            };
            
            if (dlg.ShowDialog(this) == true)
            {
                foreach (string fileName in dlg.FileNames)
                {
                    AddFileToSelection(fileName);
                }
                
                // Select the first file if none is selected
                if (_currentFile == null && _selectedFiles.Count > 0)
                {
                    var fileSelectionListBox = FindName("FileSelectionListBox") as ListBox;
                        if (fileSelectionListBox != null)
                        fileSelectionListBox.SelectedIndex = 0;
                }
            }
        }

        private void AddFileToSelection(string fileName)
        {
            if (!_selectedFiles.Any(f => f.FileName == fileName))
            {
                var fileViewModel = new FileViewModel { FileName = fileName };
                _selectedFiles.Add(fileViewModel);
                
                // If this is the first file, select it
                if (_selectedFiles.Count == 1)
                {
                    _currentFile = fileViewModel;
                    var fileSelectionListBox = FindName("FileSelectionListBox") as ListBox;
                    if (fileSelectionListBox != null)
                        fileSelectionListBox.SelectedItem = fileViewModel;
                }
            }
        }

        private void DisplayFile_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0)
            {
                MessageBox.Show("No files are selected.", "No Files Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Load and display the file data
            if (!_hasDisplayedContent)
            {
                _hasDisplayedContent = true;
                LoadAllFilesData();
            }
            
            if (_currentFile != null)
            {
                DisplayCurrentFile();
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            // Only save data if it's already been displayed/loaded
            if (!_hasDisplayedContent || _currentFile?.Pages == null)
            {
                MessageBox.Show("Please display the file first before saving.", "No Data Displayed", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog 
            { 
                Title = "Save file as",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };
            
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    foreach (var page in _currentFile.Pages)
                    {
                        sb.AppendLine($"=== {page.DisplayHeader} ===");
                        sb.AppendLine(page.TextPreview);
                        sb.AppendLine();
                    }
                    
                    File.WriteAllText(dlg.FileName, sb.ToString());
                    MessageBox.Show($"File saved successfully to {dlg.FileName}", "File Saved", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Save Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadAllFilesData()
        {
            foreach (var fileViewModel in _selectedFiles)
            {
                if (fileViewModel.Provider == null)
                {
                    fileViewModel.Provider = new PageProvider(fileViewModel.FileName, pageSize: 64 * 1024, cachePages: 128);
                    fileViewModel.Pages = new PagedFileCollection(fileViewModel.Provider);
                }
            }
        }

        private void DisplayCurrentFile()
        {
            if (_currentFile?.Pages != null)
            {
                PagesListBox.ItemsSource = _currentFile.Pages;
                UpdateFileInfo();
            }
            else
            {
                PagesListBox.ItemsSource = null;
                FileInfoText.Text = "";
            }
        }

        private void UpdateFileInfo()
        {
            if (_currentFile?.Provider != null)
            {
                FileInfoText.Text = $"{_currentFile.DisplayName} — {_currentFile.Provider.FileLength:N0} bytes, pages: {_currentFile.Provider.PageCount}";
            }
            else
            {
                FileInfoText.Text = "";
            }
        }

        private void UpdateSelectedFilesDisplay()
        {
            if (_selectedFiles.Count == 0)
            {
                textBlock1.Text = "Selected files will appear here...";
            }
            else
            {
                var fileList = string.Join("\n", _selectedFiles.Select(f => f.DisplayName));
                textBlock1.Text = $"Selected files ({_selectedFiles.Count}):\n{fileList}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var fileViewModel in _selectedFiles)
            {
                fileViewModel.Provider?.Dispose();
            }
            base.OnClosed(e);
        }
    }
}