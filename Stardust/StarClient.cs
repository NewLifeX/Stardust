using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Managers;
using Stardust.Models;

namespace Stardust;

/// <summary>星星客户端。每个设备节点有一个客户端连接服务端</summary>
public class StarClient : ClientBase, ICommandClient, IEventProvider
{
    #region 属性
    /// <summary>产品编码</summary>
    public String? ProductCode { get; set; }

    /// <summary>服务迁移</summary>
    public event EventHandler<MigrationEventArgs>? OnMigration;

    /// <summary>插件列表</summary>
    public String[]? Plugins { get; set; }

    private FrameworkManager _frameworkManager = new();
    private readonly ICache _cache = new MemoryCache();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public StarClient()
    {
        Features = Features.Login | Features.Logout | Features.Ping | Features.Upgrade | Features.Notify | Features.CommandReply | Features.PostEvent;
        SetActions("Node/");

        Log = XTrace.Log;
    }

    /// <summary>实例化</summary>
    /// <param name="urls"></param>
    public StarClient(String urls) : this() => Server = urls;
    #endregion

    #region 方法
    /// <summary>初始化</summary>
    protected override void OnInit()
    {
        var provider = ServiceProvider ??= ObjectContainer.Provider;

        PasswordProvider = new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 };

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
        var request = new LoginInfo();
        FillLoginRequest(request);

        request.ProductCode = ProductCode;
        request.Node = GetNodeInfo();

        return request;
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

            ProcessorCount = Environment.ProcessorCount,
            Memory = mi.Memory,
            AvailableMemory = mi.AvailableMemory,
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

        // 目标框架
        di.Framework = _frameworkManager.GetAllVersions().Join(",", e => e.TrimStart('v'));

#if NETCOREAPP || NETSTANDARD
        di.Framework ??= RuntimeInformation.FrameworkDescription?.TrimStart(".NET Framework", ".NET Core", ".NET Native", ".NET").Trim();

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

        di.Framework ??= ver?.TrimStart(".NET Framework", ".NET Core", ".NET Native", ".NET").Trim();
        di.Architecture = IntPtr.Size == 8 ? "X64" : "X86";
#endif

        if (Runtime.Windows) FillOnWindows(di);
        if (Runtime.Linux) FillOnLinux(di);

        return di;
    }

    /// <summary>填充Windows专属信息</summary>
    /// <param name="di"></param>
    public static void FillOnWindows(NodeInfo di)
    {
#if NETFRAMEWORK || WINDOWS
        try
        {
            // 收集屏幕相关信息。Mono+Linux无法获取
            var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            di.Dpi = $"{g.DpiX}*{g.DpiY}";
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen != null)
                di.Resolution = $"{screen.Bounds.Width}*{screen.Bounds.Height}";
        }
        catch { }
#else
        try
        {
            var graphics = IntPtr.Zero;
            var num = NativeMethods.GdipCreateFromHWND(new HandleRef(null, IntPtr.Zero), out graphics);
            if (num == 0)
            {
                var xx = new Single[1];
                var numx = NativeMethods.GdipGetDpiX(new HandleRef(di, graphics), xx);

                var yy = new Single[1];
                var numy = NativeMethods.GdipGetDpiY(new HandleRef(di, graphics), yy);

                if (numx == 0 && numy == 0) di.Dpi = $"{xx[0]}*{yy[0]}";
            }

            var w = NativeMethods.GetSystemMetrics(0);
            var h = NativeMethods.GetSystemMetrics(1);
            if (w > 0 && h > 0) di.Resolution = $"{w}*{h}";
        }
        catch { }
