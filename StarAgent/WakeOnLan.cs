using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NewLife;
using NewLife.Log;

namespace StarAgent;

/// <summary>局域网唤醒（Wake-on-LAN）魔术包构造与发送</summary>
/// <remarks>
/// 魔术包为 6 个 0xFF 加 16 次目标网卡 MAC，共 102 字节，通过 UDP 广播发往局域网。
/// 广播只在二层广播域内生效，不能跨网段或跨 VLAN；目标网卡与主板还需开启 WOL 才能被唤醒。
/// </remarks>
public static class WakeOnLan
{
    private static readonly Int32[] _Ports = [9, 7];

    /// <summary>构造魔术包</summary>
    /// <param name="mac">目标网卡MAC，6字节</param>
    /// <returns>102字节魔术包</returns>
    public static Byte[] BuildPacket(Byte[] mac)
    {
        if (mac == null) throw new ArgumentNullException(nameof(mac));
        if (mac.Length != 6) throw new ArgumentException("The MAC address must be 6 bytes.", nameof(mac));

        var buf = new Byte[6 + 16 * 6];
        for (var i = 0; i < 6; i++) buf[i] = 0xFF;
        for (var i = 0; i < 16; i++) Array.Copy(mac, 0, buf, 6 + i * 6, 6);

        return buf;
    }

    /// <summary>解析MAC列表</summary>
    /// <param name="text">MAC文本。支持 AA-BB-CC-DD-EE-FF、aa:bb:cc:dd:ee:ff、aabb.ccdd.eeff，多个用逗号、分号或空白分隔</param>
    /// <returns>MAC列表，非法项被忽略，重复项去重</returns>
    public static IList<Byte[]> ParseMacs(String? text)
    {
        var list = new List<Byte[]>();
        if (text.IsNullOrEmpty()) return list;

        var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var hex = item.Replace("-", null).Replace(":", null).Replace(".", null).Trim();
            if (hex.Length != 12) continue;

            var mac = new Byte[6];
            var valid = true;
            for (var i = 0; i < 6; i++)
            {
                if (!Byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out mac[i]))
                {
                    valid = false;
                    break;
                }
            }
            if (!valid) continue;

            if (set.Add(mac.ToHex("-"))) list.Add(mac);
        }

        return list;
    }

    /// <summary>向本机所有局域网网卡广播发送魔术包</summary>
    /// <param name="macs">目标MAC列表</param>
    /// <param name="log">日志。单张网卡发送失败只记录日志，不影响其它网卡</param>
    /// <returns>发送的报文数量</returns>
    public static Int32 Send(IList<Byte[]> macs, ILog? log = null)
    {
        if (macs == null || macs.Count == 0) return 0;

        var count = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var props = ni.GetIPProperties();
            if (props == null) continue;

            foreach (var ua in props.UnicastAddresses)
            {
                var address = ua.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(address)) continue;

                // 自动专用地址（169.254.0.0/16）通常来自未连网网卡，广播出去没有意义
                var bytes = address.GetAddressBytes();
                if (bytes.Length != 4 || bytes[0] == 169) continue;

                var mask = ua.IPv4Mask;
                if (mask == null) continue;
                var mb = mask.GetAddressBytes();
                if (mb.Length != 4) continue;

                var bc = new Byte[4];
                for (var i = 0; i < 4; i++) bc[i] = (Byte)(bytes[i] | (~mb[i] & 0xFF));
                var broadcast = new IPAddress(bc);

                try
                {
                    // 绑定本网卡地址，确保报文从该网卡发出；多网卡机器需要逐张网卡发送
                    using var udp = new UdpClient(new IPEndPoint(address, 0)) { EnableBroadcast = true };
                    foreach (var mac in macs)
                    {
                        var packet = BuildPacket(mac);
                        foreach (var port in _Ports)
                        {
                            count += SendPacket(udp, packet, new IPEndPoint(broadcast, port), log, address);
                            count += SendPacket(udp, packet, new IPEndPoint(IPAddress.Broadcast, port), log, address);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log?.Debug("Wake-on-LAN send via {0} failed: {1}", address, ex.Message);
                }
            }
        }

        return count;
    }

    private static Int32 SendPacket(UdpClient udp, Byte[] packet, IPEndPoint endpoint, ILog? log, IPAddress local)
    {
        try
        {
            udp.Send(packet, packet.Length, endpoint);
            return 1;
        }
        catch (Exception ex)
        {
            // 个别系统不允许从指定地址发定向广播，单次失败不影响其它目标地址
            log?.Debug("Wake-on-LAN send to {0} via {1} failed: {2}", endpoint, local, ex.Message);
            return 0;
        }
    }
}
