using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using rds.Data;
using rds.Models;

namespace rds
{
    public partial class ConfigWindow : Window
    {
        private readonly RdsDbContext _dbContext;

        public ConfigWindow(RdsDbContext dbContext)
        {
            InitializeComponent();
            _dbContext = dbContext;
            LoadFolders();
        }

        private void LoadFolders()
        {
            var folders = _dbContext.Folders.OrderBy(f => f.Path).ToList();
            FoldersDataGrid.ItemsSource = null;
            FoldersDataGrid.ItemsSource = folders;
        }

        private void FoldersDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var hasSelection = FoldersDataGrid.SelectedItem != null;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
        }

        private void DisplayNameModeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox)
            {
                comboBox.ItemsSource = Enum.GetValues(typeof(DisplayNameMode));
            }
        }

        private void FoldersDataGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is Folder folder)
            {
                if (e.EditingElement is System.Windows.Controls.ComboBox comboBox)
                {
                    if (comboBox.SelectedItem is DisplayNameMode mode)
                    {
                        folder.DisplayNameMode = mode;
                        if (mode == DisplayNameMode.Blank || mode == DisplayNameMode.Original)
                        {
                            folder.DisplayName = null;
                        }
                    }
                }
                else if (e.EditingElement is System.Windows.Controls.TextBox textBox)
                {
                    if (e.Column.Header.ToString() == "Display Name")
                    {
                        if (folder.DisplayNameMode == DisplayNameMode.Custom)
                        {
                            folder.DisplayName = string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text;
                        }
                        else
                        {
                            folder.DisplayName = null;
                        }
                    }
                }

                _dbContext.SaveChanges();
                LoadFolders();
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select a folder to add";
            
            if (dialog.ShowDialog() == WinFormsDialogResult.OK)
            {
                var folderPath = dialog.SelectedPath;
                
                if (_dbContext.Folders.Any(f => f.Path == folderPath))
                {
                    System.Windows.MessageBox.Show("This folder is already in the list.", "Duplicate Folder", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var folder = new Folder 
                { 
                    Path = folderPath,
                    DisplayNameMode = DisplayNameMode.Original,
                    DisplayName = null
                };
                _dbContext.Folders.Add(folder);
                _dbContext.SaveChanges();
                LoadFolders();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersDataGrid.SelectedItem is Folder selectedFolder)
            {
                using var dialog = new FolderBrowserDialog();
                dialog.Description = "Select a new folder path";
                dialog.SelectedPath = selectedFolder.Path;
                
                if (dialog.ShowDialog() == WinFormsDialogResult.OK)
                {
                    var newPath = dialog.SelectedPath;
                    
                    if (_dbContext.Folders.Any(f => f.Path == newPath && f.Id != selectedFolder.Id))
                    {
                        System.Windows.MessageBox.Show("This folder is already in the list.", "Duplicate Folder", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    selectedFolder.Path = newPath;
                    _dbContext.SaveChanges();
                    LoadFolders();
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersDataGrid.SelectedItem is Folder selectedFolder)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete the folder:\n{selectedFolder.Path}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _dbContext.Folders.Remove(selectedFolder);
                    _dbContext.SaveChanges();
                    LoadFolders();
                }
            }
        }
    }
}

