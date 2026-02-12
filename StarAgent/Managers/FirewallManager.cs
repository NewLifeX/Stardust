using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;

namespace StarAgent.Managers;

/// <summary>防火墙管理器</summary>
/// <remarks>
/// 支持自动检测和开放防火墙端口
/// - Windows: netsh advfirewall
/// - Linux: firewalld / ufw / iptables
/// </remarks>
internal class FirewallManager
{
    #region 属性
    /// <summary>防火墙类型</summary>
    public FirewallType Type { get; private set; }

    /// <summary>是否可用</summary>
    public Boolean Available { get; private set; }
    #endregion

    #region 构造
    public FirewallManager()
    {
        DetectFirewall();
    }
    #endregion

    #region 检测防火墙
    /// <summary>检测系统防火墙类型</summary>
    private void DetectFirewall()
    {
        if (Runtime.Windows)
        {
            // Windows Firewall
            try
            {
                var output = "netsh".Execute("advfirewall show currentprofile", 5_000);
                if (!output.IsNullOrEmpty() && !output.Contains("请求的操作需要提升") && !output.Contains("require elevation"))
                {
                    Type = FirewallType.WindowsFirewall;
                    Available = true;
                    return;
                }
            }
            catch { }

            Type = FirewallType.WindowsFirewall;
            Available = false;
        }
        else
        {
            // firewalld (CentOS/RHEL/Fedora)
            try
            {
                var output = "firewall-cmd".Execute("--state", 5_000);
                if (!output.IsNullOrEmpty() && output.Contains("running"))
                {
                    Type = FirewallType.Firewalld;
                    Available = true;
                    return;
                }
            }
            catch { }

            // ufw (Ubuntu/Debian)
            try
            {
                var output = "ufw".Execute("status", 5_000);
                if (!output.IsNullOrEmpty() && (output.Contains("Status: active") || output.Contains("状态：激活")))
                {
                    Type = FirewallType.Ufw;
                    Available = true;
                    return;
                }
            }
            catch { }

            // iptables (通用)
            try
            {
                var output = "iptables".Execute("-L -n", 5_000);
                if (!output.IsNullOrEmpty())
                {
                    Type = FirewallType.Iptables;
                    Available = true;
                    return;
                }
            }
            catch { }

            Type = FirewallType.None;
            Available = false;
        }
    }
    #endregion

    #region 开放端口
    /// <summary>开放TCP端口</summary>
    /// <param name="port">端口号</param>
    /// <param name="ruleName">规则名称</param>
    /// <returns>是否成功</returns>
    public Boolean OpenPort(Int32 port, String ruleName)
    {
        if (!Available) return false;
        if (port <= 0 || port > 65535) return false;

        try
        {
            switch (Type)
            {
                case FirewallType.WindowsFirewall:
                    return OpenPortWindows(port, ruleName);
                case FirewallType.Firewalld:
                    return OpenPortFirewalld(port);
                case FirewallType.Ufw:
                    return OpenPortUfw(port);
                case FirewallType.Iptables:
                    return OpenPortIptables(port);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            WriteLog("开放端口 {0} 失败: {1}", port, ex.Message);
            return false;
        }
    }

    /// <summary>Windows平台开放端口</summary>
    private Boolean OpenPortWindows(Int32 port, String ruleName)
    {
        if (ruleName.IsNullOrEmpty()) ruleName = $"StardustApp-{port}";

        // 检查规则是否已存在
        var checkCmd = $"advfirewall firewall show rule name=\"{ruleName}\"";
        var output = "netsh".Execute(checkCmd, 5_000);
        if (!output.IsNullOrEmpty() && output.Contains(ruleName))
        {
            WriteLog("防火墙规则 {0} 已存在", ruleName);
            return true;
        }

        // 添加防火墙规则
        var cmd = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}";
        output = "netsh".Execute(cmd, 5_000);

        var success = !output.IsNullOrEmpty() && (output.Contains("确定") || output.Contains("Ok"));
        if (success)
            WriteLog("已开放端口 {0} (规则: {1})", port, ruleName);
        else
            WriteLog("开放端口 {0} 失败: {1}", port, output);

        return success;
    }

    /// <summary>firewalld开放端口</summary>
    private Boolean OpenPortFirewalld(Int32 port)
    {
        // 检查端口是否已开放
        var checkCmd = $"--query-port={port}/tcp";
        var output = "firewall-cmd".Execute(checkCmd, 5_000);
        if (!output.IsNullOrEmpty() && output.Contains("yes"))
        {
            WriteLog("端口 {0} 已开放", port);
            return true;
        }

        // 永久开放端口并重载
        var cmd = $"--permanent --add-port={port}/tcp";
        output = "firewall-cmd".Execute(cmd, 5_000);
        if (output.IsNullOrEmpty() || !output.Contains("success"))
        {
            WriteLog("开放端口 {0} 失败: {1}", port, output);
            return false;
        }

        // 重载防火墙规则
        output = "firewall-cmd".Execute("--reload", 5_000);
        var success = !output.IsNullOrEmpty() && output.Contains("success");
        if (success)
            WriteLog("已开放端口 {0} (firewalld)", port);

        return success;
    }

    /// <summary>ufw开放端口</summary>
    private Boolean OpenPortUfw(Int32 port)
    {
        // ufw allow 会自动检查是否已存在，不需要手动检查
        var cmd = $"allow {port}/tcp";
        var output = "ufw".Execute(cmd, 5_000);

        var success = !output.IsNullOrEmpty() && 
                      (output.Contains("Rule added") || output.Contains("Skipping") || 
                       output.Contains("规则已添加") || output.Contains("跳过"));
        
        if (success)
            WriteLog("已开放端口 {0} (ufw)", port);
        else
            WriteLog("开放端口 {0} 失败: {1}", port, output);

        return success;
    }

