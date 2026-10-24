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
    private bool _sampling;

    public PerformanceWindow()
    {
        _cpu = CreateMetric("CPU", "Overall processor load");
        _memory = CreateMetric("Memory", "Physical memory in use");
        _gpu = CreateMetric("GPU", "Highest active engine");

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
        _timer.Tick += OnTimerTick;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        _timer.Start();
        await RefreshMetricsAsync();
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

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _monitor.Dispose();
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
