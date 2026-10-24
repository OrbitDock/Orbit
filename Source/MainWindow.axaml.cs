using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;

namespace Orbit;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint WmHotkey = 0x0312;
    private const int GwlpWndProc = -4;
    private readonly WndProc _windowProc;
    private readonly Dictionary<string, Window> _openWindows = new();
    private IntPtr _handle;
    private IntPtr _previousWndProc;
    private bool _hotkeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
        _windowProc = WindowProcedure;
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary;
        if (screen is not null)
        {
            var scale = screen.Scaling;
            Position = new PixelPoint(
                screen.WorkingArea.X + (int)((screen.WorkingArea.Width - Width * scale) / 2),
                screen.WorkingArea.Y + (int)(24 * scale));
        }

        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is null || !OperatingSystem.IsWindows())
            return;

        _handle = platformHandle.Handle;
        Marshal.SetLastPInvokeError(0);
        _previousWndProc = SetWindowLongPtr(_handle, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_windowProc));
        if (_previousWndProc == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0)
            return;

        _hotkeyRegistered = RegisterHotKey(_handle, HotkeyId, ModControl | ModAlt, 0x4F);
        if (!_hotkeyRegistered)
        {
            SetWindowLongPtr(_handle, GwlpWndProc, _previousWndProc);
            _previousWndProc = IntPtr.Zero;
        }
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsVisible = !IsVisible);
            return IntPtr.Zero;
        }

        return CallWindowProc(_previousWndProc, hwnd, message, wParam, lParam);
    }

    private void OnClipClick(object? sender, RoutedEventArgs e) => ToggleWindow("video-clipping", () => new InfoWindow("Video clipping", "This widget is not active yet."));
    private void OnScreenshotClick(object? sender, RoutedEventArgs e) => ToggleWindow("screenshots", () => new InfoWindow("Screenshots", "This widget is not active yet."));
    private void OnPerformanceClick(object? sender, RoutedEventArgs e) => ToggleWindow("performance", () => new PerformanceWindow());
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => ToggleWindow("settings", () => new SettingsWindow());
    private void OnWidgetsClick(object? sender, RoutedEventArgs e) => ToggleWindow("widgets", () => new WidgetsWindow());

    private void ToggleWindow(string key, Func<Window> createWindow)
    {
        if (_openWindows.TryGetValue(key, out var existing))
        {
            if (existing.IsVisible)
            {
                existing.Close();
                return;
            }

            _openWindows.Remove(key);
        }

        var window = createWindow();
        _openWindows[key] = window;
        window.Closed += (_, _) =>
        {
            if (_openWindows.TryGetValue(key, out var openWindow) && ReferenceEquals(openWindow, window))
                _openWindows.Remove(key);
        };
        window.Show();
    }

    public void DisposeHotkey()
    {
        if (_handle == IntPtr.Zero)
            return;

        if (_hotkeyRegistered)
            UnregisterHotKey(_handle, HotkeyId);
        if (_previousWndProc != IntPtr.Zero)
            SetWindowLongPtr(_handle, GwlpWndProc, _previousWndProc);
        _handle = IntPtr.Zero;
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr previousProcedure, IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
