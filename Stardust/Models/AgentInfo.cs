using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
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

    private static void NetworkChange_NetworkAvailabilityChanged(Object? sender, NetworkAvailabilityEventArgs e) => _ips = null;

    private static void NetworkChange_NetworkAddressChanged(Object? sender, EventArgs e) => _ips = null;
    #endregion
}