using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NewLife;
using NewLife.Log;

namespace AgentExpansion;

/// <summary>局域网扫描地址解析。从本机网卡或配置的Networks解析出待探测的局域网地址</summary>
/// <remarks>只扫描 RFC1918 私有网段，链路本地、回环、多播、保留地址以及公网地址一律排除</remarks>
internal static class LanScan
{
    /// <summary>判断是否局域网地址</summary>
    /// <remarks>
    /// 仅放行 10.0.0.0/8、172.16.0.0/12、192.168.0.0/16。
    /// 其余地址一律排除，包括：链路本地 169.254.0.0/16（APIPA，掩码多为 /16，展开后地址极多）、
    /// 回环 127.0.0.0/8、本网络 0.0.0.0/8、运营商级 NAT 100.64.0.0/10、
    /// 多播 224.0.0.0/4、保留 240.0.0.0/4、以及 192.0.2.0/24 等文档测试网段和任意公网地址。
    /// </remarks>
    /// <param name="address">待判断的 IPv4 地址</param>
    /// <returns>是否属于局域网网段</returns>
    public static Boolean IsLanAddress(IPAddress? address)
    {
        if (address == null) return false;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;

        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4) return false;

        // 10.0.0.0/8
        if (bytes[0] == 10) return true;

        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168) return true;

        return false;
    }

    /// <summary>解析配置的目标网段。支持 192.168.1.0/24、192.168.1.* 以及单个地址</summary>
    /// <param name="text">网段文本，逗号分号空白分隔</param>
    /// <param name="log">日志。用于提示被忽略的非局域网网段</param>
    /// <returns>局域网网段集合</returns>
    public static List<NetworkRange> ParseNetworks(String? text, ILog? log)
    {
        var list = new List<NetworkRange>();
        if (text.IsNullOrEmpty()) return list;

        var parts = text.Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (TryParseCidr(part, out var range) ||
                TryParseWildcard(part, out range) ||
                TryParseSingle(part, out range))
            {
                // 与本机网卡同一套过滤，避免误配成 169.254.0.0/16、0.0.0.0/0 等非局域网网段后全网段扫描
                if (!IsLanAddress(range.Start))
                {
                    log?.Warn("目标网段 {0} 不是局域网地址，已忽略", part);
                    continue;
                }

                list.Add(range);
            }
        }

        return list;
    }

    /// <summary>获取本机所有局域网网段</summary>
    /// <returns>本机网卡所在的局域网网段</returns>
    public static List<NetworkRange> GetLocalRanges()
    {
        var list = new List<NetworkRange>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var props = nic.GetIPProperties();
            if (props == null) continue;

            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (unicast.IPv4Mask == null) continue;
                //if (unicast.PrefixOrigin == PrefixOrigin.WellKnown) continue;
                // 只扫描局域网网段。无线直连、蓝牙、虚拟网卡等常常回落 169.254.0.0/16，掩码还是 /16，
                // 一旦参与扫描会展开成六万多个地址，既无意义又会刷爆日志
                if (!IsLanAddress(unicast.Address)) continue;

                var prefix = GetPrefixLength(unicast.IPv4Mask);
                if (prefix <= 0) continue;

                list.Add(NetworkRange.FromCidr(unicast.Address, prefix));
            }
        }

        return list;
    }

    /// <summary>网段去重。多张网卡可能接入同一网段</summary>
    /// <param name="ranges">原始网段集合</param>
    /// <returns>去重后的网段集合</returns>
    public static List<NetworkRange> Distinct(List<NetworkRange> ranges)
    {
        var list = new List<NetworkRange>();
        foreach (var range in ranges)
        {
            if (!list.Any(e => e.Start.Equals(range.Start) && e.End.Equals(range.End))) list.Add(range);
        }

        return list;
    }

    /// <summary>枚举待探测地址</summary>
    /// <param name="ranges">目标网段</param>
    /// <param name="maxHosts">最大主机数。0或负数表示不限制</param>
    /// <param name="log">日志。超过上限时给出提示</param>
    /// <returns>待探测地址</returns>
    public static IEnumerable<IPAddress> GetTargets(List<NetworkRange> ranges, Int32 maxHosts, ILog? log)
    {
        var count = 0;
        foreach (var range in ranges)
        {
            foreach (var address in range.GetAddresses())
            {
                yield return address;
                count++;
                if (maxHosts > 0 && count >= maxHosts)
                {
                    log?.Warn("目标地址超过上限 {0}，本轮仅扫描前 {0} 个地址，请调整MaxHosts或收窄Networks", maxHosts);
                    yield break;
                }
            }
        }
    }

    private static Int32 GetPrefixLength(IPAddress mask)
    {
        var bytes = mask.GetAddressBytes();
        var count = 0;
        foreach (var item in bytes)
        {
            var value = item;
            for (var i = 0; i < 8; i++)
            {
                if ((value & 0x80) == 0x80) count++;
                value <<= 1;
            }
        }

        return count;
    }

    private static Boolean TryParseSingle(String text, out NetworkRange range)
    {
        range = default;
        if (!IPAddress.TryParse(text, out var address)) return false;

        range = NetworkRange.FromSingle(address);
        return true;
    }

    private static Boolean TryParseCidr(String text, out NetworkRange range)
    {
        range = default;
        var p = text.IndexOf('/');
        if (p <= 0) return false;

        if (!IPAddress.TryParse(text[..p], out var address)) return false;
        if (!Int32.TryParse(text[(p + 1)..], out var prefix)) return false;
        if (prefix < 0 || prefix > 32) return false;

        range = NetworkRange.FromCidr(address, prefix);
        return true;
    }

    private static Boolean TryParseWildcard(String text, out NetworkRange range)
    {
        range = default;
        if (!text.Contains('*')) return false;

        var parts = text.Split('.');
        if (parts.Length != 4) return false;

        var startBytes = new Byte[4];
        var endBytes = new Byte[4];
        var stars = 0;

        for (var i = 0; i < 4; i++)
        {
            if (parts[i] == "*")
            {
                startBytes[i] = 0;
                endBytes[i] = 255;
                stars++;
            }
            else if (Byte.TryParse(parts[i], out var value))
            {
                startBytes[i] = value;
                endBytes[i] = value;
            }
            else
            {
                return false;
            }
        }

        var start = new IPAddress(startBytes);

        // 通配符全部位于末尾时等价于 CIDR，交由 FromCidr 处理，顺带排除网络地址与广播地址
        var trailing = 0;
        for (var i = 3; i >= 0 && parts[i] == "*"; i--) trailing++;

        range = stars == trailing && trailing < 4
            ? NetworkRange.FromCidr(start, (4 - trailing) * 8)
            : new NetworkRange(start, new IPAddress(endBytes));

        return true;
    }
}

