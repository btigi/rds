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

            const uint VK_BACK = 0x08;
            const uint VK_RETURN = 0x0D;
            const int HOTKEY_ID_SHOW = 9000;
            const int HOTKEY_ID_TOGGLE = 9001;
            const uint MOD_CONTROL_ALT = 0x0002 | 0x0001;
            const uint MOD_ALT = 0x0001;
            
            try
            {
                _globalHotkey = new GlobalHotkey(
                    MainWindow!,
                    HOTKEY_ID_SHOW,
                    VK_BACK,
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
                    MOD_CONTROL_ALT);

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
                    Text = "Scanning folders for MP3 and M3U files...",
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
                            var mp3Files = Directory.GetFiles(
                                folder.Path,
                                "*.mp3",
                                SearchOption.AllDirectories);

                            var m3uFiles = Directory.GetFiles(
                                folder.Path,
                                "*.m3u",
                                SearchOption.AllDirectories);

                            foreach (var filePath in mp3Files)
                            {
                                var fileInfo = new FileInfo(filePath);
                                foundFiles.Add(new MediaFile
                                {
                                    Path = filePath,
                                    FileName = fileInfo.Name,
                                    Extension = fileInfo.Extension.ToLowerInvariant()
                                });
                            }

                            foreach (var filePath in m3uFiles)
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
                    $"Sync completed. Found {totalFiles} MP3 and M3U files.",
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

