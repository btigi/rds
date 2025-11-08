using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using rds.Data;
using rds.Helpers;
using rds.Models;

namespace rds
{
    public class MediaFileDisplayItem
    {
        public MediaFile MediaFile { get; set; } = null!;
        public string DisplayPath { get; set; } = string.Empty;
        public string FileName => MediaFile.FileName;
    }

    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_MENU = 0x12;
        private const int VK_F4 = 0x73;

        private readonly RdsDbContext _dbContext;
        private readonly IConfiguration _configuration;

        private bool _positionLoaded = false;
        private bool _isInitializing = true;

        public MainWindow(RdsDbContext dbContext, IConfiguration configuration)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _configuration = configuration;
            
            Loaded += MainWindow_Loaded;
            LocationChanged += MainWindow_LocationChanged;
            SizeChanged += MainWindow_SizeChanged;
            StateChanged += MainWindow_StateChanged;
            
            Loaded += (s, e) => _isInitializing = false;
        }

        public void LoadWindowPositionIfNeeded()
        {
            if (!_positionLoaded)
            {
                WindowSettingsHelper.LoadWindowPosition(this, _configuration);
                _positionLoaded = true;
                
                UpdateLayout();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Visibility == Visibility.Visible && !_positionLoaded)
            {
                LoadWindowPositionIfNeeded();
            }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (!_isInitializing && WindowState == WindowState.Normal && Visibility == Visibility.Visible)
            {
                if (Left >= -1000 && Top >= -1000 && Left < SystemParameters.PrimaryScreenWidth + 1000 && Top < SystemParameters.PrimaryScreenHeight + 1000)
                {
                    WindowSettingsHelper.SaveWindowPosition(this, _configuration);
                }
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitializing && WindowState == WindowState.Normal && Visibility == Visibility.Visible)
            {
                WindowSettingsHelper.SaveWindowPosition(this, _configuration);
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (!_isInitializing && Visibility == Visibility.Visible)
            {
                WindowSettingsHelper.SaveWindowPosition(this, _configuration);
            }
            
            AdjustListViewColumnWidths();
        }

        public void AdjustListViewColumnWidths()
        {
            if (ResultsListView.View is System.Windows.Controls.GridView gridView && gridView.Columns.Count >= 2)
            {
                ResultsListView.UpdateLayout();
                var listViewWidth = ResultsListView.ActualWidth;
                
                if (listViewWidth > 0)
                {
                    var columnWidth = listViewWidth / 2;
                    gridView.Columns[0].Width = columnWidth;
                    gridView.Columns[1].Width = columnWidth;
                }
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch();
            }
        }

        private void PerformSearch()
        {
            var searchText = SearchTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                ResultsListView.ItemsSource = null;
                UpdateStatusBar(0, string.Empty);
                return;
            }

            var folders = _dbContext.Folders.ToList();
            var results = _dbContext.MediaFiles
                .ToList()
                .Where(mf => mf.Path.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 || 
                            mf.FileName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(mf => mf.Path)
                .Select(mf => new MediaFileDisplayItem
                {
                    MediaFile = mf,
                    DisplayPath = TransformPath(mf.Path, folders)
                })
                .ToList();

            ResultsListView.ItemsSource = results;
            
            UpdateStatusBar(results.Count, searchText);
            
            if (results.Count > 0)
            {
                ResultsListView.Focus();
                ResultsListView.SelectedIndex = 0;
            }
            else
            {
                SearchTextBox.Focus();
            }
        }

        private string TransformPath(string filePath, System.Collections.Generic.List<Folder> folders)
        {
            foreach (var folder in folders.OrderByDescending(f => f.Path.Length))
            {
                if (filePath.StartsWith(folder.Path, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = filePath.Substring(folder.Path.Length);
                    if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                    {
                        relativePath = relativePath.Substring(1);
                    }

                    var directoryName = Path.GetDirectoryName(relativePath);
                    if (string.IsNullOrWhiteSpace(directoryName))
                    {
                        return GetDisplayName(folder);
                    }

                    var displayName = GetDisplayName(folder);
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        return directoryName;
                    }

                    return Path.Combine(displayName, directoryName);
                }
            }

            var fallbackDir = Path.GetDirectoryName(filePath);
            return string.IsNullOrWhiteSpace(fallbackDir) ? filePath : fallbackDir;
        }

        private string GetDisplayName(Folder folder)
        {
            return folder.DisplayNameMode switch
            {
                DisplayNameMode.Original => Path.GetFileName(folder.Path.TrimEnd('\\', '/')),
                DisplayNameMode.Custom => string.IsNullOrWhiteSpace(folder.DisplayName) 
                    ? Path.GetFileName(folder.Path.TrimEnd('\\', '/')) 
                    : folder.DisplayName,
                DisplayNameMode.Blank => string.Empty,
                _ => Path.GetFileName(folder.Path.TrimEnd('\\', '/'))
            };
        }

        private void UpdateStatusBar(int resultCount, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                StatusTextBlock.Text = "Ready";
            }
            else
            {
                StatusTextBlock.Text = $"{resultCount} result{(resultCount == 1 ? "" : "s")} found for \"{searchText}\"";
            }
        }

        private void ResultsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                FocusSearchBox();
                e.Handled = true;
                return;
            }
            
            if (e.Key == Key.Enter && ResultsListView.SelectedItem is MediaFileDisplayItem selectedItem)
            {
                OpenFile(selectedItem.MediaFile);
            }
        }

        private void ResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsListView.SelectedItem is MediaFileDisplayItem selectedItem)
            {
                OpenFile(selectedItem.MediaFile);
            }
        }

        private void OpenFile(MediaFile file)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = file.Path,
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening file: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void FocusSearchBox()
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                UpdateStatusBar(0, string.Empty);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var isAltPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            var isF4Pressed = (GetAsyncKeyState(VK_F4) & 0x8000) != 0;
            
            if (isAltPressed && isF4Pressed)
            {
                e.Cancel = false;
                Application.Current.Shutdown();
            }
            else
            {
                e.Cancel = true;
                Hide();
            }
            
            base.OnClosing(e);
        }
    }
}

