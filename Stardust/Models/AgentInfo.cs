using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NewLife;
using NewLife.Reflection;

namespace Stardust.Models;

/// <summary>代理信息</summary>
public class AgentInfo
{
    #region 属性
    /// <summary>进程标识</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程名称</summary>
    public String? ProcessName { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>文件路径</summary>
    public String? FileName { get; set; }

    /// <summary>命令参数</summary>
    public String? Arguments { get; set; }

    /// <summary>本地IP地址</summary>
    public String? IP { get; set; }

    /// <summary>服务端地址</summary>
    public String? Server { get; set; }

    /// <summary>插件服务器</summary>
    public String? PluginServer { get; set; }

    /// <summary>节点编码</summary>
    public String? Code { get; set; }

    /// <summary>应用服务</summary>
    public String[]? Services { get; set; }
    #endregion

    #region 构造
    static AgentInfo()
    {
        //NetworkInterface.GetIsNetworkAvailable();
        NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
    }
    #endregion

    #region 辅助
    /// <summary>
    /// 获取本地信息
    /// </summary>
    /// <returns></returns>
    public static AgentInfo GetLocal(Boolean full)
    {
        var p = Process.GetCurrentProcess();
        var asmx = AssemblyX.Entry;
        var fileName = p.MainModule?.FileName;
        var args = fileName.IsNullOrEmpty() ? null : Environment.CommandLine.TrimPrefix(Path.ChangeExtension(fileName, ".dll")).Trim();
        //var ip = GetIps();

        var inf = new AgentInfo
        {
            Version = asmx?.FileVersion,
            ProcessId = p.Id,
            // 获取本地进程名比较慢，平均200ms，有时候超过500ms
            //ProcessName = p.ProcessName,
            FileName = fileName,
            Arguments = args,
            //IP = ip,
        };

        if (full)
        {
            inf.ProcessName = p.ProcessName;
            inf.IP = GetIps();
        }

        return inf;
    }

    private static String? _ips;
    /// <summary>
    /// 获取本地IP地址
    /// </summary>
    /// <returns></returns>
    public static String? GetIps()
    {
        try
        {
            var ips = _ips.IsNullOrEmpty() ? NetHelper.GetIPs().ToArray() : NetHelper.GetIPsWithCache();

            return _ips = ips
                .Where(ip => ip.IsIPv4() && !IPAddress.IsLoopback(ip) && ip.GetAddressBytes()[0] != 169)
                .Select(e => e + "")
                .Distinct()
                .Join();
        }
        catch
        {
            return null;
        }
    }

    private static String? _dnsList;
    /// <summary>获取DNS服务器地址列表。逗号分隔多个地址</summary>
    /// <remarks>
    /// Linux 的 systemd-resolved 会把 /etc/resolv.conf 指向 127.0.0.53 本地存根，直接读取只能拿到存根地址。
    /// 这里优先读取 /run/systemd/resolve/resolv.conf，拿到网关与公共DNS等真实上游；结果为空或仍是本地地址时不缓存，等网络就绪后重新获取。
    /// </remarks>
    /// <returns></returns>
    public static String? GetDnsList()
    {
        if (_dnsList != null) return _dnsList;

        var dns = GetDnsCore();

        // 本地存根地址和空值不做缓存，等待网络就绪后重新获取
        var isLocal = false;
        if (!dns.IsNullOrEmpty())
        {
            foreach (var item in dns.Split(','))
            {
                if (IPAddress.TryParse(item, out var ip) && ip.IsLocal()) { isLocal = true; break; }
            }
        }
        if (!dns.IsNullOrEmpty() && !isLocal) _dnsList = dns;

        return dns;
    }

    /// <summary>获取DNS服务器地址。单个地址，多个时取第一个</summary>
    /// <returns></returns>
    public static String? GetDns()
    {
        var dns = GetDnsList();
        if (dns.IsNullOrEmpty()) return dns;

        var p = dns.IndexOf(',');
        return p > 0 ? dns[..p] : dns;
    }

    private static String? GetDnsCore()
    {
        try
        {
            // systemd-resolved 环境中，/etc/resolv.conf 只是本地存根，真实上游在 /run/systemd/resolve/resolv.conf
            if (Runtime.Linux)
            {
                var file = "/run/systemd/resolve/resolv.conf";
                if (File.Exists(file))
                {
                    var list = ParseDnsServers(File.ReadAllText(file));
                    if (list.Length > 0) return list.Join(",");
                }
            }

            var dns = NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(e => e.GetIPProperties().DnsAddresses)
                .Where(e => e.AddressFamily == AddressFamily.InterNetwork)
                .Select(e => e + "")
                .Distinct()
                .ToArray();
            if (dns.Length > 0) return dns.Join(",");

            // 如果没有DNS地址，在Linux下直接读取/etc/resolv.conf文件
            if (Runtime.Linux)
            {
                var file = "/etc/resolv.conf";
                if (File.Exists(file))
                {
                    var list = ParseDnsServers(File.ReadAllText(file));
                    if (list.Length > 0) return list.Join(",");
                }
            }

            return String.Empty;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>解析 resolv.conf 内容中的DNS服务器地址，剔除本地存根与重复项</summary>
    /// <param name="content">文件内容</param>
    /// <returns>地址数组</returns>
    public static String[] ParseDnsServers(String content)
    {
        var list = new List<String>();
        foreach (var item in content.Split('\n'))
        {
            var line = item.Trim();
            if (line.IsNullOrEmpty() || !line.StartsWith("nameserver")) continue;

            line = line["nameserver".Length..].Trim();
            var p = line.IndexOf('#');
            if (p > 0) line = line[..p].Trim(); // 去掉#结尾的注释

            // 本地存根地址（如127.0.0.53）不是真实上游，不参与上报
            if (line.IsNullOrEmpty()) continue;
            if (IPAddress.TryParse(line, out var ip) && ip.IsLocal()) continue;

            if (!list.Contains(line)) list.Add(line);
        }

        return list.ToArray();
    }

    private static String? _gateway;
    /// <summary>获取网关IP地址和MAC</summary>
    /// <returns></returns>
    public static String? GetGateway()
    {
        if (_gateway != null) return _gateway;
        try
        {
            var gateway = NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(e => e.GetIPProperties().GatewayAddresses)
                .FirstOrDefault(e => e.Address.AddressFamily == AddressFamily.InterNetwork);
            var ip = gateway?.Address.ToString();
            if (ip.IsNullOrEmpty()) return _gateway = String.Empty;

            var arps = GetArpTable();
            if (arps.TryGetValue(ip, out var mac))
                return _gateway = $"{ip}/{mac}";

            return _gateway = ip;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>获取ARP表</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetArpTable()
    {
        var dic = new Dictionary<String, String>();

        if (Runtime.Windows)
        {
            var size = 0;
            GetIpNetTable(IntPtr.Zero, ref size, false);

            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetIpNetTable(buffer, ref size, false) == 0)
                {
                    var entrySize = MarshalHelper.SizeOf<MibIpNetRow>();
                    var count = Marshal.ReadInt32(buffer);
                    var currentBuffer = IntPtr.Add(buffer, 4);

                    for (var i = 0; i < count; i++)
                    {
                        var row = MarshalHelper.PtrToStructure<MibIpNetRow>(currentBuffer);
                        var ip = new IPAddress(row.Addr).ToString();
                        var mac = String.Join("-", row.PhysAddr.Take(row.PhysAddrLen).Select(b => b.ToString("X2")));

                        if (!ip.IsNullOrEmpty() && !mac.IsNullOrEmpty() &&
                            row.Type is MibIpNetType.DYNAMIC or MibIpNetType.STATIC)
                            dic[ip] = mac;

                        currentBuffer = IntPtr.Add(currentBuffer, entrySize);
                    }

                    return dic;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        else if (Runtime.Linux)
        {
            // Linux下读取/proc/net/arp文件获取ARP表
            var rs = "";
            var f = "/proc/net/arp";
            if (File.Exists(f)) rs = File.ReadAllText(f);

            if (rs.IsNullOrEmpty()) rs = "arp".Execute("-n", 5_000);
            if (!rs.IsNullOrEmpty())
            {
                foreach (var item in rs.Split(['\n'], StringSplitOptions.RemoveEmptyEntries).Skip(1))
                {
                    var arr = item.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    if (arr.Length >= 2)
                    {
                        var ip = arr[0];
                        var mac = arr.Skip(1).FirstOrDefault(e => e.Contains(':'))?.Replace(':', '-').ToUpper();
                        if (!mac.IsNullOrEmpty()) dic[ip] = mac;
                    }
                }
            }
        }

        return dic;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern Int32 GetIpNetTable(IntPtr pIpNetTable, ref Int32 pdwSize, Boolean bOrder);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibIpNetRow
    {
        [MarshalAs(UnmanagedType.U4)]
        public Int32 Index;
        [MarshalAs(UnmanagedType.U4)]
        public Int32 PhysAddrLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public Byte[] PhysAddr;
        [MarshalAs(UnmanagedType.U4)]
        public UInt32 Addr;
        [MarshalAs(UnmanagedType.U4)]
        public MibIpNetType Type;
    }

    enum MibIpNetType : Int32
    {
        OTHER = 1,
        INVALID = 2,
        DYNAMIC = 3,
        STATIC = 4,
        LOCAL = 5
    }

    private static void NetworkChange_NetworkAvailabilityChanged(Object? sender, NetworkAvailabilityEventArgs e)
    {
        _ips = null;
        _gateway = null;
        _dnsList = null;
    }

    private static void NetworkChange_NetworkAddressChanged(Object? sender, EventArgs e)
    {
        _ips = null;
        _gateway = null;
        _dnsList = null;
    }
    #endregion
}