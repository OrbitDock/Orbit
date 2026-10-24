using System.Runtime.InteropServices;

namespace Orbit.Performance;

public sealed class SystemPerformanceMonitor : IDisposable
{
    private const uint PdhFormatDouble = 0x00000200;
    private const string GpuCounterPath = @"\GPU Engine(*)\Utilization Percentage";
    private readonly object _sync = new();
    private IntPtr _gpuQuery;
    private IntPtr _gpuCounter;
    private ulong? _previousCpuIdle;
    private ulong? _previousCpuTotal;
    private bool _gpuHasData;
    private bool _disposed;

    public SystemPerformanceMonitor()
    {
        if (OperatingSystem.IsWindows())
            InitializeGpuCounter();
    }

    public bool GpuAvailable
    {
        get
        {
            lock (_sync)
                return _gpuHasData;
        }
    }

    public SystemPerformanceSnapshot Read()
    {
        lock (_sync)
        {
            if (_disposed)
                return new(null, null, null);

            return new(ReadCpuUsage(), ReadMemory(), ReadGpuUsage());
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            if (_gpuQuery != IntPtr.Zero)
                PdhCloseQuery(_gpuQuery);

            _gpuQuery = IntPtr.Zero;
            _gpuCounter = IntPtr.Zero;
            _disposed = true;
        }
    }

    private double? ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            return null;

        var idle = idleTime.Value;
        var total = kernelTime.Value + userTime.Value;
        var previousIdle = _previousCpuIdle;
        var previousTotal = _previousCpuTotal;
        _previousCpuIdle = idle;
        _previousCpuTotal = total;

        if (previousIdle is null || previousTotal is null || total <= previousTotal.Value)
            return null;

        var totalDelta = total - previousTotal.Value;
        var idleDelta = Math.Min(idle - previousIdle.Value, totalDelta);
        return (totalDelta - idleDelta) * 100d / totalDelta;
    }

    private static SystemMemorySnapshot? ReadMemory()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
            return null;

        return new(status.TotalPhysicalMemory, status.TotalPhysicalMemory - status.AvailablePhysicalMemory);
    }

    private double? ReadGpuUsage()
    {
        _gpuHasData = false;
        if (_gpuQuery == IntPtr.Zero || _gpuCounter == IntPtr.Zero || PdhCollectQueryData(_gpuQuery) != 0)
            return null;

        uint bufferSize = 0;
        uint itemCount = 0;
        PdhGetFormattedCounterArrayW(_gpuCounter, PdhFormatDouble, ref bufferSize, out itemCount, IntPtr.Zero);
        if (bufferSize == 0 || itemCount == 0)
            return null;

        var buffer = Marshal.AllocHGlobal(checked((int)bufferSize));
        try
        {
            if (PdhGetFormattedCounterArrayW(_gpuCounter, PdhFormatDouble, ref bufferSize, out itemCount, buffer) != 0)
                return null;

            var itemSize = Marshal.SizeOf<FormattedCounterItem>();
            var maximum = double.NegativeInfinity;
            for (var index = 0; index < itemCount; index++)
            {
                var item = Marshal.PtrToStructure<FormattedCounterItem>(IntPtr.Add(buffer, index * itemSize));
                var value = item.Value.DoubleValue;
                if (item.Value.Status == 0 && double.IsFinite(value))
                    maximum = Math.Max(maximum, value);
            }

            if (!double.IsFinite(maximum))
                return null;

            _gpuHasData = true;
            return Math.Clamp(maximum, 0, 100);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void InitializeGpuCounter()
    {
        if (PdhOpenQueryW(IntPtr.Zero, UIntPtr.Zero, out _gpuQuery) != 0)
            return;

        if (PdhAddCounterW(_gpuQuery, GpuCounterPath, UIntPtr.Zero, out _gpuCounter) != 0)
        {
            PdhCloseQuery(_gpuQuery);
            _gpuQuery = IntPtr.Zero;
            _gpuCounter = IntPtr.Zero;
            return;
        }

        PdhCollectQueryData(_gpuQuery);
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong Value => ToUInt64(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedCounterValue
    {
        public uint Status;
        public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedCounterItem
    {
        public IntPtr Name;
        public FormattedCounterValue Value;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", ExactSpelling = true)]
    private static extern uint PdhOpenQueryW(IntPtr dataSource, UIntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", EntryPoint = "PdhAddCounterW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhAddCounterW(IntPtr query, string counterPath, UIntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW", ExactSpelling = true)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}

public sealed record SystemPerformanceSnapshot(
    double? CpuUsage,
    SystemMemorySnapshot? Memory,
    double? GpuUsage);

public sealed record SystemMemorySnapshot(ulong TotalBytes, ulong UsedBytes);
