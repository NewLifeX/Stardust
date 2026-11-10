using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Managers;
using Stardust.Models;
using Stardust.Monitors;

namespace Stardust;

/// <summary>星星客户端。每个设备节点有一个客户端连接服务端</summary>
public partial class StarClient : ClientBase, ICommandClient, IEventProvider
{
    #region 属性
    /// <summary>产品编码</summary>
    public String? ProductCode { get; set; }

    /// <summary>服务迁移</summary>
    public event EventHandler<MigrationEventArgs>? OnMigration;

    /// <summary>插件列表</summary>
    public String[]? Plugins { get; set; }

    private FrameworkManager _frameworkManager = new();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public StarClient() => SetDefault();

    private void SetDefault()
    {
        Features = Features.Login | Features.Logout | Features.Ping | Features.Upgrade | Features.Notify | Features.CommandReply | Features.PostEvent;
        SetActions("Node/");

        Log = XTrace.Log;

        // NativeAOT 下模型属性必须显式保留，否则序列化为空对象
        AotRoots.Ensure();
    }

    /// <summary>使用客户端设置来初始化</summary>
    /// <param name="setting"></param>
    public StarClient(IClientSetting setting) : base(setting)
    {
        SetDefault();

        // 配置了语言时固定本进程的界面与日志语言
        if (setting is StarSetting star) StarLocalization.UseLanguage(star.Language);
    }

    /// <summary>实例化</summary>
    /// <param name="urls"></param>
    public StarClient(String urls) : this() => Server = urls;
    #endregion

    #region 多语言
    /// <summary>应用平台下发的默认语言。本地配置固定语言时不接受平台默认语言</summary>
    /// <param name="language">语言名称</param>
    /// <param name="save">是否保存到核心配置</param>
    /// <remarks>
    /// 优先级为本地固定语言 &gt; 平台默认语言 &gt; 系统语言。客户端配置 <c>Star.Language</c> 已固定进程语言时，
    /// 平台下发的默认语言不能改动界面与日志，也不写入核心配置，避免下次启动被平台语言顶掉。
    /// 平台下发 <see cref="StarLocalization.AutoLanguage"/> 时清除本机保存的平台语言，恢复系统语言。
    /// </remarks>
    protected override void ApplyDefaultLanguage(String? language, Boolean save = true)
    {
        if (Setting is StarSetting star && !star.Language.IsNullOrWhiteSpace()) return;

        if (language.EqualIgnoreCase(StarLocalization.AutoLanguage))
        {
            ResetDefaultLanguage(save);
            return;
        }

        base.ApplyDefaultLanguage(language, save);
    }

    /// <summary>清除本机保存的平台默认语言，恢复系统语言</summary>
    /// <param name="save">是否同步清除核心配置</param>
    private static void ResetDefaultLanguage(Boolean save)
    {
        var core = NewLife.Setting.Current;
        if (save && !core.DefaultLanguage.IsNullOrEmpty())
        {
            var old = core.DefaultLanguage;
            core.DefaultLanguage = "";
            try
            {
                // Save 内部会重新应用语言配置，顺带清空 DefaultCulture
                core.Save();
            }
            catch
            {
                // 保存失败时回滚，下一次登录/心跳继续尝试
                core.DefaultLanguage = old;
            }
        }

        // 兼容 save=false、保存失败以及核心配置原本为空的情况
        var cleared = Localization.DefaultCulture;
        if (cleared != null)
        {
            Localization.SetDefaultLanguage(null);
            if (Localization.ForcedLanguage == null && Localization.LogCulture?.Name == cleared.Name)
                Localization.LogCulture = null;
        }
    }
    #endregion

    #region 方法
    /// <summary>初始化</summary>
    protected override void OnInit()
    {
        var provider = ServiceProvider ??= ObjectContainer.Provider;

        PasswordProvider = new ServerTimePasswordProvider(() => GetNow()) { Algorithm = "md5", SaltTime = 60 };

        // 找到容器，注册默认的模型实现，供后续InvokeAsync时自动创建正确的模型对象
        var container = ModelExtension.GetService<IObjectContainer>(provider) ?? ObjectContainer.Current;
        if (container != null)
        {
            container.AddTransient<ILoginRequest, LoginInfo>();
            container.AddTransient<IPingRequest, PingInfo>();
        }

        _frameworkManager.Attach(this);

        base.OnInit();
    }
    #endregion

