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
        var args = fileName.IsNullOrEmpty() ? null : Environment.CommandLine.TrimStart(Path.ChangeExtension(fileName, ".dll")).Trim();
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

    private static String? _dns;
    /// <summary>获取网关IP地址和MAC</summary>
    /// <returns></returns>
    public static String? GetDns()
    {
        if (_dns != null) return _dns;
        try
        {
            var dns = NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(e => e.GetIPProperties().DnsAddresses)
                .FirstOrDefault(e => e.AddressFamily == AddressFamily.InterNetwork);
            return _dns = dns?.ToString() ?? String.Empty;
        }
        catch
        {
            return null;
        }
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

            if (Runtime.Windows)
            {
                var arpTable = GetArpTable();
                var entry = arpTable.FirstOrDefault(e => e.IpAddress == ip);
                if (entry != null)
                    return _gateway = $"{ip}/{entry.MacAddress}";
            }

            return _gateway = ip;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>获取ARP表</summary>
    /// <returns></returns>
    public static ArpEntry[] GetArpTable()
    {
        var size = 0;
        GetIpNetTable(IntPtr.Zero, ref size, false);

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetIpNetTable(buffer, ref size, false) == 0)
            {
                var entrySize = Marshal.SizeOf(typeof(MibIpNetRow));
                var count = Marshal.ReadInt32(buffer);
                var currentBuffer = IntPtr.Add(buffer, 4);
                var entries = new ArpEntry[count];

                for (var i = 0; i < count; i++)
                {
                    var row = (MibIpNetRow)Marshal.PtrToStructure(currentBuffer, typeof(MibIpNetRow))!;
                    entries[i] = new ArpEntry
                    {
                        IpAddress = new IPAddress(row.Addr).ToString(),
                        MacAddress = String.Join("-", row.PhysAddr.Take(row.PhysAddrLen).Select(b => b.ToString("X2")))
                    };
                    currentBuffer = IntPtr.Add(currentBuffer, entrySize);
                }

                return entries.Where(e => !e.MacAddress.IsNullOrEmpty() && e.MacAddress != "00-00-00-00-00-00").ToArray();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return [];
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
        public Int32 Type;
    }

    /// <summary>ARP表项</summary>
    [DebuggerDisplay("{IpAddress} {MacAddress}")]
    public class ArpEntry
    {
        /// <summary>IP地址</summary>
        public String IpAddress { get; set; } = null!;

        /// <summary>MAC地址</summary>
        public String MacAddress { get; set; } = null!;
    }

    private static void NetworkChange_NetworkAvailabilityChanged(Object? sender, NetworkAvailabilityEventArgs e)
    {
        _ips = null;
        _gateway = null;
        _dns = null;
    }

    private static void NetworkChange_NetworkAddressChanged(Object? sender, EventArgs e)
    {
        _ips = null;
        _gateway = null;
        _dns = null;
    }
    #endregion
}