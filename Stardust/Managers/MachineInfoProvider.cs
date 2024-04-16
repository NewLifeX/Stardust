﻿#if !NET40
using System.Diagnostics;
using System.Net.NetworkInformation;
using NewLife;
using NewLife.Log;
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

                        // 表示网络的信号质量的百分比值。 WLAN_SIGNAL_QUALITY 的类型为 ULONG。
                        // 此成员包含介于 0 和 100 之间的值。 值为 0 表示实际 RSSI 信号强度为 -100 dbm。 值为 100 表示实际 RSSI 信号强度为 -50 dbm。
                        // 可以使用线性内插计算 1 到 99 之间的 wlanSignalQuality 值的 RSSI 信号强度值。
                        //info["Signal"] = item.wlanSignalQuality / 100d * (-50 - -100) + -100;
                        info["Signal"] = item.wlanSignalQuality;

                        break;
                    }
                }
            }
        }
        else if (Runtime.Linux)
        {
            var signal = 0;
            var file = "/proc/net/wireless";
            if (File.Exists(file))
            {
                var line = File.ReadAllLines(file)?.LastOrDefault();
                if (!line.IsNullOrEmpty())
                {
                    var ss = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (ss.Length > 3)
                    {
                        // Inter-| sta-|   Quality        |   Discarded packets               | Missed | WE
                        // face | tus | link level noise |  nwid  crypt   frag  retry   misc | beacon | 22
                        // wlan0: 0000   70.  -38.  -256        0      0      0    739      0        0

                        // Quality=70/70  Signal level=-38 dBm  Noise level=-256 dBm
                        info["Signal"] = signal = (ss[3]?.TrimEnd('.')).ToInt();
                    }
                }
            }

            if (signal == 0)
            {
                var rs = Execute("iw", "dev wlan0 link", 1_000);
                if (!rs.IsNullOrEmpty())
                {
                    /*
                     * Connected to 24:4b:fe:6d:5c:f8 (on wlan0)
                     * SSID: FeiFan
                     * freq: 2462
                     * RX: 36978860 bytes (198669 packets)
                     * TX: 12425460 bytes (48657 packets)
                     * signal: -31 dBm
                     * tx bitrate: 78.0 MBit/s VHT-MCS 8 VHT-NSS 1
                     * 
                     */
                    var dic = rs.SplitAsDictionary(":", "\n");
                    if (dic.TryGetValue("SSID", out var value))
                        info["SSID"] = value;

                    if (dic.TryGetValue("signal", out value))
                        info["Signal"] = signal = value.TrimEnd("dBm").Trim().ToInt();
                }
            }
        }
    }

    private static String? Execute(String cmd, String? arguments = null, Int32 msWait = 5_000)
    {
        try
        {
#if DEBUG
            if (XTrace.Log.Level <= LogLevel.Debug) XTrace.WriteLine("Execute({0} {1})", cmd, arguments);
#endif

            var psi = new ProcessStartInfo(cmd, arguments ?? String.Empty)
            {
                // UseShellExecute 必须 false，以便于后续重定向输出流
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                //RedirectStandardError = true,
            };
            var process = Process.Start(psi);
            if (process == null) return null;

            if (!process.WaitForExit(msWait))
            {
                process.Kill();
                return null;
            }

            return process.StandardOutput.ReadToEnd();
        }
        catch { return null; }
    }
}
#endif