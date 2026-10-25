using System.Runtime.InteropServices;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Orbit.Controls;
using Orbit.Marketplace;

namespace Orbit;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;
    private const uint WmHotkey = 0x0312;
    private readonly WndProc _windowProc;
    private readonly Dictionary<string, Window> _openWindows = new();
    private readonly Dictionary<string, WidgetMarketplace.LoadedWidget> _loadedMarketplaceWidgets = new();
    private readonly WidgetMarketplace _widgetMarketplace = new();
    private readonly bool _hasSavedPosition;
    private IntPtr _hotkeyWindow;
    private bool _hotkeyRegistered;
    private IntPtr _keyboardHook;
    private readonly HookProc _keyboardHookProc;
    private readonly HashSet<uint> _downKeys = new();
    private bool _chordTriggered;
    private const string BarAnimationKey = "BarDropAnimationEnabled";
    private bool _barAnimationRunning;
    private bool _entryPrepared;
    private PixelPoint _animationOrigin;
    private PixelPoint _animationTarget;

    public MainWindow()
    {
        InitializeComponent();
        _hasSavedPosition = WindowPositionStore.Track(this);
        _windowProc = WindowProcedure;
        _keyboardHookProc = KeyboardHookProcedure;
        Opened += OnOpened;
        Opened += (_, _) => RefreshMarketplaceWidgetButtons();
        OrbitHotkey.Changed += UpdateHotkey;
        _ = WarmMarketplaceIndexAsync();
    }

    private async Task WarmMarketplaceIndexAsync()
    {
        try
        {
            await _widgetMarketplace.LoadIndexAsync();
        }
        catch
        {
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_entryPrepared)
        {
            _entryPrepared = false;
            return;
        }

        var screen = Screens.Primary;
        if (!_hasSavedPosition && screen is not null)
        {
            var scale = screen.Scaling;
            Position = new PixelPoint(
                screen.WorkingArea.X + (int)((screen.WorkingArea.Width - Width * scale) / 2),
                screen.WorkingArea.Y + (int)(24 * scale));
        }

        PrepareBarEntry();
        _ = AnimateBarAsync(true);

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

        UpdateHotkey();
    }

    private void UpdateHotkey()
    {
        if (_hotkeyWindow == IntPtr.Zero)
            return;

        if (_hotkeyRegistered)
            UnregisterHotKey(_hotkeyWindow, HotkeyId);
        _hotkeyRegistered = false;

        if (_keyboardHook != IntPtr.Zero)
            UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = SetWindowsHookEx(13, _keyboardHookProc, IntPtr.Zero, 0);
        _downKeys.Clear();
        _chordTriggered = false;
    }

    private IntPtr KeyboardHookProcedure(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var key = (uint)Marshal.ReadInt32(lParam);
            if (message is 0x0100 or 0x0104)
            {
                _downKeys.Add(key);
                var binding = OrbitHotkey.Current;
                var keys = OrbitHotkey.GetKeys(binding);
                if (!_chordTriggered && keys.All(_downKeys.Contains) && AreModifiersDown(binding.Modifiers))
                {
                    _chordTriggered = true;
                    Avalonia.Threading.Dispatcher.UIThread.Post(ToggleOverlay);
                }
            }
            else if (message is 0x0101 or 0x0105)
            {
                _downKeys.Remove(key);
                _chordTriggered = false;
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private bool AreModifiersDown(uint modifiers)
    {
        return ((modifiers & 2) == 0 || _downKeys.Contains(0xA2) || _downKeys.Contains(0xA3))
            && ((modifiers & 1) == 0 || _downKeys.Contains(0xA4) || _downKeys.Contains(0xA5))
            && ((modifiers & 4) == 0 || _downKeys.Contains(0xA0) || _downKeys.Contains(0xA1))
            && ((modifiers & 8) == 0 || _downKeys.Contains(0x5B) || _downKeys.Contains(0x5C));
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

    private async void ToggleOverlay()
    {
        if (_barAnimationRunning)
            return;

        if (IsVisible)
        {
            await AnimateBarAsync(false);
            Hide();
            Position = _animationOrigin;
            WindowPositionStore.SetTrackingPaused(this, false);
            _openWindows.Values.LastOrDefault(window => window.IsVisible)?.Activate();
            return;
        }

        PrepareBarEntry();
        Show();
        Topmost = false;
        Topmost = true;
        Activate();
        await AnimateBarAsync(true);
    }

    private async Task AnimateBarAsync(bool entering)
    {
        if (!OrbitSettings.GetBool(BarAnimationKey, true))
        {
            if (entering)
                Position = _animationOrigin;
            return;
        }

        _barAnimationRunning = true;
        var target = _animationTarget;
        var stopwatch = Stopwatch.StartNew();
        const double duration = 320;
        while (stopwatch.Elapsed.TotalMilliseconds < duration && IsVisible)
        {
            var progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            var startY = entering ? target.Y : _animationOrigin.Y;
            var endY = entering ? _animationOrigin.Y : target.Y;
            Position = new PixelPoint(_animationOrigin.X, (int)(startY + (endY - startY) * eased));
            await Task.Delay(16);
        }

        Position = entering ? _animationOrigin : target;
        _barAnimationRunning = false;
    }

    private void PrepareBarEntry()
    {
        _entryPrepared = true;
        _animationOrigin = Position;
        if (!OrbitSettings.GetBool(BarAnimationKey, true))
            return;

        WindowPositionStore.SetTrackingPaused(this, true);
        _animationTarget = GetAboveScreenPosition(_animationOrigin);
        Position = _animationTarget;
    }

    private PixelPoint GetAboveScreenPosition(PixelPoint origin)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return new PixelPoint(origin.X, -((int)(Height * 2) + 16));

        var scale = screen.Scaling;
        var height = (int)(Height * scale);
        return new PixelPoint(origin.X, screen.Bounds.Y - height - (int)(16 * scale));
    }

    private void OnClipClick(object? sender, RoutedEventArgs e) => ToggleWindow("video-clipping", () => new InfoWindow("Video clipping", "This widget is not active yet."));
    private void OnScreenshotClick(object? sender, RoutedEventArgs e) => ToggleWindow("screenshots", () => new InfoWindow("Screenshots", "This widget is not active yet."));
    private void OnPerformanceClick(object? sender, RoutedEventArgs e) => ToggleWindow("performance", () => new PerformanceWindow());
    private void OnMusicClick(object? sender, RoutedEventArgs e) => ToggleWindow("music", () => new MusicWindow());
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => ToggleWindow("settings", () => new SettingsWindow());
    private void OnWidgetsClick(object? sender, RoutedEventArgs e) => ToggleWindow("widgets", () => new MarketplaceWindow(_widgetMarketplace, OpenMarketplaceWidgetAsync, RefreshMarketplaceWidgetButtons, CloseMarketplaceWidget));

    private Task OpenMarketplaceWidgetAsync(MarketplaceWidget widget)
    {
        ToggleWindow($"marketplace:{widget.Id}", () =>
        {
            var loaded = _widgetMarketplace.Load(widget);
            var content = loaded.Instance.CreateView();
            _loadedMarketplaceWidgets[widget.Id] = loaded;
            return new MarketplaceWidgetWindow(widget.Name, content);
        });
        return Task.CompletedTask;
    }

    private void CloseMarketplaceWidget(MarketplaceWidget widget)
    {
        var key = $"marketplace:{widget.Id}";
        if (_openWindows.TryGetValue(key, out var window))
            window.Close();
        _loadedMarketplaceWidgets.Remove(widget.Id);
    }

    private void RefreshMarketplaceWidgetButtons()
    {
        if (this.FindControl<StackPanel>("WidgetButtons") is not { } panel)
            return;

        foreach (var oldButton in panel.Children.OfType<Button>().Where(button => button.Tag is string).ToArray())
            panel.Children.Remove(oldButton);

        foreach (var widget in _widgetMarketplace.GetInstalledWidgets())
        {
            var key = $"marketplace:{widget.Id}";
            var iconKind = Enum.TryParse<OrbitIconKind>(widget.Icon, true, out var parsedIcon)
                ? parsedIcon
                : widget.Id == "orbit.weather" ? OrbitIconKind.Weather : OrbitIconKind.Marketplace;
            var button = new Button
            {
                Tag = key,
                Classes = { "overlay-icon-button" },
                Content = new OrbitIcon(iconKind) { Width = 20, Height = 20, Stroke = OrbitTheme.Icon, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }
            };
            ToolTip.SetTip(button, widget.Name);
            button.Click += async (_, _) => await OpenMarketplaceWidgetAsync(widget);
            panel.Children.Add(button);
        }
    }

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
            "music" => "MusicButton",
            _ => null
        };
        var button = buttonName is null
            ? this.FindControl<StackPanel>("WidgetButtons")?.Children.OfType<Button>().FirstOrDefault(item => item.Tag as string == key)
            : this.FindControl<Button>(buttonName);
        if (button is null)
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
        OrbitHotkey.Changed -= UpdateHotkey;
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_hotkeyWindow == IntPtr.Zero)
            return;

        if (_hotkeyRegistered)
            UnregisterHotKey(_hotkeyWindow, HotkeyId);
        DestroyWindow(_hotkeyWindow);
        UnregisterClass("OrbitHotkeyWindow", IntPtr.Zero);
        _hotkeyWindow = IntPtr.Zero;
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

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
