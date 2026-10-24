using System.Runtime.InteropServices;
using PresentMonFps;

namespace Orbit.Performance;

public sealed class GameFrameRateMonitor : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private Task? _captureTask;
    private uint _targetProcessId;
    private double? _fps;
    private bool _disposed;

    public GameFrameRateMonitor() { }

    public double? ReadFramesPerSecond()
    {
        lock (_sync)
        {
            if (_disposed)
                return null;

            var processId = GetForegroundProcessId();
            if (processId == 0 || processId == Environment.ProcessId)
            {
                if (_targetProcessId != 0)
                {
                    StopCapture();
                    _targetProcessId = 0;
                    _fps = null;
                }
                return null;
            }

            if (processId != _targetProcessId)
            {
                var previousCapture = StopCapture();
                _targetProcessId = processId;
                _fps = null;
                _cancellation = new CancellationTokenSource();
                var target = processId;
                var cancellation = _cancellation;
                _captureTask = CaptureAsync(target, cancellation, previousCapture);
            }

            return _fps;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            StopCapture();
        }
    }

    private async Task CaptureAsync(uint processId, CancellationTokenSource cancellation, Task? previousCapture)
    {
        try
        {
            if (previousCapture is not null)
                await previousCapture;
            if (cancellation.IsCancellationRequested)
                return;

            var request = new FpsRequest(processId) { PeriodMillisecond = 250 };
            await FpsInspector.StartForeverAsync(request, result =>
            {
                lock (_sync)
                {
                    if (!_disposed && processId == _targetProcessId)
                        _fps = result.Fps;
                }
            }, cancellation.Token);
        }
        catch (Exception)
        {
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_cancellation, cancellation))
                {
                    _cancellation = null;
                    _captureTask = null;
                    _targetProcessId = 0;
                    _fps = null;
                }
            }
            cancellation.Dispose();
        }
    }

    private Task? StopCapture()
    {
        var previousCapture = _captureTask;
        _cancellation?.Cancel();
        _cancellation = null;
        _captureTask = null;
        return previousCapture;
    }

    private static uint GetForegroundProcessId()
    {
        if (!OperatingSystem.IsWindows())
            return 0;

        GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        return processId;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
