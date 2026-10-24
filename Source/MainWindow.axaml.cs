using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Orbit.Controls;

namespace Orbit;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint WmHotkey = 0x0312;
    private readonly WndProc _windowProc;
    private readonly Dictionary<string, Window> _openWindows = new();
    private readonly bool _hasSavedPosition;
    private IntPtr _hotkeyWindow;
    private bool _hotkeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
        _hasSavedPosition = WindowPositionStore.Track(this);
        _windowProc = WindowProcedure;
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary;
        if (!_hasSavedPosition && screen is not null)
        {
            var scale = screen.Scaling;
            Position = new PixelPoint(
                screen.WorkingArea.X + (int)((screen.WorkingArea.Width - Width * scale) / 2),
                screen.WorkingArea.Y + (int)(24 * scale));
        }

        if (!OperatingSystem.IsWindows())
            return;

        var windowClass = new WndClass
        {
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(_windowProc),
            ClassName = "OrbitHotkeyWindow"
        };
        if (RegisterClass(ref windowClass) == 0)
            return;

        _hotkeyWindow = CreateWindowEx(0, windowClass.ClassName, string.Empty, 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_hotkeyWindow == IntPtr.Zero)
        {
            UnregisterClass(windowClass.ClassName, IntPtr.Zero);
            return;
        }

        _hotkeyRegistered = RegisterHotKey(_hotkeyWindow, HotkeyId, ModControl | ModAlt, 0x4F);
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(ToggleOverlay);
            return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void ToggleOverlay()
    {
        if (IsVisible)
        {
            Hide();
            _openWindows.Values.LastOrDefault(window => window.IsVisible)?.Activate();
            return;
        }

        Show();
        Topmost = false;
        Topmost = true;
        Activate();
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
            SetWidgetButtonActive(key, false);
        };
        window.Show();
        window.Activate();
        SetWidgetButtonActive(key, true);
    }

    private void SetWidgetButtonActive(string key, bool active)
    {
        var buttonName = key switch
        {
            "video-clipping" => "ClipButton",
            "screenshots" => "ScreenshotButton",
            "performance" => "PerformanceButton",
            _ => null
        };
        if (buttonName is null || this.FindControl<Button>(buttonName) is not { } button)
            return;

        if (active)
            button.Classes.Add("widget-active");
        else
            button.Classes.Remove("widget-active");

        if (button.Content is OrbitIcon icon)
            icon.Stroke = active ? OrbitTheme.Accent : OrbitTheme.Icon;
    }

    public void DisposeHotkey()
    {
        if (_hotkeyWindow == IntPtr.Zero)
            return;

        if (_hotkeyRegistered)
            UnregisterHotKey(_hotkeyWindow, HotkeyId);
        DestroyWindow(_hotkeyWindow);
        UnregisterClass("OrbitHotkeyWindow", IntPtr.Zero);
        _hotkeyWindow = IntPtr.Zero;
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint Style;
        public IntPtr WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WndClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
