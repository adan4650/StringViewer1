#nullable enable

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace StringViewer1
{
    public class FileViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public string DisplayName => Path.GetFileName(FileName);
        public PageProvider? Provider { get; set; }
        public PagedFileCollection? Pages { get; set; }
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<FileViewModel> _selectedFiles = new ObservableCollection<FileViewModel>();
        private FileViewModel? _currentFile;
        private bool _isLoading = false;

        // Change these fields to be private readonly to avoid ambiguity and ensure only one instance exists
        private readonly TextBlock LoadingStatusText = new TextBlock { Visibility = Visibility.Collapsed };
        
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = this;

                // Add controls to the grid if not present in XAML
                if (Content is DockPanel dockPanel && dockPanel.Children.Count > 0 && dockPanel.Children[0] is Grid grid)
                {
                    grid.Children.Add(LoadingStatusText);
                }

                // Initialize the file selection ListBox that's now in XAML
                InitializeFileSelectionListBox();
            }
            catch (System.Windows.Markup.XamlParseException xamlEx)
            {
                MessageBox.Show($"XAML parsing error during initialization:\n{xamlEx.Message}\n\nLine: {xamlEx.LineNumber}, Position: {xamlEx.LinePosition}", 
                               "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during window initialization:\n{ex.Message}\n\nInner Exception: {ex.InnerException?.Message}", 
                               "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void InitializeFileSelectionListBox()
        {
            try
            {
                // Set up the ListBox that's now defined in XAML
                FileSelectionListBox.ItemsSource = _selectedFiles;
                FileSelectionListBox.DisplayMemberPath = "DisplayName";
                FileSelectionListBox.SelectionChanged += FileSelectionListBox_SelectionChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing FileSelectionListBox: {ex.Message}");
                MessageBox.Show($"Warning: Could not initialize file selection list box: {ex.Message}", 
                               "UI Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void FileSelectionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is FileViewModel selectedFile)
            {
                _currentFile = selectedFile;
                textBox1.Text = selectedFile.FileName;
                
                // Always update display if we have loaded content and the current file has been initialized
                if (_currentFile?.Pages != null)
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
                    FileSelectionListBox.SelectedIndex = 0;
                }
            }
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug: Check what's selected
            System.Diagnostics.Debug.WriteLine($"Selected item: {FileSelectionListBox?.SelectedItem}");
            System.Diagnostics.Debug.WriteLine($"Selected index: {FileSelectionListBox?.SelectedIndex}");
            
            // Try multiple ways to get the selected file
            FileViewModel? selectedFile = FileSelectionListBox?.SelectedItem as FileViewModel;
            
            // Fallback: use the current file if no selection but current file exists
            if (selectedFile == null && _currentFile != null)
            {
                selectedFile = _currentFile;
            }
            
            if (selectedFile != null)
            {
                // Dispose of resources if the file has been loaded
                selectedFile.Provider?.Dispose();
                
                // Remove the file from the collection
                _selectedFiles.Remove(selectedFile);
                
                // If this was the current file, update the current file reference
                if (_currentFile == selectedFile)
                {
                    _currentFile = null;
                    textBox1.Text = string.Empty;
                    PagesListBox.ItemsSource = null;
                    FileInfoText.Text = "";
                    
                    // Select the first available file if any exist
                        if (_selectedFiles.Count > 0)
                    {
                        FileSelectionListBox.SelectedIndex = 0;     
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a file from the list to remove.", "No File Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
                    FileSelectionListBox.SelectedItem = fileViewModel;
                }
            }
        }

        private async void DisplayFile_Click(object sender, RoutedEventArgs e)
        {

            if (_selectedFiles.Count == 0)
            {
                MessageBox.Show("No files are selected.", "No Files Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prevent multiple simultaneous loading operations
            if (_isLoading)
            {
                return;
            }

            // Check if there are any uninitialized files that need loading
            var uninitializedFiles = _selectedFiles.Where(f => f.Provider == null).ToList();
            
            if (uninitializedFiles.Count > 0)
            {
                await LoadFilesDataAsync(uninitializedFiles);
            }
            
            if (_currentFile != null)
            {
                DisplayCurrentFile();
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            // Only save data if the current file has loaded pages
            if (_currentFile?.Pages == null)
            {
                MessageBox.Show("Please select and display a file first before saving.", "No Data to Save", 
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

        // Add 'this.' prefix to all usages of LoadingProgressBar and LoadingStatusText inside MainWindow methods
        // Example fix inside LoadFilesDataAsync:
        private async Task LoadFilesDataAsync(List<FileViewModel> filesToLoad)
        {
            try
            {
                _isLoading = true;

                // Show progress controls
                this.LoadingStatusText.Visibility = Visibility.Visible;
                this.LoadingStatusText.Text = "Preparing to load files...";
                
                int totalFiles = filesToLoad.Count;
                int filesLoaded = 0;

                foreach (var fileViewModel in filesToLoad)
                {
                    try
                    {
                        // Update status
                        this.LoadingStatusText.Text = $"Loading {fileViewModel.DisplayName}...";

                        // Simulate some loading time for visual feedback
                        await Task.Delay(100);

                        // Create provider and pages
                        fileViewModel.Provider = new PageProvider(fileViewModel.FileName, pageSize: 64 * 1024, cachePages: 128);
                        fileViewModel.Pages = new PagedFileCollection(fileViewModel.Provider);

                        // Additional delay for very large files (based on file size)
                        var fileInfo = new FileInfo(fileViewModel.FileName);
                        if (fileInfo.Length > 100 * 1024 * 1024) // Files larger than 100MB
                        {
                            await Task.Delay(500);
                        }
                        else if (fileInfo.Length > 10 * 1024 * 1024) // Files larger than 10MB
                        {
                            await Task.Delay(200);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading file {fileViewModel.DisplayName}: {ex.Message}", "File Loading Error", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    filesLoaded++;

                    // Update progress
                    this.LoadingStatusText.Text = $"Loaded {filesLoaded} of {totalFiles} files...";
                }

                this.LoadingStatusText.Text = "All files loaded successfully!";
                // Briefly display the completion status
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                this.LoadingStatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error loading files: {ex.Message}", "Loading Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide progress controls
                this.LoadingStatusText.Visibility = Visibility.Collapsed;

                // Re-enable the Display File button
                button3.IsEnabled = true;
                _isLoading = false;
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