using Android.App;
using Android.Content;
using Android.OS;
using NewLife;

namespace StarAgent.Droid;

/// <summary>Android机器信息提供者。为 NewLife.MachineInfo 补充内存与电量</summary>
/// <param name="context">上下文</param>
public class AndroidMachineInfoProvider(Context context) : IMachineInfo
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
            if (context.GetSystemService(Context.ActivityService) is ActivityManager am)
            {
                var memory = new ActivityManager.MemoryInfo();
                am.GetMemoryInfo(memory);
                if (memory.TotalMem > 0) info.Memory = (UInt64)memory.TotalMem;
                if (memory.AvailMem > 0)
                {
                    info.AvailableMemory = (UInt64)memory.AvailMem;
                    info.FreeMemory = (UInt64)memory.AvailMem;
                }
            }
        }
        catch { }

        try
        {
            using var filter = new IntentFilter(Intent.ActionBatteryChanged);
            using var battery = context.RegisterReceiver(null, filter);
            if (battery == null) return;

            var level = battery.GetIntExtra(BatteryManager.ExtraLevel, -1);
            var scale = battery.GetIntExtra(BatteryManager.ExtraScale, -1);
            if (level >= 0 && scale > 0) info.Battery = (Double)level / scale;

            var temperature = battery.GetIntExtra(BatteryManager.ExtraTemperature, Int32.MinValue);
            if (temperature != Int32.MinValue) info.Temperature = temperature / 10.0;
        }
        catch { }
    }
}