    /// <summary>iptables开放端口</summary>
    private Boolean OpenPortIptables(Int32 port)
    {
        // 检查规则是否已存在
        var checkCmd = $"-C INPUT -p tcp --dport {port} -j ACCEPT";
        var output = "iptables".Execute(checkCmd, 5_000);
        if (String.IsNullOrEmpty(output))
        {
            WriteLog("端口 {0} 已开放", port);
            return true;
        }

        // 添加规则
        var cmd = $"-A INPUT -p tcp --dport {port} -j ACCEPT";
        output = "iptables".Execute(cmd, 5_000);

        var success = String.IsNullOrEmpty(output) || !output.Contains("error");
        if (success)
        {
            WriteLog("已开放端口 {0} (iptables)", port);

            // 尝试保存规则（可选，因为可能需要特定的保存命令）
            try
            {
                if (File.Exists("/etc/redhat-release"))
                    "service".Execute("iptables save", 5_000);
                else if (File.Exists("/etc/debian_version"))
                    "iptables-save".Execute("> /etc/iptables/rules.v4", 5_000);
            }
            catch { }
        }
        else
        {
            WriteLog("开放端口 {0} 失败: {1}", port, output);
        }

        return success;
    }
    #endregion

    #region 端口提取
    /// <summary>从工作目录检测需要开放的端口</summary>
    /// <param name="workDir">工作目录</param>
    /// <returns>端口列表</returns>
    public static IEnumerable<Int32> DetectPorts(String workDir)
    {
        var ports = new HashSet<Int32>();

        if (workDir.IsNullOrEmpty() || !Directory.Exists(workDir)) return ports;

        // 1. 从Nginx配置文件提取端口
        var nginxPorts = ExtractPortsFromNginx(workDir);
        foreach (var port in nginxPorts) ports.Add(port);

        // 2. 从appsettings.json提取端口
        var jsonPorts = ExtractPortsFromAppSettings(workDir);
        foreach (var port in jsonPorts) ports.Add(port);

        // 3. 从web.config提取端口
        var webConfigPorts = ExtractPortsFromWebConfig(workDir);
        foreach (var port in webConfigPorts) ports.Add(port);

        return ports;
    }

    /// <summary>从Nginx配置文件提取监听端口</summary>
    private static IEnumerable<Int32> ExtractPortsFromNginx(String workDir)
    {
        var ports = new HashSet<Int32>();

        try
        {
            var files = Directory.GetFiles(workDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWithIgnoreCase(".nginx", ".conf"));

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                // 匹配 listen 80; 或 listen 8080; 等
                var matches = Regex.Matches(content, @"listen\s+(\d+)", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (Int32.TryParse(match.Groups[1].Value, out var port))
                    {
                        if (port > 0 && port <= 65535)
                            ports.Add(port);
                    }
                }
            }
        }
        catch { }

        return ports;
    }

    /// <summary>从appsettings.json提取监听端口</summary>
    private static IEnumerable<Int32> ExtractPortsFromAppSettings(String workDir)
    {
        var ports = new HashSet<Int32>();

        try
        {
            var files = Directory.GetFiles(workDir, "appsettings*.json", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);

                // 匹配 "urls": "http://localhost:5000;https://localhost:5001"
                // 需要提取 urls 字段的值，然后从中提取所有端口
                var urlsMatch = Regex.Match(content, @"""urls"":\s*""([^""]*)""", RegexOptions.IgnoreCase);
                if (urlsMatch.Success)
                {
                    var urlsValue = urlsMatch.Groups[1].Value;
                    // 从 urls 值中提取所有端口号
                    var portMatches = Regex.Matches(urlsValue, @":(\d+)");
                    foreach (Match match in portMatches)
                    {
                        if (Int32.TryParse(match.Groups[1].Value, out var port))
                        {
                            if (port > 0 && port <= 65535)
                                ports.Add(port);
                        }
                    }
                }

                // 匹配 Kestrel:Endpoints 配置
                var kestrelMatches = Regex.Matches(content, @"""Url"":\s*""[^""]*:(\d+)", RegexOptions.IgnoreCase);
                foreach (Match match in kestrelMatches)
                {
                    if (Int32.TryParse(match.Groups[1].Value, out var port))
                    {
                        if (port > 0 && port <= 65535)
                            ports.Add(port);
                    }
                }
            }
        }
        catch { }

        return ports;
    }

    /// <summary>从web.config提取监听端口</summary>
    private static IEnumerable<Int32> ExtractPortsFromWebConfig(String workDir)
    {
        var ports = new HashSet<Int32>();

        try
        {
            var file = Path.Combine(workDir, "web.config");
            if (!File.Exists(file)) return ports;

            var content = File.ReadAllText(file);

            // 匹配 bindingInformation="*:80:" 等
            var matches = Regex.Matches(content, @"bindingInformation=""[^:]*:(\d+):", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (Int32.TryParse(match.Groups[1].Value, out var port))
                {
                    if (port > 0 && port <= 65535)
                        ports.Add(port);
                }
            }
        }
        catch { }

        return ports;
    }
    #endregion

    #region 日志
    public ILog Log { get; set; } = XTrace.Log;

    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}

/// <summary>防火墙类型</summary>
internal enum FirewallType
{
    /// <summary>无防火墙</summary>
    None = 0,

    /// <summary>Windows防火墙</summary>
    WindowsFirewall = 1,

    /// <summary>firewalld (CentOS/RHEL/Fedora)</summary>
    Firewalld = 2,

    /// <summary>ufw (Ubuntu/Debian)</summary>
    Ufw = 3,

    /// <summary>iptables (通用)</summary>
    Iptables = 4,
}