    #region 登录
    /// <summary>创建登录请求</summary>
    /// <returns></returns>
    public override ILoginRequest BuildLoginRequest()
    {
        EnsureServerTime();

        var request = new LoginInfo();
        FillLoginRequest(request);

        request.ProductCode = ProductCode;
        request.Node = GetNodeInfo();

        return request;
    }

    /// <summary>登录前对齐服务端时间</summary>
    /// <remarks>
    /// 本机时钟偏差超过盐值窗口时，用本机时间生成的盐值会被服务端判定过期，登录失败并反复重试。
    /// 登录前先用匿名接口取一次服务端时间，把偏移写入客户端，使盐值按服务端时间生成。
    /// 探测失败不阻塞登录，保持原有行为。
    /// </remarks>
    private void EnsureServerTime()
    {
        if (Span != TimeSpan.Zero) return;

        var time = ServerTimeProbe.GetServerTime(Server ?? Setting?.Server);
        if (time > 0) FixTime(0, time);
    }

    /// <summary>获取设备信息</summary>
    /// <returns></returns>
    public NodeInfo GetNodeInfo()
    {
        var mi = MachineInfo.GetCurrent();

        var asm = AssemblyX.Entry ?? AssemblyX.Create(Assembly.GetExecutingAssembly());
        var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).Where(e => e != "00-00-00-00-00-00").OrderBy(e => e).Join(",");
        var path = ".".GetFullPath();
        var drives = GetDrives();
        var driveInfo = DriveInfo.GetDrives().FirstOrDefault(e => path.StartsWithIgnoreCase(e.Name));
        var di = new NodeInfo
        {
            Version = asm?.FileVersion,
            Compile = asm?.Compile ?? DateTime.MinValue,

            OSName = mi.OSName,
            OSVersion = mi.OSVersion,

            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            IP = AgentInfo.GetIps(),
            Gateway = AgentInfo.GetGateway(),
            Dns = AgentInfo.GetDnsList(),

            ProcessorCount = Environment.ProcessorCount,
            Memory = mi.Memory,
            AvailableMemory = mi.AvailableMemory,
            FreeMemory = mi.FreeMemory,
            TotalSize = (UInt64)(driveInfo?.TotalSize ?? 0),
            AvailableFreeSpace = (UInt64)(driveInfo?.AvailableFreeSpace ?? 0),
            DriveSize = (UInt64)drives.Sum(e => e.TotalSize),
            DriveInfo = drives.Join(",", e => $"{e.Name}[{e.DriveFormat}]={e.AvailableFreeSpace.ToGMK()}/{e.TotalSize.ToGMK()}"),

            Product = mi.Product,
            Vendor = mi.Vendor,
            Processor = mi.Processor,
            //CpuRate = mi.CpuRate,
            UUID = mi.UUID,
            MachineGuid = mi.Guid,
            SerialNumber = mi.Serial,
            Board = mi.Board,
            DiskID = mi.DiskID,

            Macs = mcs,

            InstallPath = ".".GetFullPath(),
            Runtime = Environment.Version + "",

            Time = DateTime.UtcNow,
            Plugins = Plugins,
        };

        // 获取最新机器名
        if (Runtime.Linux)
        {
            var file = @"/etc/hostname";
            if (File.Exists(file)) di.MachineName = File.ReadAllText(file).Trim();
        }

        // 获取时区
        try
        {
            var tz = TimeZoneInfo.Local;
            // 优先使用IANA时区名称（如Asia/Shanghai），否则使用偏移量格式
            di.TimeZone = tz.Id;
            if (di.TimeZone.IsNullOrEmpty() || di.TimeZone.Contains(' '))
            {
                var offset = tz.BaseUtcOffset;
                di.TimeZone = $"GMT{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:00}:{offset.Minutes:00}";
            }
        }
        catch { }

        // 目标框架
        di.Framework = _frameworkManager.GetAllVersions().Join(",", e => e.TrimStart('v'));

#if NETCOREAPP || NETSTANDARD
        di.Framework ??= RuntimeInformation.FrameworkDescription?.TrimPrefix(".NET Framework").TrimPrefix(".NET Core").TrimPrefix(".NET Native").TrimPrefix(".NET").Trim();

