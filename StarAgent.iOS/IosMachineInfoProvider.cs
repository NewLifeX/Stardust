using Foundation;
using NewLife;
using UIKit;

namespace StarAgent.Ios;

/// <summary>iOS机器信息提供者。为 NewLife.MachineInfo 补充内存与电量</summary>
public class IosMachineInfoProvider : IMachineInfo
{
    /// <summary>初始化时执行一次</summary>
    /// <param name="info">机器信息</param>
    public void Init(MachineInfo info) { }

    /// <summary>刷新动态数据</summary>
    /// <param name="info">机器信息</param>
    public void Refresh(MachineInfo info)
    {
        try
        {
            var total = (UInt64)NSProcessInfo.ProcessInfo.PhysicalMemory;
            if (total > 0) info.Memory = total;

            var used = (UInt64)System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            if (used < total)
            {
                info.AvailableMemory = total - used;
                info.FreeMemory = info.AvailableMemory;
            }
        }
        catch { }

        try
        {
            var level = UIDevice.CurrentDevice.BatteryLevel;
            if (level >= 0) info.Battery = Math.Round(level * 100, 1);
        }
        catch { }
    }
}