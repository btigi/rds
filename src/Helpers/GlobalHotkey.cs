using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace rds.Helpers
{
    public class GlobalHotkey : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private int _hotkeyId;
        private Action? _callback;

        public GlobalHotkey(Window window, int hotkeyId, uint virtualKey, Action callback, uint modifiers)
        {
            _hotkeyId = hotkeyId;
            _callback = callback;

            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.EnsureHandle();

            _source = HwndSource.FromHwnd(_windowHandle);
            if (_source == null)
            {
                var presentationSource = PresentationSource.FromVisual(window);
                _source = presentationSource as HwndSource;
            }

            if (_source == null)
            {
                throw new InvalidOperationException("Window handle is not available. Ensure the window has been shown at least once.");
            }

            _source.AddHook(HwndHook);

            if (!RegisterHotKey(_windowHandle, _hotkeyId, modifiers, virtualKey))
            {
                throw new InvalidOperationException("Failed to register hotkey. It may already be registered by another application.");
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                _callback?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }

            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, _hotkeyId);
            }
        }
    }
}

