using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;
using Stardust.Services;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace Stardust;

/// <summary>星星客户端。每个设备节点有一个客户端连接服务端</summary>
public class StarClient : ApiHttpClient, ICommandClient, IEventProvider
{
    #region 属性
    /// <summary>证书</summary>
    public String Code { get; set; }

    /// <summary>密钥</summary>
    public String Secret { get; set; }

    /// <summary>产品编码</summary>
    public String ProductCode { get; set; }

    /// <summary>是否已登录</summary>
    public Boolean Logined { get; set; }

    /// <summary>登录完成后触发</summary>
    public event EventHandler OnLogined;

    /// <summary>最后一次登录成功后的消息</summary>
    public LoginResponse Info { get; private set; }

    /// <summary>请求到服务端并返回的延迟时间。单位ms</summary>
    public Int32 Delay { get; set; }

    ///// <summary>本地应用服务管理</summary>
    //public ServiceManager Manager { get; set; }

    /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认120</summary>
    public Int32 MaxFails { get; set; } = 120;

    private ConcurrentDictionary<String, Delegate> _commands = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>命令集合</summary>
    public IDictionary<String, Delegate> Commands => _commands;

    /// <summary>收到命令时触发</summary>
    public event EventHandler<CommandEventArgs> Received;

    private readonly ConcurrentQueue<PingInfo> _fails = new();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public StarClient()
    {
        Log = XTrace.Log;

        _task = MachineInfo.RegisterAsync();
    }

    /// <summary>实例化</summary>
    /// <param name="urls"></param>
    public StarClient(String urls) : this()
    {
        if (!urls.IsNullOrEmpty())
        {
            var ss = urls.Split(",");
            for (var i = 0; i < ss.Length; i++)
            {
                Add("service" + (i + 1), new Uri(ss[i]));
            }
        }
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        Logout(disposing ? "Dispose" : "GC").Wait(1_000);

        StopTimer();

        base.Dispose(disposing);
    }
    #endregion

