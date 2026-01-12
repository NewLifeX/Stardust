using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Stardust.Windows;

/// <summary>PDH 性能计数器辅助类</summary>
/// <remarks>
/// 使用 Windows Performance Data Helper (PDH) API 获取性能计数器数据。
/// 相比 System.Diagnostics.PerformanceCounter，无需额外依赖包，直接通过 P/Invoke 调用。
/// </remarks>
internal static class PdhHelper
{
    #region P/Invoke
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern UInt32 PdhOpenQuery(String? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern UInt32 PdhAddEnglishCounterW(IntPtr hQuery, String szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

    [DllImport("pdh.dll")]
    private static extern UInt32 PdhCollectQueryData(IntPtr hQuery);

    [DllImport("pdh.dll")]
    private static extern UInt32 PdhGetFormattedCounterValue(IntPtr hCounter, UInt32 dwFormat, out UInt32 lpdwType, out PdhCounterValue pValue);

    [DllImport("pdh.dll")]
    private static extern UInt32 PdhCloseQuery(IntPtr hQuery);

    [DllImport("pdh.dll")]
    private static extern UInt32 PdhRemoveCounter(IntPtr hCounter);

    private const UInt32 ERROR_SUCCESS = 0;
    private const UInt32 PDH_FMT_DOUBLE = 0x00000200;

    [StructLayout(LayoutKind.Explicit)]
    private struct PdhCounterValue
    {
        [FieldOffset(0)]
        public UInt32 CStatus;

        [FieldOffset(8)]
        public Double DoubleValue;
    }
    #endregion

    #region 字段
    private static IntPtr _queryHandle;
    private static IntPtr _processorQueueCounter;
    private static IntPtr _diskReadsCounter;
    private static IntPtr _diskWritesCounter;
    private static IntPtr _diskIdleTimeCounter;
    private static Boolean _initialized;
    private static readonly Object _lock = new();
    #endregion

    #region 初始化
    /// <summary>初始化 PDH 查询</summary>
    /// <returns>是否成功</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    private static Boolean Initialize()
    {
        if (_initialized) return _queryHandle != IntPtr.Zero;

        lock (_lock)
        {
            if (_initialized) return _queryHandle != IntPtr.Zero;
            _initialized = true;

            try
            {
                if (PdhOpenQuery(null, IntPtr.Zero, out _queryHandle) != ERROR_SUCCESS)
                {
                    _queryHandle = IntPtr.Zero;
                    return false;
                }

                // 添加计数器，失败不影响其它计数器
                if (PdhAddEnglishCounterW(_queryHandle, @"\System\Processor Queue Length", IntPtr.Zero, out _processorQueueCounter) != ERROR_SUCCESS)
                    _processorQueueCounter = IntPtr.Zero;

                if (PdhAddEnglishCounterW(_queryHandle, @"\PhysicalDisk(_Total)\Disk Reads/sec", IntPtr.Zero, out _diskReadsCounter) != ERROR_SUCCESS)
                    _diskReadsCounter = IntPtr.Zero;

                if (PdhAddEnglishCounterW(_queryHandle, @"\PhysicalDisk(_Total)\Disk Writes/sec", IntPtr.Zero, out _diskWritesCounter) != ERROR_SUCCESS)
                    _diskWritesCounter = IntPtr.Zero;

                if (PdhAddEnglishCounterW(_queryHandle, @"\PhysicalDisk(_Total)\% Idle Time", IntPtr.Zero, out _diskIdleTimeCounter) != ERROR_SUCCESS)
                    _diskIdleTimeCounter = IntPtr.Zero;

                // 首次采样（某些计数器需要两次采样）
                PdhCollectQueryData(_queryHandle);

                return true;
            }
            catch
            {
                _queryHandle = IntPtr.Zero;
                return false;
            }
        }
    }
    #endregion

    #region 方法
    /// <summary>获取处理器队列长度</summary>
    /// <returns>队列长度，失败返回 -1</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public static Double GetProcessorQueueLength()
    {
        if (!Initialize() || _processorQueueCounter == IntPtr.Zero) return -1;

        lock (_lock)
        {
            try
            {
                if (PdhCollectQueryData(_queryHandle) != ERROR_SUCCESS) return -1;
                if (PdhGetFormattedCounterValue(_processorQueueCounter, PDH_FMT_DOUBLE, out _, out var value) != ERROR_SUCCESS) return -1;

                return value.CStatus == 0 ? value.DoubleValue : -1;
            }
            catch { return -1; }
        }
    }

    /// <summary>获取磁盘统计信息</summary>
    /// <returns>IOPS 和活动时间百分比（0-1），失败返回 (-1, -1)</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public static (Int32 IOPS, Double ActiveTime) GetDiskStats()
    {
        if (!Initialize()) return (-1, -1);

        lock (_lock)
        {
            try
            {
                if (PdhCollectQueryData(_queryHandle) != ERROR_SUCCESS) return (-1, -1);

                var reads = GetCounterValue(_diskReadsCounter);
                var writes = GetCounterValue(_diskWritesCounter);
                var idleTime = GetCounterValue(_diskIdleTimeCounter, 100);

                var iops = (Int32)(reads + writes);
                var activeTime = Math.Max(0, Math.Min(100, 100 - idleTime)) / 100.0;

                return (iops, Math.Round(activeTime, 2));
            }
            catch { return (-1, -1); }
        }

        static Double GetCounterValue(IntPtr counter, Double defaultValue = 0)
        {
            if (counter == IntPtr.Zero) return defaultValue;
            if (PdhGetFormattedCounterValue(counter, PDH_FMT_DOUBLE, out _, out var value) != ERROR_SUCCESS) return defaultValue;
            return value.CStatus == 0 ? value.DoubleValue : defaultValue;
        }
    }

    /// <summary>释放资源</summary>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public static void Cleanup()
    {
        lock (_lock)
        {
            if (_queryHandle == IntPtr.Zero) return;

            RemoveCounter(ref _processorQueueCounter);
            RemoveCounter(ref _diskReadsCounter);
            RemoveCounter(ref _diskWritesCounter);
            RemoveCounter(ref _diskIdleTimeCounter);

            PdhCloseQuery(_queryHandle);
            _queryHandle = IntPtr.Zero;
            _initialized = false;
        }

        static void RemoveCounter(ref IntPtr counter)
        {
            if (counter != IntPtr.Zero)
            {
                PdhRemoveCounter(counter);
                counter = IntPtr.Zero;
            }
        }
    }
    #endregion
}
