using System.Diagnostics;
using System.Runtime.InteropServices;
using PresentMonFps;

namespace Orbit.Performance;

public sealed class GameFrameRateMonitor : IDisposable
{
    private static readonly object SessionLock = new();
    private static GameFrameRateMonitor? _active;
    private static CancellationTokenSource? _cancellation;
    private static double? _currentFps;
    private uint _targetProcessId;
    private bool _disposed;

    public GameFrameRateMonitor()
    {
        if (!OperatingSystem.IsWindows())
            return;

        lock (SessionLock)
            _active = this;
    }

    public double? ReadFramesPerSecond()
    {
        lock (SessionLock)
        {
            if (_disposed)
                return null;

            var processId = GetForegroundProcessId();
            if (processId != 0 && processId != Environment.ProcessId && processId != _targetProcessId)
            {
                StopSession();
                _targetProcessId = processId;
                _currentFps = null;
                _cancellation = new CancellationTokenSource();
                var request = new FpsRequest(processId) { PeriodMillisecond = 500 };
                _ = Task.Run(() => FpsInspector.StartForeverAsync(request, OnFpsUpdated, _cancellation.Token));
            }

            return _currentFps;
        }
    }

    public void Dispose()
    {
        lock (SessionLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (ReferenceEquals(_active, this))
            {
                StopSession();
                _active = null;
            }
        }
    }

    private static void OnFpsUpdated(FpsResult result)
    {
        lock (SessionLock)
            _currentFps = result.Fps;
    }

    private static void StopSession()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        try { FpsInspector.StopTraceSession(); } catch (Exception) { }
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