    #region 方法
    /// <summary>远程调用拦截，支持重新登录</summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="method"></param>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <param name="onRequest"></param>
    /// <returns></returns>
    public override async Task<TResult> InvokeAsync<TResult>(HttpMethod method, String action, Object args = null, Action<HttpRequestMessage> onRequest = null)
    {
        try
        {
            return await base.InvokeAsync<TResult>(method, action, args, onRequest);
        }
        catch (Exception ex)
        {
            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex && (aex.Code == 401 || aex.Code == 403) && !action.EqualIgnoreCase("Node/Login", "Node/Logout"))
            {
                Log?.Debug("{0}", ex);
                //XTrace.WriteException(ex);
                WriteLog("重新登录！");
                await Login();

                return await base.InvokeAsync<TResult>(method, action, args, onRequest);
            }

            throw;
        }
    }
    #endregion

    #region 登录
    /// <summary>登录</summary>
    /// <returns></returns>
    public async Task<Object> Login()
    {
        XTrace.WriteLine("登录：{0}", Code);

        var info = GetLoginInfo();

        // 登录前清空令牌，避免服务端使用上一次信息
        Token = null;
        Logined = false;
        Info = null;

        var rs = Info = await LoginAsync(info);
        if (rs != null && !rs.Code.IsNullOrEmpty())
        {
            XTrace.WriteLine("下发证书：{0}/{1}", rs.Code, rs.Secret);
            Code = rs.Code;
            Secret = rs.Secret;
        }

        // 登录后设置用于用户认证的token
        Token = rs.Token;
        Logined = true;

        OnLogined?.Invoke(this, EventArgs.Empty);

        StartTimer();

        return rs;
    }

    /// <summary>获取登录信息</summary>
    /// <returns></returns>
    public LoginInfo GetLoginInfo()
    {
        var di = GetNodeInfo();
        var ext = new LoginInfo
        {
            Code = Code,
            Secret = Secret.IsNullOrEmpty() ? null : Secret.MD5(),
            ProductCode = ProductCode,

            Node = di,
        };

        return ext;
    }

    private readonly Task<MachineInfo> _task;
    /// <summary>获取设备信息</summary>
    /// <returns></returns>
    public NodeInfo GetNodeInfo()
    {
        var mi = MachineInfo.Current ?? _task.Result;

        var asm = AssemblyX.Entry ?? AssemblyX.Create(Assembly.GetExecutingAssembly());
        //var ps = System.IO.Ports.SerialPort.GetPortNames();
        var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).Where(e => e != "00-00-00-00-00-00").OrderBy(e => e).Join(",");
        //var driveInfo = new DriveInfo(Path.GetPathRoot(".".GetFullPath()));
        var path = ".".GetFullPath();
        var drives = GetDrives();
        var driveInfo = DriveInfo.GetDrives().FirstOrDefault(e => path.StartsWithIgnoreCase(e.Name));
        var di = new NodeInfo
        {
            Version = asm?.FileVersion,
            Compile = asm?.Compile ?? DateTime.MinValue,

            OSName = mi.OSName,
            OSVersion = mi.OSVersion,
            //Architecture = RuntimeInformation.ProcessArchitecture,

            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            IP = AgentInfo.GetIps(),

            ProcessorCount = Environment.ProcessorCount,
            Memory = mi.Memory,
            AvailableMemory = mi.AvailableMemory,
            TotalSize = (UInt64)driveInfo?.TotalSize,
            AvailableFreeSpace = (UInt64)driveInfo?.AvailableFreeSpace,
            DriveInfo = drives.Join(",", e => $"{e.Name}[{e.DriveFormat}]={e.AvailableFreeSpace}/{e.TotalSize}"),

            Product = mi.Product,
            Processor = mi.Processor,
            //CpuID = mi.CpuID,
            CpuRate = mi.CpuRate,
            UUID = mi.UUID,
            MachineGuid = mi.Guid,
            SerialNumber = mi.Serial,
            Board = mi.Board,
            DiskID = mi.DiskID,

            Macs = mcs,
            //COMs = ps.Join(","),

            InstallPath = ".".GetFullPath(),
            Runtime = Environment.Version + "",

            Time = DateTime.UtcNow,
        };

#if NETCOREAPP || NETSTANDARD
        // 目标框架
        di.Framework = GetNetCore()?.ToString();
        di.Framework ??= RuntimeInformation.FrameworkDescription?.TrimStart(".NET Framework", ".NET Core", ".NET Native", ".NET").Trim();

        di.Architecture = RuntimeInformation.ProcessArchitecture + "";
#else
        var ver = "";
        var tar = asm.Asm.GetCustomAttribute<TargetFrameworkAttribute>();
        if (tar != null) ver = !tar.FrameworkDisplayName.IsNullOrEmpty() ? tar.FrameworkDisplayName : tar.FrameworkName;

        di.Framework = ver?.TrimStart(".NET Framework", ".NET Core", ".NET Native", ".NET").Trim();

        // .NET45以上运行时
        if (Runtime.Windows && Environment.Version >= new Version("4.0.30319.42000"))
        {
            var reg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            if (reg != null)
            {
                var str = reg.GetValue("Version") + "";
                if (!str.IsNullOrEmpty()) di.Framework = str;
            }
        }

        try
        {
            // 收集屏幕相关信息。Mono+Linux无法获取
            var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            di.Dpi = $"{g.DpiX}*{g.DpiY}";
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            di.Resolution = $"{screen.Bounds.Width}*{screen.Bounds.Height}";
        }
        catch { }