#endif
    }

    /// <summary>填充Linux专属信息</summary>
    /// <param name="di"></param>
    public static void FillOnLinux(NodeInfo di)
    {
        di.MaxOpenFiles = Execute("bash", "-c \"ulimit -n\"")?.Trim().ToInt() ?? 0;

        // 很多Linux系统没有xrandr命令
        //var xrandr = Execute("xrandr", "-q");
        //if (!xrandr.IsNullOrEmpty())
        //{
        //    var current = xrandr.Substring("current", ",").Trim();
        //    if (!current.IsNullOrEmpty())
        //    {
        //        var ss = current.SplitAsInt("x");
        //        if (ss.Length >= 2) di.Resolution = $"{ss[0]}*{ss[1]}";
        //    }
        //}
    }

    /// <summary>获取驱动器信息</summary>
    /// <returns></returns>
    public static IList<DriveInfo> GetDrives()
    {
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
            var process = Process.Start(psi);
            if (process == null) return null;

            if (!process.WaitForExit(3_000))
            {
                process.Kill();
                return null;
            }

            return process.StandardOutput.ReadToEnd();
        }
        catch (Exception ex)
        {
            XTrace.WriteLine(ex.Message);
            return null;
        }
    }
    #endregion

    #region 心跳
    private readonly String[] _excludes = ["Idle", "System", "Registry", "smss", "csrss", "lsass", "wininit", "services", "winlogon", "LogonUI", "SearchUI", "fontdrvhost", "dwm", "svchost", "dllhost", "conhost", "taskhostw", "explorer", "ctfmon", "ChsIME", "WmiPrvSE", "WUDFHost", "TabTip*", "igfxCUIServiceN", "igfxEMN", "smartscreen", "sihost", "RuntimeBroker", "StartMenuExperienceHost", "SecurityHealthSystray", "SecurityHealthService", "ShellExperienceHost", "PerfWatson2", "audiodg", "spoolsv", "*ServiceHub*", "systemd*", "cron", "rsyslogd", "sudo", "dbus*", "bash", "login", "networkd*", "kworker*", "ksoftirqd*", "migration*", "auditd", "polkitd", "atd"];

    /// <summary>构建心跳请求</summary>
    /// <returns></returns>
    public override IPingRequest BuildPingRequest()
    {
        var request = new PingInfo();
        FillPingRequest(request);

        var exs = _excludes.Where(e => e.Contains('*')).ToArray();

        var ps = Process.GetProcesses();
        var pcs = new List<String>();
        foreach (var item in ps)
        {
            // 有些进程可能已退出，无法获取详细信息
            try
            {
                if (Runtime.Linux && item.SessionId == 0) continue;

                var name = item.GetProcessName();
                if (name.EqualIgnoreCase(_excludes) || exs.Any(e => e.IsMatch(name))) continue;

                if (!pcs.Contains(name)) pcs.Add(name);
            }
            catch { }
        }

        var mi = MachineInfo.GetCurrent();
        mi.Refresh();

        var drives = GetDrives();

        request.IP = AgentInfo.GetIps();
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

        if (mi is IExtend ext)
        {
            // 读取无线信号强度
            if (ext.Items.TryGetValue("Signal", out var value)) request.Signal = value.ToInt();
        }

        return request;
    }

    /// <summary>心跳</summary>
    /// <returns></returns>
    public override async Task<IPingResponse?> Ping(CancellationToken cancellationToken = default)
    {
        var rs = await base.Ping(cancellationToken).ConfigureAwait(false);
        if (rs != null)
        {
            // 迁移到新服务器
            if (rs is PingResponse prs && !prs.NewServer.IsNullOrEmpty() && prs.NewServer != Server)
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

                    await Login(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return rs;
    }
    #endregion

    #region 部署
    /// <summary>获取分配到本节点的应用服务信息</summary>
    /// <returns></returns>
    public Task<DeployInfo[]?> GetDeploy() => InvokeAsync<DeployInfo[]>("Deploy/GetAll");

    /// <summary>上传本节点的所有应用服务信息</summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public Task<Int32> UploadDeploy(ServiceInfo[] services) => InvokeAsync<Int32>("Deploy/Upload", services);

    /// <summary>应用心跳。上报应用信息</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    public Task<Int32> AppPing(AppInfo inf) => InvokeAsync<Int32>("Deploy/Ping", inf);
    #endregion
}