        di.Architecture = RuntimeInformation.ProcessArchitecture + "";

        if (Runtime.Linux)
        {
            // 识别Alpine
            var nr = new NetRuntime();
            if (nr.IsAlpine() && !di.OSName.StartsWithIgnoreCase("Alpine")) di.OSName = $"{di.OSName}(Alpine)";
        }
#else
        var ver = "";
        var tar = asm?.Asm.GetCustomAttribute<TargetFrameworkAttribute>();
        if (tar != null) ver = !tar.FrameworkDisplayName.IsNullOrEmpty() ? tar.FrameworkDisplayName : tar.FrameworkName;

        di.Framework ??= ver?.TrimPrefix(".NET Framework").TrimPrefix(".NET Core").TrimPrefix(".NET Native").TrimPrefix(".NET").Trim();
        di.Architecture = IntPtr.Size == 8 ? "X64" : "X86";
#endif

#if NET5_0_OR_GREATER
        if (OperatingSystem.IsWindows()) FillOnWindows(di);
        if (OperatingSystem.IsLinux()) FillOnLinux(di);
        if (OperatingSystem.IsMacOS()) FillOnOSX(di);
#else
        if (Runtime.Windows) FillOnWindows(di);
        if (Runtime.Linux) FillOnLinux(di);
        if (Runtime.OSX) FillOnOSX(di);
#endif

        return di;
    }

    private static DateTime _drivesTime;
    private static IList<DriveInfo>? _drives;

    /// <summary>获取驱动器信息。带短缓存，避免每次心跳枚举磁盘</summary>
    /// <returns></returns>
    public static IList<DriveInfo> GetDrives()
    {
        // 磁盘信息短缓存，60秒内复用，避免每次心跳枚举磁盘
        var now = DateTime.Now;
        if (_drives != null && (now - _drivesTime).TotalSeconds < 60) return _drives;

        var list = new List<DriveInfo>();
        foreach (var di in DriveInfo.GetDrives())
        {
            if (!di.IsReady) continue;
            if (di.DriveType is not DriveType.Fixed and not DriveType.Removable) continue;
            if (di.Name != "/" && di.DriveFormat.EqualIgnoreCase("overlay", "squashfs")) continue;
            if (di.Name.Contains("container") && di.Name.EndsWithIgnoreCase("/overlay")) continue;
            if (di.TotalSize <= 0) continue;

            if (!list.Any(e => e.Name == di.Name)) list.Add(di);
        }

        _drives = list;
        _drivesTime = now;

        return list;
    }

    private static String? Execute(String cmd, String? arguments = null)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd, arguments ?? "")
            {
                // UseShellExecute 必须 false，以便于后续重定向输出流
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(psi);
            if (process == null) return null;

            if (!process.WaitForExit(3_000))
            {
                process.Kill();
                return null;
            }

            return process.StandardOutput.ReadToEnd();
        }
#if DEBUG
        catch (Exception ex)
        {
            XTrace.WriteLine(ex.Message);
            return null;
        }
#else
        catch
        {
            return null;
        }
