#if !NET40
using System.Net.NetworkInformation;
using NewLife;
using Stardust.Windows;

namespace Stardust.Managers;

/// <summary>机器信息提供者，增强机器信息获取能力</summary>
public class MachineInfoProvider : IMachineInfo
{
    public void Init(MachineInfo info)
    {
    }

    public void Refresh(MachineInfo info)
    {
        if (Runtime.Windows)
        {
            // 是否使用WiFi
            if (NetworkInterface.GetAllNetworkInterfaces().Any(e => e.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && e.OperationalStatus == OperationalStatus.Up))
            {
                // 从WiFi获取信号强度
                foreach (var item in NativeWifi.GetConnectedNetworkSsids())
                {
                    if (item.wlanSignalQuality > 0)
                    {
                        info["Ssid"] = item.dot11Ssid;
                        info["Signal"] = item.wlanSignalQuality;
                        break;
                    }
                }
            }
        }
    }
}
#endif