/// <summary>IP地址区间</summary>
internal readonly struct NetworkRange
{
    /// <summary>实例化</summary>
    /// <param name="start">起始地址</param>
    /// <param name="end">结束地址</param>
    public NetworkRange(IPAddress start, IPAddress end)
    {
        Start = start;
        End = end;
    }

    /// <summary>起始地址</summary>
    public IPAddress Start { get; }

    /// <summary>结束地址</summary>
    public IPAddress End { get; }

    /// <summary>枚举区间内所有地址</summary>
    /// <returns>区间内地址</returns>
    public IEnumerable<IPAddress> GetAddresses()
    {
        var start = ToUInt32(Start);
        var end = ToUInt32(End);
        if (end < start) yield break;

        for (var i = start; i <= end; i++)
        {
            yield return ToIPAddress(i);

            if (i == UInt32.MaxValue) break;
        }
    }

    /// <summary>从单个地址构造区间</summary>
    /// <param name="address">地址</param>
    /// <returns>区间</returns>
    public static NetworkRange FromSingle(IPAddress address) => new(address, address);

    /// <summary>从CIDR构造区间，自动排除网络地址与广播地址</summary>
    /// <param name="address">任意地址</param>
    /// <param name="prefix">前缀长度</param>
    /// <returns>区间</returns>
    public static NetworkRange FromCidr(IPAddress address, Int32 prefix)
    {
        var ip = ToUInt32(address);
        var mask = prefix == 0 ? 0U : UInt32.MaxValue << (32 - prefix);
        var network = ip & mask;
        var broadcast = network | ~mask;

        var start = prefix >= 31 ? network : network + 1;
        var end = prefix >= 31 ? broadcast : broadcast - 1;

        return new NetworkRange(ToIPAddress(start), ToIPAddress(end));
    }

    private static UInt32 ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static IPAddress ToIPAddress(UInt32 value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return new IPAddress(bytes);
    }
}
