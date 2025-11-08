using System;
using System.IO;
using System.Linq;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using rds.Data;
using rds.Helpers;
using rds.Models;

namespace rds
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private IConfiguration? _configuration;
        private RdsDbContext? _dbContext;
        private GlobalHotkey? _globalHotkey;
        private GlobalHotkey? _toggleMaximizeHotkey;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            _configuration = builder.Build();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var options = new DbContextOptionsBuilder<RdsDbContext>()
                .UseSqlite(connectionString)
                .Options;
            _dbContext = new RdsDbContext(options);
            
            _dbContext.Database.EnsureCreated();
            
            EnsureMediaFilesTableExists();

            _notifyIcon = new TaskbarIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                ToolTipText = "RDS",
                Visibility = Visibility.Visible
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            var showMenuItem = new System.Windows.Controls.MenuItem { Header = "Show" };
            showMenuItem.Click += ShowMenuItem_Click;
            contextMenu.Items.Add(showMenuItem);

            var configureMenuItem = new System.Windows.Controls.MenuItem { Header = "Configure" };
            configureMenuItem.Click += ConfigureMenuItem_Click;
            contextMenu.Items.Add(configureMenuItem);

            var syncMenuItem = new System.Windows.Controls.MenuItem { Header = "Sync" };
            syncMenuItem.Click += SyncMenuItem_Click;
            contextMenu.Items.Add(syncMenuItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var quitMenuItem = new System.Windows.Controls.MenuItem { Header = "Quit" };
            quitMenuItem.Click += QuitMenuItem_Click;
            contextMenu.Items.Add(quitMenuItem);

            _notifyIcon.ContextMenu = contextMenu;

            MainWindow = new MainWindow(_dbContext, _configuration);
            
            MainWindow.SourceInitialized += (s, e) =>
            {
                RegisterGlobalHotkey();
            };

            // We apparently need to show the form once to get a valid handle for hotkeys
            MainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            MainWindow.Left = -10000;
            MainWindow.Top = -10000;
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Show();
            MainWindow.Hide();
        }

        private void RegisterGlobalHotkey()
        {
            if (_globalHotkey != null)
                return;

            const uint VK_RETURN = 0x0D;
            const int HOTKEY_ID_SHOW = 9000;
            const int HOTKEY_ID_TOGGLE = 9001;
            const uint MOD_ALT = 0x0001;
            
            try
            {
                var modifiers = _configuration!.GetSection("Hotkeys:ShowWindow:Modifiers").Get<string[]>() ?? new[] { "Ctrl", "Alt" };
                var keyName = _configuration["Hotkeys:ShowWindow:Key"] ?? "Backspace";
                
                var virtualKey = ParseVirtualKey(keyName);
                var modifierFlags = ParseModifiers(modifiers);
                
                _globalHotkey = new GlobalHotkey(
                    MainWindow!,
                    HOTKEY_ID_SHOW,
                    virtualKey,
                    () =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (MainWindow != null)
                            {
                                var mainWin = (MainWindow)MainWindow;
                                if (MainWindow.Width <= 0) MainWindow.Width = 800;
                                if (MainWindow.Height <= 0) MainWindow.Height = 450;
                                MainWindow.WindowState = WindowState.Normal;
                                mainWin.LoadWindowPositionIfNeeded();
                                MainWindow.Show();
                                MainWindow.Activate();
                                mainWin.FocusSearchBox();
                            }
                        });
                    },
                    modifierFlags);

                _toggleMaximizeHotkey = new GlobalHotkey(
                    MainWindow!,
                    HOTKEY_ID_TOGGLE,
                    VK_RETURN,
                    () =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (MainWindow != null && MainWindow.Visibility == Visibility.Visible)
                            {
                                var mainWin = (MainWindow)MainWindow;
                                if (MainWindow.WindowState == WindowState.Maximized)
                                {
                                    MainWindow.WindowState = WindowState.Normal;
                                }
                                else
                                {
                                    MainWindow.WindowState = WindowState.Maximized;
                                }
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    mainWin.AdjustListViewColumnWidths();
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        });
                    },
                    MOD_ALT);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register global hotkey: {ex.Message}");
            }
        }

        private uint ParseVirtualKey(string keyName)
        {
            return keyName.ToUpperInvariant() switch
            {
                "BACKSPACE" => 0x08,
                "TAB" => 0x09,
                "ENTER" => 0x0D,
                "SHIFT" => 0x10,
                "CTRL" => 0x11,
                "ALT" => 0x12,
                "PAUSE" => 0x13,
                "CAPSLOCK" => 0x14,
                "ESC" => 0x1B,
                "ESCAPE" => 0x1B,
                "SPACE" => 0x20,
                "PAGEUP" => 0x21,
                "PAGEDOWN" => 0x22,
                "END" => 0x23,
                "HOME" => 0x24,
                "LEFT" => 0x25,
                "UP" => 0x26,
                "RIGHT" => 0x27,
                "DOWN" => 0x28,
                "INSERT" => 0x2D,
                "DELETE" => 0x2E,
                "0" => 0x30,
                "1" => 0x31,
                "2" => 0x32,
                "3" => 0x33,
                "4" => 0x34,
                "5" => 0x35,
                "6" => 0x36,
                "7" => 0x37,
                "8" => 0x38,
                "9" => 0x39,
                "A" => 0x41,
                "B" => 0x42,
                "C" => 0x43,
                "D" => 0x44,
                "E" => 0x45,
                "F" => 0x46,
                "G" => 0x47,
                "H" => 0x48,
                "I" => 0x49,
                "J" => 0x4A,
                "K" => 0x4B,
                "L" => 0x4C,
                "M" => 0x4D,
                "N" => 0x4E,
                "O" => 0x4F,
                "P" => 0x50,
                "Q" => 0x51,
                "R" => 0x52,
                "S" => 0x53,
                "T" => 0x54,
                "U" => 0x55,
                "V" => 0x56,
                "W" => 0x57,
                "X" => 0x58,
                "Y" => 0x59,
                "Z" => 0x5A,
                "F1" => 0x70,
                "F2" => 0x71,
                "F3" => 0x72,
                "F4" => 0x73,
                "F5" => 0x74,
                "F6" => 0x75,
                "F7" => 0x76,
                "F8" => 0x77,
                "F9" => 0x78,
                "F10" => 0x79,
                "F11" => 0x7A,
                "F12" => 0x7B,
                _ => 0x08
            };
        }

        private uint ParseModifiers(string[] modifiers)
        {
            uint flags = 0;
            foreach (var modifier in modifiers)
            {
                var mod = modifier.ToUpperInvariant();
                if (mod == "CTRL" || mod == "CONTROL")
                {
                    flags |= 0x0002;
                }
                else if (mod == "ALT")
                {
                    flags |= 0x0001;
                }
                else if (mod == "SHIFT")
                {
                    flags |= 0x0004;
                }
            }
            return flags;
        }

        private void ShowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow != null)
            {
                var mainWin = (MainWindow)MainWindow;
                if (MainWindow.Width <= 0) MainWindow.Width = 800;
                if (MainWindow.Height <= 0) MainWindow.Height = 450;
                MainWindow.WindowState = WindowState.Normal;
                mainWin.LoadWindowPositionIfNeeded();
                MainWindow.Show();
                MainWindow.Activate();
                mainWin.FocusSearchBox();
            }
        }

        private void ConfigureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ConfigWindow(_dbContext!);
            configWindow.ShowDialog();
        }

        private async void SyncMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folders = _dbContext!.Folders.ToList();
                
                if (folders.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "No folders configured. Please configure folders first.",
                        "No Folders",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var extensions = _configuration!.GetSection("FileExtensions").Get<string[]>() ?? new[] { "mp3", "m3u" };
                var extensionList = string.Join(", ", extensions.Select(ext => ext.ToUpperInvariant()));

                var progressWindow = new System.Windows.Window
                {
                    Title = "Syncing...",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize
                };

                var progressText = new System.Windows.Controls.TextBlock
                {
                    Text = $"Scanning folders for {extensionList} files...",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20)
                };

                progressWindow.Content = progressText;
                progressWindow.Show();
                progressWindow.Activate();

                var totalFiles = 0;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    var foundFiles = new System.Collections.Generic.List<MediaFile>();

                    foreach (var folder in folders)
                    {
                        if (!Directory.Exists(folder.Path))
                        {
                            continue;
                        }

                        try
                        {
                            foreach (var extension in extensions)
                            {
                                var searchPattern = $"*.{extension.TrimStart('.')}";
                                var files = Directory.GetFiles(
                                    folder.Path,
                                    searchPattern,
                                    SearchOption.AllDirectories);

                                foreach (var filePath in files)
                                {
                                    var fileInfo = new FileInfo(filePath);
                                    foundFiles.Add(new MediaFile
                                    {
                                        Path = filePath,
                                        FileName = fileInfo.Name,
                                        Extension = fileInfo.Extension.ToLowerInvariant()
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error scanning folder {folder.Path}: {ex.Message}");
                        }
                    }

                    totalFiles = foundFiles.Count;

                    Dispatcher.Invoke(() =>
                    {
                        foreach (var file in foundFiles)
                        {
                            if (!_dbContext!.MediaFiles.Any(mf => mf.Path == file.Path))
                            {
                                _dbContext!.MediaFiles.Add(file);
                            }
                            else
                            {
                                var existingFile = _dbContext!.MediaFiles.FirstOrDefault(mf => mf.Path == file.Path);
                                if (existingFile != null)
                                {
                                    existingFile.ScannedAt = DateTime.Now;
                                }
                            }
                        }

                        _dbContext!.SaveChanges();
                    });
                });

                progressWindow.Close();

                System.Windows.MessageBox.Show(
                    $"Sync completed. Found {totalFiles} {extensionList} file{(totalFiles == 1 ? "" : "s")}.",
                    "Sync Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error during sync: {ex.Message}",
                    "Sync Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void QuitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _notifyIcon?.Dispose();
            _dbContext?.Dispose();
            Shutdown();
        }

        private void EnsureMediaFilesTableExists()
        {
            try
            {
                _dbContext!.Database.ExecuteSqlRaw("SELECT COUNT(*) FROM MediaFiles");
            }
            catch
            {
                _dbContext!.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS MediaFiles (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Path TEXT NOT NULL,
                        FileName TEXT NOT NULL,
                        Extension TEXT NOT NULL,
                        ScannedAt TEXT NOT NULL
                    );
                ");
                
                try
                {
                    _dbContext!.Database.ExecuteSqlRaw(
                        "CREATE UNIQUE INDEX IF NOT EXISTS IX_MediaFiles_Path ON MediaFiles(Path);");
                }
                catch
                {
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _globalHotkey?.Dispose();
            _toggleMaximizeHotkey?.Dispose();
            _notifyIcon?.Dispose();
            _dbContext?.Dispose();
            base.OnExit(e);
        }
    }
}