#endif

        if (Runtime.Linux) di.MaxOpenFiles = Execute("bash", "-c \"ulimit -n\"")?.Trim().ToInt() ?? 0;

        return di;
    }

    /// <summary>获取驱动器信息</summary>
    /// <returns></returns>
    public static IList<DriveInfo> GetDrives()
    {
        var list = new List<DriveInfo>();
        foreach (var di in DriveInfo.GetDrives())
        {
            if (!di.IsReady) continue;
            if (di.DriveType != DriveType.Fixed && di.DriveType != DriveType.Removable) continue;
            if (di.Name != "/" && di.DriveFormat.EqualIgnoreCase("overlay", "squashfs")) continue;
            if (di.Name.Contains("container") && di.Name.EndsWithIgnoreCase("/overlay")) continue;
            if (di.TotalSize <= 0) continue;

            if (!list.Any(e => e.Name == di.Name)) list.Add(di);
        }

        return list;
    }

    private static Version GetNetCore()
    {
        var dir = "";
        if (Environment.OSVersion.Platform <= PlatformID.WinCE)
        {
            dir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (String.IsNullOrEmpty(dir)) return null;
            dir += "\\dotnet\\shared";
        }
        else if (Runtime.Linux)
            dir = "/usr/share/dotnet/shared";

        if (!Directory.Exists(dir)) return null;

        Version ver = null;
        foreach (var item in dir.AsDirectory().GetDirectories())
        {
            foreach (var elm in item.GetDirectories())
            {
                if (Version.TryParse(elm.Name, out var v) && (ver == null || ver < v))
                    ver = v;
            }
        }

        if (ver != null) return ver;

        // 各平台通用处理
        {
            var infs = Execute("dotnet", "--list-runtimes")?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (infs != null)
            {
                foreach (var line in infs)
                {
                    var ss = line.Split(' ');
                    if (ss.Length >= 2)
                    {
                        if (Version.TryParse(ss[1], out var v) && (ver == null || ver < v))
                            ver = v;
                    }
                }
            }
        }

        return ver;
    }

    private static String Execute(String cmd, String arguments = null)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd, arguments)
            {
                // UseShellExecute 必须 false，以便于后续重定向输出流
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var process = Process.Start(psi);
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

    /// <summary>注销</summary>
    /// <param name="reason"></param>
    /// <returns></returns>
    public async Task<Object> Logout(String reason)
    {
        if (!Logined) return null;

        XTrace.WriteLine("注销：{0} {1}", Code, reason);

        try
        {
            var rs = await LogoutAsync(reason);
            if (rs != null)
            {
                // 更新令牌
                Token = rs.Token;
            }

            StopTimer();

            Logined = false;

            return rs;
        }
        catch (Exception ex)
        {
            Log?.Debug("{0}", ex);
            //XTrace.WriteException(ex);

            return null;
        }
    }

    /// <summary>登录</summary>
    /// <param name="inf">登录信息</param>
    /// <returns></returns>
    private async Task<LoginResponse> LoginAsync(LoginInfo inf) => await PostAsync<LoginResponse>("Node/Login", inf);

    /// <summary>注销</summary>
    /// <returns></returns>
    private async Task<LoginResponse> LogoutAsync(String reason) => await GetAsync<LoginResponse>("Node/Logout", new { reason });
    #endregion

    #region 心跳
    private readonly String[] _excludes = new[] { "Idle", "System", "Registry", "smss", "csrss", "lsass", "wininit", "services", "winlogon", "LogonUI", "SearchUI", "fontdrvhost", "dwm", "svchost", "dllhost", "conhost", "taskhostw", "explorer", "ctfmon", "ChsIME", "WmiPrvSE", "WUDFHost", "TabTip*", "igfxCUIServiceN", "igfxEMN", "smartscreen", "sihost", "RuntimeBroker", "StartMenuExperienceHost", "SecurityHealthSystray", "SecurityHealthService", "ShellExperienceHost", "PerfWatson2", "audiodg", "spoolsv",
        "*ServiceHub*",
        "systemd*", "cron", "rsyslogd", "sudo", "dbus*", "bash", "login", "networkd*", "kworker*", "ksoftirqd*", "migration*", "auditd", "polkitd", "atd"
    };

    /// <summary>获取心跳信息</summary>
    public PingInfo GetHeartInfo()
    {
        var exs = _excludes.Where(e => e.Contains('*')).ToArray();

        var ps = Process.GetProcesses();
        var pcs = new List<String>();
        foreach (var item in ps)
        {
            // 有些进程可能已退出，无法获取详细信息
            try
            {
                if (Runtime.Linux && item.SessionId == 0) continue;

                var name = item.ProcessName;
                if (name.EqualIgnoreCase(_excludes) || exs.Any(e => e.IsMatch(name))) continue;

                // 特殊处理dotnet
                if (name == "dotnet" || "*/dotnet".IsMatch(name)) name = AppInfo.GetProcessName(item);

                if (!pcs.Contains(name)) pcs.Add(name);
            }
            catch { }
        }

        var mi = MachineInfo.Current ?? _task.Result;
        mi.Refresh();

        var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).OrderBy(e => e).Join(",");
        var path = ".".GetFullPath();
        var driveInfo = DriveInfo.GetDrives().FirstOrDefault(e => path.StartsWithIgnoreCase(e.Name));
        var ip = AgentInfo.GetIps();
        var ext = new PingInfo
        {
            AvailableMemory = mi.AvailableMemory,
            AvailableFreeSpace = (UInt64)driveInfo?.AvailableFreeSpace,
            CpuRate = mi.CpuRate,
            Temperature = mi.Temperature,
            Battery = mi.Battery,
            UplinkSpeed = mi.UplinkSpeed,
            DownlinkSpeed = mi.DownlinkSpeed,
            ProcessCount = ps.Length,
            Uptime = Environment.TickCount / 1000,

            Macs = mcs,
            //COMs = ps.Join(","),
            IP = ip,

            Processes = pcs.Join(),

            Time = DateTime.UtcNow.ToLong(),
            Delay = Delay,
        };
        //ext.Uptime = Environment.TickCount64 / 1000;
        // 开始时间 Environment.TickCount 很容易溢出，导致开机24天后变成负数。
        // 后来在 netcore3.0 增加了Environment.TickCount64
        // 现在借助 Stopwatch 来解决
        if (Stopwatch.IsHighResolution) ext.Uptime = (Int32)(Stopwatch.GetTimestamp() / Stopwatch.Frequency);

        // 获取Tcp连接信息，某些Linux平台不支持
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = properties.GetActiveTcpConnections();

            ext.TcpConnections = connections.Count(e => e.State == TcpState.Established);
            ext.TcpTimeWait = connections.Count(e => e.State == TcpState.TimeWait);
            ext.TcpCloseWait = connections.Count(e => e.State == TcpState.CloseWait);
        }
        catch { }

        return ext;
    }

    /// <summary>心跳</summary>
    /// <returns></returns>
    public async Task<Object> Ping()
    {
        try
        {
            var inf = GetHeartInfo();

            PingResponse rs = null;
            try
            {
                rs = await PingAsync(inf);
                if (rs != null)
                {
                    // 由服务器改变采样频率
                    if (rs.Period > 0) _timer.Period = rs.Period * 1000;

                    var dt = rs.Time.ToDateTime();
                    if (dt.Year > 2000)
                    {
                        // 计算延迟
                        var ts = DateTime.UtcNow - dt;
                        var ms = (Int32)Math.Round(ts.TotalMilliseconds);
                        if (Delay > 0)
                            Delay = (Delay + ms) / 2;
                        else
                            Delay = ms;
                    }

                    // 令牌
                    if (!rs.Token.IsNullOrEmpty())
                    {
                        Token = rs.Token;
                    }

                    //// 推队列
                    //if (rs.Commands != null && rs.Commands.Length > 0)
                    //{
                    //    foreach (var item in rs.Commands)
                    //    {
                    //        //CommandQueue.Publish(item.Command, item);
                    //        await OnReceiveCommand(item);
                    //    }
                    //}

                    //// 应用服务
                    //if (rs.Services != null && rs.Services.Length > 0)
                    //{
                    //    Manager.Add(rs.Services);
                    //    Manager.CheckService();
                    //}
                }
            }
            catch
            {
                if (_fails.Count < MaxFails) _fails.Enqueue(inf);

                throw;
            }

            // 上报正常，处理历史，失败则丢弃
            while (_fails.TryDequeue(out var info))
            {
                await PingAsync(info);
            }

            return rs;
        }
        catch (Exception ex)
        {
            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex && (aex.Code == 401 || aex.Code == 403))
            {
                XTrace.WriteLine("重新登录");
                return Login();
            }

            //XTrace.WriteLine(inf.ToJson());
            XTrace.WriteLine("心跳异常 {0}", ex.GetTrue().Message);

            throw;
        }
    }

    /// <summary>心跳</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    private async Task<PingResponse> PingAsync(PingInfo inf) => await PostAsync<PingResponse>("Node/Ping", inf);

    private TraceService _trace;
    /// <summary>使用追踪服务</summary>
    public void UseTrace()
    {
        //_trace = new TraceService
        //{
        //    Queue = CommandQueue,
        //    Callback = (id, data) => ReportAsync(id, data).Wait(),
        //};
        //_trace.Init();
        _trace = new TraceService();
        //_trace.Attach(CommandQueue);
    }
    #endregion

    #region 上报
    private readonly ConcurrentQueue<EventModel> _events = new();
    private readonly ConcurrentQueue<EventModel> _failEvents = new();
    private TimerX _eventTimer;
    private String _eventTraceId;

    /// <summary>批量上报事件</summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public async Task<Int32> PostEvents(params EventModel[] events) => await PostAsync<Int32>("Node/PostEvents", events);

    async Task DoPostEvent(Object state)
    {
        DefaultSpan.Current = null;
        var tid = _eventTraceId;
        _eventTraceId = null;

        // 正常队列为空，异常队列有数据，给它一次机会
        if (_events.IsEmpty && !_failEvents.IsEmpty)
        {
            while (_failEvents.TryDequeue(out var ev))
            {
                _events.Enqueue(ev);
            }
        }

        while (!_events.IsEmpty)
        {
            var max = 100;
            var list = new List<EventModel>();
            while (_events.TryDequeue(out var model) && max-- > 0) list.Add(model);

            using var span = Tracer?.NewSpan("PostEvent", list.Count);
            span?.Detach(tid);
            try
            {
                if (list.Count > 0) await PostEvents(list.ToArray());

                // 成功后读取本地缓存
                while (_failEvents.TryDequeue(out var ev))
                {
                    _events.Enqueue(ev);
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);

                // 失败后进入本地缓存
                foreach (var item in list)
                {
                    _failEvents.Enqueue(item);
                }
            }
        }
    }

    /// <summary>写事件</summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <param name="remark"></param>
    public virtual Boolean WriteEvent(String type, String name, String remark)
    {
        // 记录追踪标识，上报的时候带上，尽可能让源头和下游串联起来
        _eventTraceId = DefaultSpan.Current?.ToString();

        var now = DateTime.UtcNow;
        var ev = new EventModel { Time = now.ToLong(), Type = type, Name = name, Remark = remark };
        _events.Enqueue(ev);

        _eventTimer?.SetNext(1000);

        return true;
    }

    ///// <summary>写信息事件</summary>
    ///// <param name="name"></param>
    ///// <param name="remark"></param>
    //public virtual void WriteInfoEvent(String name, String remark) => WriteEvent("info", name, remark);

    ///// <summary>写错误事件</summary>
    ///// <param name="name"></param>
    ///// <param name="remark"></param>
    //public virtual void WriteErrorEvent(String name, String remark) => WriteEvent("error", name, remark);

    /// <summary>上报命令结果，如截屏、抓日志</summary>
    /// <param name="id"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    private async Task<Object> ReportAsync(Int32 id, Byte[] data) => await PostAsync<Object>("Node/Report?Id=" + id, data);

    /// <summary>上报服务调用结果</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public virtual async Task<Object> CommandReply(CommandReplyModel model) => await PostAsync<Object>("Node/CommandReply", model);
    #endregion

    #region 长连接
    private TimerX _timer;
    private void StartTimer()
    {
        if (_timer == null)
        {
            lock (this)
            {
                _timer ??= new TimerX(DoPing, null, 1_000, 60_000, "Device") { Async = true };
                _eventTimer = new TimerX(DoPostEvent, null, 3_000, 60_000, "Device") { Async = true };
            }
        }
    }

    private void StopTimer()
    {
        _timer.TryDispose();
        _timer = null;

        if (_websocket != null && _websocket.State == WebSocketState.Open) _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default).Wait();
        _source?.Cancel();

        //_websocket.TryDispose();
        _websocket = null;
    }

    private WebSocket _websocket;
    private CancellationTokenSource _source;
    private async Task DoPing(Object state)
    {
        DefaultSpan.Current = null;
        try
        {
            await Ping();

            var svc = _currentService;
            if (svc == null || Token == null) return;

            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
                var uri = new Uri(new Uri(url), "/node/notify");
                var client = new ClientWebSocket();
                client.Options.SetRequestHeader("Authorization", "Bearer " + Token);
                await client.ConnectAsync(uri, default);

                _websocket = client;

                _source = new CancellationTokenSource();
                _ = Task.Run(() => DoPull(client, _source.Token));
            }
        }
        catch (Exception ex)
        {
            Log?.Debug("{0}", ex);
        }
    }

    private async Task DoPull(WebSocket socket, CancellationToken cancellationToken)
    {
        DefaultSpan.Current = null;
        try
        {
            var buf = new Byte[4 * 1024];
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), cancellationToken);
                var model = buf.ToStr(null, 0, data.Count).ToJsonEntity<CommandModel>();
                if (model != null)
                {
                    // 埋点，建立调用链
                    using var span = Tracer?.NewSpan("OnReceiveCommand", model);
                    span?.Detach(model.TraceId);
                    try
                    {
                        XTrace.WriteLine("Got Command: {0}", model.ToJson());
                        if (model.Expire.Year < 2000 || model.Expire > DateTime.Now)
                        {
                            await OnReceiveCommand(model);
                        }
                    }
                    catch (Exception ex)
                    {
                        span?.SetError(ex, null);
                    }
                }
            }
        }
        catch (WebSocketException) { }
        catch (Exception ex)
        {
            Log?.Debug("{0}", ex);
            //XTrace.WriteException(ex);
        }

        if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
    }

    /// <summary>
    /// 触发收到命令的动作
    /// </summary>
    /// <param name="model"></param>
    protected virtual async Task OnReceiveCommand(CommandModel model)
    {
        var e = new CommandEventArgs { Model = model };
        Received?.Invoke(this, e);

        var rs = await this.ExecuteCommand(model);
        e.Reply ??= rs;

        if (e.Reply != null) await CommandReply(e.Reply);
    }
    #endregion

    #region 更新
    /// <summary>获取更新信息</summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public async Task<UpgradeInfo> Upgrade(String channel)
    {
        XTrace.WriteLine("检查更新：{0}", channel);

        // 清理
        var ug = new Stardust.Web.Upgrade { Log = XTrace.Log };
        ug.DeleteBackup(".");

        var rs = await UpgradeAsync(channel);
        if (rs != null)
        {
            XTrace.WriteLine("发现更新：{0}", rs.ToJson(true));
        }

        return rs;
    }

    /// <summary>更新</summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public async Task<UpgradeInfo> UpgradeAsync(String channel) => await GetAsync<UpgradeInfo>("Node/Upgrade", new { channel });
    #endregion

    #region 部署
    /// <summary>获取分配到本节点的应用服务信息</summary>
    /// <returns></returns>
    public async Task<DeployInfo[]> GetDeploy() => await GetAsync<DeployInfo[]>("Deploy/GetAll");

    /// <summary>上传本节点的所有应用服务信息</summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public async Task<Int32> UploadDeploy(ServiceInfo[] services) => await PostAsync<Int32>("Deploy/Upload", services);
    #endregion

    #region 辅助
    /// <summary>
    /// 把Url相对路径格式化为绝对路径。常用于文件下载
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public String BuildUrl(String url)
    {
        if (!url.StartsWithIgnoreCase("http://", "https://"))
        {
            var svr = Services.FirstOrDefault(e => e.Name == Source) ?? Services.FirstOrDefault();
            if (svr != null && svr.Address != null)
                url = new Uri(svr.Address, url) + "";
        }

        return url;
    }
    #endregion
}