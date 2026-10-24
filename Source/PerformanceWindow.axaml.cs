using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orbit.Controls;
using Orbit.Performance;

namespace Orbit;

public sealed class PerformanceWindow : SettingsWindowBase
{
    private readonly SystemPerformanceMonitor _monitor = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly MetricView _cpu;
    private readonly MetricView _memory;
    private readonly MetricView _gpu;
    private readonly CheckBox _overlayToggle;
    private readonly CheckBox _overlayLockToggle;
    private readonly PerformanceOverlayWindow _overlay = PerformanceOverlayWindow.Shared;
    private bool _sampling;

    public PerformanceWindow()
    {
        _cpu = CreateMetric("CPU", "Overall processor load");
        _memory = CreateMetric("Memory", "Physical memory in use");
        _gpu = CreateMetric("GPU", "Highest active engine");
        _overlayToggle = new CheckBox
        {
            Content = "In-Game Performance Overlay",
            IsChecked = PerformanceOverlayWindow.OverlayEnabled,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        _overlayLockToggle = new CheckBox
        {
            Content = "Lock overlay position",
            IsChecked = PerformanceOverlayWindow.IsLocked,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(24, 8, 0, 0),
            IsEnabled = PerformanceOverlayWindow.OverlayEnabled
        };

        var metrics = new StackPanel
        {
            Spacing = 0,
            Children =
            {
                _cpu.Root,
                CreateDivider(),
                _memory.Root,
                CreateDivider(),
                _gpu.Root,
                CreateDivider(),
                _overlayToggle,
                _overlayLockToggle,
                new TextBlock
                {
                    Text = "System-wide readings refresh every second.",
                    FontSize = 11,
                    Foreground = OrbitTheme.Muted,
                    Margin = new Avalonia.Thickness(0, 10, 0, 0)
                }
            }
        };

        SetContent("PC performance", metrics);
        _overlayToggle.IsCheckedChanged += OnOverlayToggleChanged;
        _overlayLockToggle.IsCheckedChanged += (_, _) => PerformanceOverlayWindow.IsLocked = _overlayLockToggle.IsChecked == true;
        _overlay.OverlayClosed += OnOverlayClosed;
        _timer.Tick += OnTimerTick;
        Opened += OnOpened;
        Closed += (_, _) => _timer.Stop();
        Opened += (_, _) =>
        {
            if (PerformanceOverlayWindow.OverlayEnabled)
                _overlay.Show();
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _timer.Start();
        _ = RefreshMetricsAsync();
    }

    private async void OnTimerTick(object? sender, EventArgs e) => await RefreshMetricsAsync();

    private async Task RefreshMetricsAsync()
    {
        if (_sampling)
            return;

        _sampling = true;
        try
        {
            var snapshot = await Task.Run(_monitor.Read);
            if (!IsVisible)
                return;

            UpdateMetric(_cpu, snapshot.CpuUsage, "Overall processor load");
            if (snapshot.Memory is { TotalBytes: > 0 } memory)
            {
                var usage = memory.UsedBytes * 100d / memory.TotalBytes;
                UpdateMetric(_memory, usage, $"{FormatBytes(memory.UsedBytes)} used of {FormatBytes(memory.TotalBytes)}");
            }
            else
            {
                UpdateMetric(_memory, null, "Memory readings are unavailable");
            }

            UpdateMetric(_gpu, snapshot.GpuUsage, _monitor.GpuAvailable ? "Highest active engine" : "GPU counters are unavailable");
        }
        catch (Exception)
        {
            UpdateMetric(_cpu, null, "CPU readings are unavailable");
            UpdateMetric(_memory, null, "Memory readings are unavailable");
            UpdateMetric(_gpu, null, "GPU readings are unavailable");
        }
        finally
        {
            _sampling = false;
        }
    }

    private void OnOverlayToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PerformanceOverlayWindow.OverlayEnabled = _overlayToggle.IsChecked == true;
        _overlayLockToggle.IsEnabled = PerformanceOverlayWindow.OverlayEnabled;
        if (PerformanceOverlayWindow.OverlayEnabled)
        {
            _overlay.Show();
            _overlay.Activate();
        }
        else
        {
            _overlay.Hide();
        }
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        _overlayToggle.IsChecked = false;
        _overlayLockToggle.IsEnabled = false;
    }

    private static MetricView CreateMetric(string title, string detail)
    {
        var value = new TextBlock
        {
            Text = "N/A",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var description = new TextBlock
        {
            Text = detail,
            FontSize = 11,
            Foreground = OrbitTheme.Muted
        };
        var meter = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 4,
            Background = OrbitTheme.Line,
            Foreground = OrbitTheme.Accent,
            Margin = new Avalonia.Thickness(0, 9, 0, 0)
        };
        var labels = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeight.Medium },
                description
            }
        };
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Children = { labels, value, meter }
        };
        Grid.SetColumn(value, 1);
        Grid.SetRow(meter, 1);
        Grid.SetColumnSpan(meter, 2);
        return new(row, value, description, meter);
    }

    private static Border CreateDivider() => new()
    {
        Height = 1,
        Background = OrbitTheme.Line,
        Margin = new Avalonia.Thickness(0, 9)
    };

    private static void UpdateMetric(MetricView metric, double? usage, string detail)
    {
        metric.Value.Text = usage.HasValue ? $"{Math.Clamp(usage.Value, 0, 100):0}%" : "N/A";
        metric.Description.Text = detail;
        metric.Meter.Value = usage.HasValue ? Math.Clamp(usage.Value, 0, 100) : 0;
    }

    private static string FormatBytes(ulong bytes) => $"{bytes / 1024d / 1024d / 1024d:0.0} GB";

    private sealed record MetricView(Grid Root, TextBlock Value, TextBlock Description, ProgressBar Meter);
}
