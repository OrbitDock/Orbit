using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Orbit.Controls;
using System.Text.Json;
using Avalonia.Threading;

namespace Orbit.Performance;

public sealed class PerformanceOverlayWindow : Window
{
    private const string SettingKey = "InGamePerformanceOverlay";
    private const string LockSettingKey = "InGamePerformanceOverlayLocked";
    private readonly TextBlock _stats = new();
    private readonly SystemPerformanceMonitor _monitor = new();
    private readonly GameFrameRateMonitor _frameRateMonitor = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private SystemPerformanceSnapshot _snapshot = new(null, null, null);
    private bool _sampling;
    public static PerformanceOverlayWindow Shared { get; } = new();
    public event EventHandler? OverlayClosed;

    public static bool OverlayEnabled
    {
        get
        {
            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(SettingsPath));
                return settings is not null && settings.TryGetValue(SettingKey, out var enabled) && enabled;
            }
            catch (Exception)
            {
                return false;
            }
        }
        set
        {
            try
            {
                var settings = new Dictionary<string, bool>();
                if (File.Exists(SettingsPath))
                    settings = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(SettingsPath)) ?? settings;
                settings[SettingKey] = value;
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
            }
            catch (Exception)
            {
            }
        }
    }

    public static bool IsLocked
    {
        get
        {
            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(SettingsPath));
                return settings is null || !settings.TryGetValue(LockSettingKey, out var locked) || locked;
            }
            catch (Exception)
            {
                return true;
            }
        }
        set => SaveSetting(LockSettingKey, value);
    }

    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbit", "settings.json");

    private static void SaveSetting(string key, bool value)
    {
        try
        {
            var settings = new Dictionary<string, bool>();
            if (File.Exists(SettingsPath))
                settings = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(SettingsPath)) ?? settings;
            settings[key] = value;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch (Exception)
        {
        }
    }

    public PerformanceOverlayWindow()
    {
        Width = 250;
        Height = 56;
        MinWidth = Width;
        MaxWidth = Width;
        MinHeight = Height;
        MaxHeight = Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 41, 41, 41)),
            BorderBrush = OrbitTheme.Accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(9, 5),
            Child = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = "ORBIT  |  PERFORMANCE", FontSize = 9, FontWeight = FontWeight.SemiBold, Foreground = OrbitTheme.Muted },
                    _stats
                }
            }
        };
        card.PointerPressed += OnPointerPressed;
        Content = card;
        ContextMenu = CreateContextMenu();
        Opened += (_, _) =>
        {
            if (Position == default)
                PositionTopLeft();
        };
        _timer.Tick += OnTimerTick;
        _frameTimer.Tick += (_, _) => UpdateStats();
        Opened += (_, _) =>
        {
            _timer.Start();
            _frameTimer.Start();
        };
        Deactivated += (_, _) => KeepOnTop();
        Orbit.WindowPositionStore.Track(this);
    }

    public void Update(SystemPerformanceSnapshot snapshot)
    {
        _snapshot = snapshot;
        UpdateStats();
    }

    private void UpdateStats()
    {
        if (_snapshot.Memory is { TotalBytes: > 0 } memory)
        {
            var used = memory.UsedBytes * 100d / memory.TotalBytes;
            _stats.Text = $"FPS {FormatFps(_frameRateMonitor.ReadFramesPerSecond())}  CPU {Format(_snapshot.CpuUsage)}\nGPU {Format(_snapshot.GpuUsage)}  MEM {used:0}% ({FormatBytes(memory.UsedBytes)}/{FormatBytes(memory.TotalBytes)})";
        }
        else
        {
            _stats.Text = $"FPS {FormatFps(_frameRateMonitor.ReadFramesPerSecond())}  CPU {Format(_snapshot.CpuUsage)}\nGPU {Format(_snapshot.GpuUsage)}  MEM N/A";
        }
        _stats.FontSize = 11;
        _stats.FontWeight = FontWeight.Medium;
        _stats.Foreground = OrbitTheme.Text;
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_sampling || !OverlayEnabled)
            return;

        KeepOnTop();
        _sampling = true;
        try
        {
            var snapshot = await Task.Run(_monitor.Read);
            Update(snapshot);
        }
        catch (Exception)
        {
            Update(new(null, null, null));
        }
        finally
        {
            _sampling = false;
        }
    }

    private void KeepOnTop()
    {
        if (!OperatingSystem.IsWindows() || !IsVisible || TryGetPlatformHandle() is not { } handle)
            return;

        SetWindowPos(handle.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    private void PositionTopLeft()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;

        var scale = screen.Scaling;
        Position = new PixelPoint(
            screen.WorkingArea.X + (int)(12 * scale),
            screen.WorkingArea.Y + (int)(12 * scale));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            var menu = ContextMenu;
            if (menu is not null)
            {
                menu.Open(this);
            }
            e.Handled = true;
            return;
        }

        if (!IsLocked && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void SetLocked(bool locked)
    {
        IsLocked = locked;
    }

    private void CloseOverlay()
    {
        OverlayEnabled = false;
        Hide();
        OverlayClosed?.Invoke(this, EventArgs.Empty);
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu
        {
            Background = OrbitTheme.Surface,
            BorderBrush = OrbitTheme.Line,
            BorderThickness = new Thickness(1),
            Items =
            {
                new MenuItem { Header = "Unlock overlay" },
                new MenuItem { Header = "Close overlay" }
            }
        };
        ((MenuItem)menu.Items[0]!).Click += (_, _) => SetLocked(!IsLocked);
        ((MenuItem)menu.Items[1]!).Click += (_, _) => CloseOverlay();
        menu.Opening += (_, _) => ((MenuItem)menu.Items[0]!).Header = IsLocked ? "Unlock overlay" : "Lock overlay";
        return menu;
    }

    private static string Format(double? value) => value.HasValue ? $"{Math.Clamp(value.Value, 0, 100):0}%" : "N/A";
    private static string FormatFps(double? value) => value.HasValue ? $"{value.Value:0}" : "N/A";
    private static string FormatBytes(ulong bytes) => $"{bytes / 1024d / 1024d / 1024d:0.0} GB";

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
