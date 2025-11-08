using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Configuration;

namespace rds.Helpers
{
    public static class WindowSettingsHelper
    {
        private static readonly string AppSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        public static void SaveWindowPosition(Window window, IConfiguration configuration)
        {
            try
            {
                var json = File.ReadAllText(AppSettingsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var windowState = window.WindowState == WindowState.Maximized ? "Maximized" : "Normal";
                var left = window.WindowState == WindowState.Maximized ? window.RestoreBounds.Left : window.Left;
                var top = window.WindowState == WindowState.Maximized ? window.RestoreBounds.Top : window.Top;
                var width = window.WindowState == WindowState.Maximized ? window.RestoreBounds.Width : window.Width;
                var height = window.WindowState == WindowState.Maximized ? window.RestoreBounds.Height : window.Height;

                var settings = new
                {
                    ConnectionStrings = new
                    {
                        DefaultConnection = root.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()
                    },
                    WindowSettings = new
                    {
                        Left = left,
                        Top = top,
                        Width = width,
                        Height = height,
                        WindowState = windowState
                    }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonString = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(AppSettingsPath, jsonString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save window position: {ex.Message}");
            }
        }

        public static void LoadWindowPosition(Window window, IConfiguration configuration)
        {
            try
            {
                var left = configuration.GetValue<double>("WindowSettings:Left", -1);
                var top = configuration.GetValue<double>("WindowSettings:Top", -1);
                var width = configuration.GetValue<double>("WindowSettings:Width", 800);
                var height = configuration.GetValue<double>("WindowSettings:Height", 450);
                var windowStateStr = configuration.GetValue<string>("WindowSettings:WindowState", "Normal");

                if (width > 0 && height > 0)
                {
                    window.Width = width;
                    window.Height = height;
                }

                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                var maxLeft = screenWidth + 500;
                var maxTop = screenHeight + 500;
                
                if (left >= 0 && top >= 0 && left < maxLeft && top < maxTop)
                {
                    window.Left = left;
                    window.Top = top;
                }
                else
                {
                    window.Left = (screenWidth - window.Width) / 2;
                    window.Top = (screenHeight - window.Height) / 2;
                }

                if (Enum.TryParse<WindowState>(windowStateStr, out var windowState))
                {
                    window.WindowState = windowState;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load window position: {ex.Message}");
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                window.Left = (screenWidth - window.Width) / 2;
                window.Top = (screenHeight - window.Height) / 2;
            }
        }
    }
}