#endif
    }
    #endregion

    #region 心跳
    private readonly String[] _excludes = ["Idle", "System", "Registry", "smss", "csrss", "lsass", "wininit", "services", "winlogon", "LogonUI", "SearchUI", "fontdrvhost", "dwm", "svchost", "dllhost", "conhost", "taskhostw", "explorer", "ctfmon", "ChsIME", "WmiPrvSE", "WUDFHost", "TabTip*", "igfxCUIServiceN", "igfxEMN", "smartscreen", "sihost", "RuntimeBroker", "StartMenuExperienceHost", "SecurityHealthSystray", "SecurityHealthService", "ShellExperienceHost", "PerfWatson2", "audiodg", "spoolsv", "*ServiceHub*", "systemd*", "cron", "rsyslogd", "sudo", "dbus*", "bash", "login", "networkd*", "kworker*", "ksoftirqd*", "migration*", "auditd", "polkitd", "atd"];

    private volatile PingResult? _lastGwResult;
    private volatile PingResult? _lastDnsResult;
    private volatile PingResult? _lastSvrResult;
    private DateTime _lastMeasureTime;
    private volatile Boolean _measuring;

    /// <summary>构建心跳请求</summary>
    /// <returns></returns>
    public override IPingRequest BuildPingRequest()
    {
        var request = new PingInfo();
        FillPingRequest(request);

        // 获取网络质量（使用后台缓存，避免阻塞心跳）
        EnsurePingResults();

        var exs = _excludes.Where(e => e.Contains('*')).ToArray();

        var ps = Process.GetProcesses();
        var pcs = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ps)
        {
            // 有些进程可能已退出，无法获取详细信息
            try
            {
                if (Runtime.Linux && item.SessionId == 0) continue;

                var name = item.GetProcessName();
                if (name.EqualIgnoreCase(_excludes) || exs.Any(e => e.IsMatch(name))) continue;

                pcs.Add(name);
            }
            catch { }
            finally
            {
                // 释放进程句柄，避免每次心跳泄漏句柄
                item.Dispose();
            }
        }

        var mi = MachineInfo.GetCurrent();
        mi.Refresh();

        var drives = GetDrives();

        request.IP = AgentInfo.GetIps();
        request.Gateway = AgentInfo.GetGateway();
        request.Dns = AgentInfo.GetDnsList();
        request.DriveInfo = drives.Join(",", e => $"{e.Name}[{e.DriveFormat}]={e.AvailableFreeSpace.ToGMK()}/{e.TotalSize.ToGMK()}");
        request.Macs = (String?)NetHelper.GetMacs().Select(e => e.ToHex("-")).OrderBy(e => e).Join(",");
        request.ProcessCount = ps.Length;
        request.Processes = pcs.Join();

        // 目标框架
        request.Framework = _frameworkManager.GetAllVersions().Join(",", e => e.TrimStart('v'));

        // 获取Tcp连接信息，某些Linux平台不支持
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = properties.GetActiveTcpConnections();

            request.TcpConnections = connections.Count(e => e.State == TcpState.Established);
            request.TcpTimeWait = connections.Count(e => e.State == TcpState.TimeWait);
            request.TcpCloseWait = connections.Count(e => e.State == TcpState.CloseWait);
        }
        catch { }

        // 使用缓存的网络质量结果
        var gwResult = _lastGwResult;
        var dnsResult = _lastDnsResult;
        var svrResult = _lastSvrResult;

        // 网关结果
        if (gwResult != null)
        {
            request.IntranetScore = gwResult.Score;
            request.GatewayLatency = gwResult.Latency;
            request.GatewayLossRate = gwResult.LossRate;
        }

        // DNS结果
        if (dnsResult != null)
        {
            request.DnsLatency = dnsResult.Latency;
            request.DnsLossRate = dnsResult.LossRate;
        }

        // 服务器结果
        if (svrResult != null)
        {
            request.ServerLatency = svrResult.Latency;
            request.ServerLossRate = svrResult.LossRate;
        }

        // 外网质量综合评分：DNS占30%，服务器占70%
        request.InternetScore = (dnsResult?.Score ?? 0) * 0.3 + (svrResult?.Score ?? 0) * 0.7;

        if (mi is IExtend ext)
        {
            // 读取无线信号强度
            if (ext.Items.TryGetValue("Signal", out var value)) request.Signal = value.ToInt();
        }

        // 平台特定采集（Load1、DiskIOPS、GPU）
        if (Runtime.Windows) FillPingOnWindows(request);
        if (Runtime.Linux) FillPingOnLinux(request);
        if (Runtime.OSX) FillPingOnOSX(request);

        // 最后设置时间，避免因为代码执行原因导致误差过大
        request.Uptime = (Int32)(Runtime.TickCount64 / 1000);
        request.Time = DateTime.UtcNow.ToLong();

        return request;
    }

    private void EnsurePingResults()
    {
        var now = DateTime.UtcNow;
        if (_lastGwResult != null && _lastDnsResult != null && _lastSvrResult != null &&
            (now - _lastMeasureTime).TotalSeconds < 48)
            return;

        if (_measuring) return;

        _measuring = true;
        Task.Run(MeasurePingResultsAsync);
    }

    private async Task MeasurePingResultsAsync()
    {
        try
        {
            var monitor = new PingMonitor();
            var gw = AgentInfo.GetGateway();
            if (gw != null && gw.Contains('/')) gw = gw.Substring(0, gw.IndexOf("/"));
            var dns = AgentInfo.GetDns();
            if (dns.IsNullOrEmpty() || IPAddress.TryParse(dns, out var ip) && ip.IsLocal()) dns = "223.5.5.5";
            var svr = (Client as ApiHttpClient)?.Current?.Address.Host;

            var gwTask = Task.Run(() => monitor.GetResultAsync(gw));
            var dnsTask = Task.Run(() => monitor.GetResultAsync(dns));
            var svrTask = Task.Run(() => monitor.GetResultAsync(svr));

            _lastGwResult = await gwTask.ConfigureAwait(false);
            _lastDnsResult = await dnsTask.ConfigureAwait(false);
            _lastSvrResult = await svrTask.ConfigureAwait(false);

            _lastMeasureTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            XTrace.WriteLine(L.TLog("Background network quality measurement failed: {0}", ex.Message));
        }
        finally
        {
            _measuring = false;
        }
    }

    /// <summary>心跳</summary>
    /// <returns></returns>
    public override async Task<IPingResponse?> Ping(CancellationToken cancellationToken = default)
    {
        var rs = await base.Ping(cancellationToken).ConfigureAwait(false);
        if (rs != null)
        {
            // 迁移到新服务器
            if (rs is IPingResponse2 prs && !prs.NewServer.IsNullOrEmpty() && prs.NewServer != Server)
            {
                var arg = new MigrationEventArgs { NewServer = prs.NewServer };

                OnMigration?.Invoke(this, arg);
                if (!arg.Cancel)
                {
                    await Logout($"切换新服务器：{prs.NewServer}，原服务器：{Server}", cancellationToken).ConfigureAwait(false);

                    // 清空原有链接，添加新链接
                    Server = prs.NewServer;
                    Client = null;
                    Status = LoginStatus.Ready;

                    await Login("切换新服务器", cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return rs;
    }
    #endregion

    #region 部署
    /// <summary>获取分配到本节点的应用服务信息</summary>
    /// <returns></returns>
    public Task<DeployInfo[]?> GetDeploy(Int32 deployId, String deployName, String appName) => InvokeAsync<DeployInfo[]>("Deploy/GetAll", new Dictionary<String, Object?>
    {
        ["deployId"] = deployId,
        ["deployName"] = deployName,
        ["appName"] = appName,
    });

    /// <summary>上传本节点的所有应用服务信息</summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public Task<Int32> UploadDeploy(ServiceInfo[] services) => InvokeAsync<Int32>("Deploy/Upload", services);

    /// <summary>应用心跳。上报应用信息</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    public Task<Int32> AppPing(AppInfo inf) => InvokeAsync<Int32>("Deploy/Ping", inf);
    #endregion

    #region 升级
    private Int32 _upgrading;
    /// <summary>获取更新信息。如有更新，则下载解压覆盖并重启应用</summary>
    /// <remarks>
    /// 心跳定时检查与平台下发的 node/upgrade 命令可能同时触发升级。两个流程同时下载并覆盖同一批文件，
    /// 会互相破坏安装目录（Linux 下正在运行的 dll 被覆盖还可能直接导致进程崩溃），因此后到者直接跳过。
    /// </remarks>
    /// <param name="channel">更新通道</param>
    /// <param name="version">指定版本。用于手工升级到指定版本，为空时按通道策略匹配</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新信息；已有升级进行中时返回null</returns>
    public override async Task<IUpgradeInfo?> Upgrade(String? channel, String? version, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _upgrading, 1, 0) != 0)
        {
            // 平台命令会把 null 解释成“无可用更新”，这里补一条事件，让节点历史能看出是“已有升级进行中”
            var msg = L.T("Another upgrade is already in progress; skipping this request.");
            WriteLog(msg);
            WriteEvent("info", "Upgrade", msg);

            return null;
        }

        try
        {
            return await base.Upgrade(channel, version, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _upgrading, 0);
        }
    }
    #endregion
}
