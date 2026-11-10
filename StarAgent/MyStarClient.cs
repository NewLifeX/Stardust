using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace StarAgent;

/// <summary>实例化</summary>
/// <param name="set"></param>
internal class MyStarClient(StarAgentSetting set) : StarClient(set)
{
    #region 属性
    //public IHost Host { get; set; }

    public ServiceBase Service { get; set; } = null!;

    public StarAgentSetting AgentSetting { get; set; } = set;

    ///// <summary>项目名。新节点默认所需要加入的项目</summary>
    //public String Project { get; set; }

    private Boolean InService
    {
        get
        {
            var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());
            if (inService) return true;

            // 以服务方式运行时，重启服务，否则采取拉起进程的方式
            if (Service != null && Service.Host is DefaultHost host && host.InService) return true;

            return false;
        }
    }
    #endregion

    #region 方法
    protected override void OnInit()
    {
        var provider = ServiceProvider ??= ObjectContainer.Provider;

        // 找到容器，注册默认的模型实现，供后续InvokeAsync时自动创建正确的模型对象
        var container = ModelExtension.GetService<IObjectContainer>(provider) ?? ObjectContainer.Current;
        if (container != null)
        {
            container.AddTransient<IPingResponse, MyPingResponse>();
        }

        base.OnInit();
    }
    #endregion

    #region 登录
    public override void Open()
    {
        this.RegisterCommand("node/restart", Restart);
        this.RegisterCommand("node/reboot", Reboot);
        this.RegisterCommand("node/setchannel", SetChannel);
        this.RegisterCommand("node/synctime", SyncTime);
        this.RegisterCommand("bash", RunBash);
        this.RegisterCommand("cmd", RunCmd);

        base.Open();
    }

    public override ILoginRequest BuildLoginRequest()
    {
        var set = AgentSetting;
        var request = base.BuildLoginRequest();
        if (request is LoginInfo req)
        {
            req.Project = set.Project;

            var info = req.Node;
            if (info != null && InService)
            {
                if (!set.Dpi.IsNullOrEmpty()) info.Dpi = set.Dpi;
                if (!set.Resolution.IsNullOrEmpty()) info.Resolution = set.Resolution;
            }
        }

        return request;
    }
    #endregion

    #region 心跳
    private DateTime _lastSync;
    private DateTime _lastClockWarn;
    protected override async Task OnPing(Object state)
    {
        await base.OnPing(state);

        // 本机时钟偏差超过盐值窗口时，登录会因盐值过期失败。客户端已改用服务端时间生成盐值（无需修改系统时钟），
        // 这里只上报一条限流告警，提示运维修 NTP——改系统时钟是特权操作，容器内还会影响宿主机与同机所有服务。
        if (Span.TotalMilliseconds != 0 && Math.Abs(Span.TotalMilliseconds) > 60_000 && _lastClockWarn.AddHours(6) < DateTime.Now)
        {
            _lastClockWarn = DateTime.Now;

            var msg = L.TLog("Clock skew {0:n0}ms exceeds the salt window; fix NTP on this node.", Span.TotalMilliseconds);
            // 事件名与 Stardust.Data.HistoryAction.ClockSkew 保持一致，便于平台按动作筛选与翻译（StarAgent 不引用 Stardust.Data）
            this.WriteEvent("warn", "Clock skew", msg);
            WriteLog(msg);
        }

        // 心跳成功后上报本地事件台账，上报失败的事件留在台账等下一次心跳
        await EventLedger.ReportAsync(this, EventLedger.GetDirectory(), EventLedger.NodeEvents, "StarAgentReportFailed");

        var syncTime = AgentSetting.SyncTime;
        if (syncTime > 0 && Span.TotalMilliseconds != 0)
        {
            var now = DateTime.Now;
            if (_lastSync.AddSeconds(syncTime) < now)
            {
                _lastSync = now;

                try
                {
                    // 同步时间
                    SyncTime();
                }
                catch (Exception ex)
                {
                    WriteLog("Time synchronization failed: {0}", ex.Message);
                }
            }
        }
    }

    public override async Task<IPingResponse?> Ping(CancellationToken cancellationToken = default)
    {
        var rs = await base.Ping(cancellationToken);
        if (rs is MyPingResponse mpr)
        {
            var set = AgentSetting;
            var syncTime = mpr.SyncTime;
            if (syncTime > 0 && syncTime != set.SyncTime)
            {
                WriteLog("Time synchronization interval changed to {0} second(s)", syncTime);

                set.SyncTime = syncTime;
                set.Save();
            }
        }

        return rs;
    }
    #endregion

    #region 更新
    private static Boolean _aotUpgradeWarned;

    /// <summary>是否以 NativeAOT 方式运行（无动态代码能力）</summary>
    private static Boolean IsNativeAot
    {
        get
        {
#if NET5_0_OR_GREATER
            return !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
#else
            return false;
#endif
        }
    }

    public override Task<IUpgradeInfo?> Upgrade(String? channel, CancellationToken cancellationToken = default)
    {
        if (channel.IsNullOrEmpty()) channel = AgentSetting.Channel;

        return base.Upgrade(channel, cancellationToken);
    }

    /// <summary>向服务端查询升级信息。在基类纯查询前后输出本地版本、服务端返回与对比结论，让日志能直接回答「要不要更新」</summary>
    /// <remarks>周期轮询与 node/upgrade 命令两条路径都经由 Upgrade→UpgradeAsync，此处一处覆盖。下载/覆盖/回滚仍由基类 Upgrade 处理，不受影响。</remarks>
    /// <param name="channel">更新通道</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务端升级信息，无更新时为空</returns>
    protected override async Task<IUpgradeInfo?> UpgradeAsync(String? channel, CancellationToken cancellationToken = default)
    {
        // NativeAOT 单体程序不能被平台下发的托管程序集包覆盖（覆盖后会静默退回 JIT 版），自动更新直接跳过
        if (IsNativeAot)
        {
            if (!_aotUpgradeWarned)
            {
                _aotUpgradeWarned = true;
                WriteLog("NativeAOT build: automatic upgrade is disabled because the platform distributes managed assembly packages that cannot replace a native executable. Deploy the native package manually instead.");
            }

            return null;
        }

        var local = GetLocalVersion();
        WriteLog("Upgrade check: local version={0}, channel={1}, querying the server...", local, channel);

        var info = await base.UpgradeAsync(channel, cancellationToken);
        if (info == null || info.Version.IsNullOrEmpty())
        {
            WriteLog("Upgrade check result: no update available on the server (local version={0}, channel={1}). If an update is expected, verify the product version/release package on the platform: enabled, channel matches, product code matches, and the release package target runtime matches the node runtime.", local, channel);
            return info;
        }

        var same = info.Version.EqualIgnoreCase(local);
        var status = same ? L.TLog("Version is the same; no update needed") : L.TLog("Update required; preparing to download");
        if (info is UpgradeInfo ui)
            WriteLog("Upgrade check result: latest server version={0}, local version={1}, {2}. Source={3}, size={4}, MD5={5}, Force={6}",
                info.Version, local, status,
                info.Source, ui.FileSize, info.FileHash, ui.Force);
        else
            WriteLog("Upgrade check result: latest server version={0}, local version={1}, {2}. Source={3}, MD5={4}",
                info.Version, local, status,
                info.Source, info.FileHash);

        return info;
    }

    private String? _localVersion;
    /// <summary>本地版本。取主程序集文件版本（如 3.7.2026.1103），与节点登录上报及平台「产品版本」的 Version 同源</summary>
    /// <returns></returns>
    private String GetLocalVersion()
    {
        if (_localVersion.IsNullOrEmpty())
        {
            var asm = typeof(MyStarClient).Assembly;
            _localVersion = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                ?? asm.GetName().Version?.ToString();
        }

        return _localVersion!;
    }

    protected override void Restart(Upgrade upgrade)
    {
        // 带有-s参数就算是服务中运行
        var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());
        var pid = Process.GetCurrentProcess().Id;

        // 使用当前进程可执行文件名，支持重命名场景（如 MyAgent.exe）
        var exeName = GetExecutableName();

        // 诊断埋点：记录 exeName 和进程信息，排查更新后重启失败问题
        var p = Process.GetCurrentProcess();
        this.WriteInfoEvent("Upgrade", $"Restart: exeName={exeName} ProcessName={p.ProcessName} MainModule={p.MainModule?.FileName}");

        // 以服务方式运行时，重启服务，否则采取拉起进程的方式
        if (inService || Service.Host is DefaultHost host && host.InService)
        {
            this.WriteInfoEvent("Upgrade", L.TLog("Forced update completed; restarting the background service. PID={0}", pid));

            // 冒烟测试：尝试拉起新版，若失败等 5s 重试一次（应对慢速存储落盘 / JIT 预热）
            var rs = upgrade.Run(exeName, "-restart -upgrade", 3_000);
            if (!rs)
            {
                var delay = 5_000;
                this.WriteInfoEvent("Upgrade", L.TLog("The new version failed to start for the first time; retrying in {0}ms. {1}", delay, upgrade.LastErrorMessage));
                Thread.Sleep(delay);
                rs = upgrade.Run(exeName, "-restart -upgrade", 3_000);
            }

            //!! 这里不需要自杀，外部命令重启服务会结束当前进程
            if (rs)
            {
                var msg = L.TLog("Forced update completed; the new process has been launched, waiting for the current service to restart.");
                this.WriteInfoEvent("Upgrade", msg);
                ConfirmUpgrade(msg);
            }
            else if (TryRestartService(upgrade))
            {
                // 服务管理器重启不依赖可执行文件名，是名称解析失败时的兜底通道
            }
            else
            {
                // 两次均失败，确认新版有致命缺陷，回滚 .del 恢复旧版文件
                upgrade.Rollback();
                FailUpgrade(L.TLog("The new version failed to start (smoke test failed); the old files have been rolled back and the current service keeps running. {0}", upgrade.LastErrorMessage));
            }
        }
        else
        {
            this.WriteInfoEvent("Upgrade", L.TLog("Forced update completed; launching the new process. PID={0}", pid));

            // 冒烟测试：尝试拉起新版，若失败等 5s 重试一次
            var rs = upgrade.Run(exeName, "-run -upgrade", 3_000);
            if (!rs)
            {
                var delay = 5_000;
                this.WriteInfoEvent("Upgrade", L.TLog("The new version failed to start for the first time; retrying in {0}ms. {1}", delay, upgrade.LastErrorMessage));
                Thread.Sleep(delay);
                rs = upgrade.Run(exeName, "-run -upgrade", 3_000);
            }

            if (rs)
            {
                Service.StopWork("Upgrade");

                var msg = L.TLog("Forced update completed; the new process has been launched and the current process is about to exit. PID={0}", pid);
                this.WriteInfoEvent("Upgrade", msg);
                ConfirmUpgrade(msg);

                upgrade.KillSelf();
            }
            else if (TryRestartService(upgrade))
            {
                // 进程模式下拉起失败时，若本机存在后台服务，改由服务管理器重启
            }
            else
            {
                // 两次均失败，确认新版有致命缺陷，回滚 .del 恢复旧版文件
                upgrade.Rollback();
                FailUpgrade(L.TLog("The new version failed to start (smoke test failed); the old files have been rolled back and the current service keeps running. {0}", upgrade.LastErrorMessage));
            }
        }
    }

    /// <summary>确认升级完成。写入本地事件台账，由心跳上报服务端，停止按退避间隔重复下发</summary>
    /// <remarks>
    /// 强制升级需要重启进程才算完成，基类不会在覆盖文件后上报 success；重启后上报的版本号又可能因为
    /// 安装包内容与当前版本一致（节点本来就在目标版本）而保持不变，服务端据此判定升级未完成，
    /// 按 10min/30min/2h/6h/24h 反复下发同一份安装包。
    /// <para>确认口径：拉起的新版进程存活 3 秒（冒烟测试通过）即视为升级成功——U 与新版是同一份二进制，
    /// 它能起来就说明宿主与入口点可用，内部流程由发版测试保证。冒烟失败时回滚并写 error，服务端立即重试。</para>
    /// <para>写入时机必须在冒烟通过之后、交接之前：拉起前写只证明“文件复制完成”，U 可能根本起不来；
    /// 新版进程的 -upgrade 等待 6 秒再重启服务，为这里留出确定的落盘窗口，避免事件被重启打断。</para>
    /// </remarks>
    /// <param name="message">事件内容</param>
    private static void ConfirmUpgrade(String message) => EventLedger.Append(EventLedger.GetDirectory(), EventLedger.NodeEvents, "success", "Upgrade", message);

    /// <summary>升级失败。写入本地事件台账，与 success 同批上报时最终结果为失败，服务端会清空指纹并允许立即重试</summary>
    /// <param name="message">失败原因</param>
    private static void FailUpgrade(String message) => EventLedger.Append(EventLedger.GetDirectory(), EventLedger.NodeEvents, "error", "Upgrade", message);

    /// <summary>兜底重启服务</summary>
    /// <remarks>
    /// 更新已经把新文件覆盖到位（旧文件改名为 .del），此时拉起新进程失败多半是名称/路径解析问题，
    /// 由服务管理器重启不依赖可执行文件名，能直接把服务切到新版本。
    /// 只在目标目录确实存在新主程序时才尝试，避免新版文件残缺导致服务反复拉起失败。
    /// </remarks>
    /// <param name="upgrade">升级对象</param>
    /// <returns>是否已交给服务管理器重启</returns>
    private Boolean TryRestartService(Upgrade upgrade)
    {
        try
        {
            var name = GetExecutableName();
            var dest = upgrade.DestinationPath;
            if (dest.IsNullOrEmpty()) return false;

            var exe = Path.Combine(dest, name + ".exe");
            var dll = Path.Combine(dest, name + ".dll");
            if (!File.Exists(exe) && !File.Exists(dll)) return false;

            var host = Service?.Host;
            if (host == null) return false;

            this.WriteInfoEvent("Upgrade", L.TLog("Failed to launch the new process; falling back to restarting the background service. {0}", upgrade.LastErrorMessage));
            WriteLog("Fallback: restart service {0}", Service.ServiceName);

            host.Restart(Service.ServiceName);
            return true;
        }
        catch (Exception ex)
        {
            this.WriteInfoEvent("Upgrade", L.TLog("Failed to restart the background service: {0}", ex.Message));
            return false;
        }
    }

    /// <summary>获取当前进程可执行文件名（不含扩展名），支持重命名场景。dotnet/mono 宿主下返回真正目标程序集名</summary>
    /// <remarks>
    /// 升级过程会把正在使用的 exe/dll 改名为 *.del，此后 <see cref="Process.ProcessName"/> 会带上 .del 后缀
    /// （实测为 StarAgent.exe.del），据此拼路径得到 StarAgent.exe.del.exe，必然找不到文件，导致升级完成后无法拉起新进程。
    /// 入口程序集名是编译期确定的，与磁盘文件改名无关，故优先使用，进程名仅作兜底。
    /// </remarks>
    private static String GetExecutableName()
    {
        var name = Assembly.GetEntryAssembly()?.GetName().Name;
        if (name.IsNullOrEmpty()) name = Process.GetCurrentProcess().GetProcessName();
        if (name.IsNullOrEmpty()) return "StarAgent";

        return name.TrimSuffix(".exe", ".dll", ".del");
    }

    /// <summary>使用当前进程可执行文件完整路径启动新进程，避免依赖工作目录中的文件名</summary>
    private static Boolean RunCurrentProcess(String args)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var exePath = process.MainModule?.FileName;
            if (exePath.IsNullOrEmpty()) return false;

            var arguments = args;

            // 兼容 dotnet/mono 启动模式（Linux 常见：dotnet StarAgent.dll）
            // 参考 ProcessHelper.GetProcessName 的检测逻辑
            var pname = process.ProcessName;
            if (pname.EqualIgnoreCase("dotnet", "dotnet.exe") || pname?.EndsWith("/dotnet") == true)
            {
                var dll = Environment.GetCommandLineArgs()[0].GetFullPath();
                if (dll.Contains(' ')) dll = $"\"{dll}\"";
                arguments = $"{dll} {args}";
            }
            else if (pname.EqualIgnoreCase("mono", "mono.exe", "mono-sgen"))
            {
                exePath = Environment.GetCommandLineArgs()[0].GetFullPath();
            }

            var si = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
            };

            return Process.Start(si) != null;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return false;
        }
    }
    #endregion

    #region 扩展功能
    /// <summary>重启应用服务</summary>
    private String? Restart(String? argument)
    {
        // 异步执行，让方法调用返回结果给服务端
        Task.Factory.StartNew(() =>
        {
            Thread.Sleep(1000);

            var upgrade = new Upgrade { Log = XTrace.Log };

            // 带有-s参数就算是服务中运行
            var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());

            // 以服务方式运行时，重启服务，否则采取拉起进程的方式
            if (inService || Service.Host is DefaultHost host && host.InService)
            {
                // 使用当前进程可执行文件路径重启服务，避免硬编码进程名导致重命名后失效
                var rs = RunCurrentProcess("-restart -delay");

                //!! 这里不需要自杀，外部命令重启服务会结束当前进程
                return rs + "";
            }
            else
            {
                // 使用当前进程可执行文件路径重新拉起进程
                var rs = RunCurrentProcess("-run -delay");
                if (rs)
                {
                    Service.StopWork("Upgrade");

                    upgrade.KillSelf();
                }

                return rs + "";
            }
        }, TaskCreationOptions.LongRunning);

        return "success";
    }

    /// <summary>重启操作系统</summary>
    private String? Reboot(String? argument)
    {
        var dic = argument.IsNullOrEmpty() ? null : JsonParser.Decode(argument);
        var timeout = dic?["timeout"].ToInt();

        // 异步执行，让方法调用返回结果给服务端
        Task.Factory.StartNew(() =>
        {
            Thread.Sleep(1000);

            if (Runtime.Windows)
            {
                if (timeout > 0)
                    "shutdown".ShellExecute($"-r -t {timeout}");
                else
                    "shutdown".ShellExecute($"-r");

                Thread.Sleep(5000);
                "shutdown".ShellExecute($"-r -f");
            }
            else if (Runtime.Linux)
            {
                // 重启Linux之前先同步数据到硬盘
                "sync".ShellExecute();

                // 多种方式重启Linux，先使用温和的方式
                "systemctl".ShellExecute("reboot");

                Thread.Sleep(5000);
                "shutdown".ShellExecute("-r now");

                Thread.Sleep(5000);
                "reboot".ShellExecute();
            }
            else if (Runtime.OSX)
            {
                // 先同步数据到硬盘，再以非交互方式重启，避免等待 sudo 密码
                "sync".ShellExecute();
                "sudo".ShellExecute("-n shutdown -r now");
            }
        }, TaskCreationOptions.LongRunning);

        return "success";
    }

    /// <summary>设置通道</summary>
    /// <param name="argument"></param>
    /// <returns></returns>
    private String? SetChannel(String? argument)
    {
        if (argument.IsNullOrEmpty()) return L.T("Parameter is empty.");

        var set = AgentSetting;
        set.Channel = argument;
        set.Save();

        return "success " + argument;
    }

    /// <summary>同步时间</summary>
    /// <remarks>
    /// 仅由平台/人工下发的 node/synctime 命令触发，代理不再自动修改系统时钟。
    /// 鉴权已改用服务端时间生成盐值（<see cref="ServerTimePasswordProvider"/>），本机时钟偏差不会再导致登录失败。
    /// </remarks>
    /// <param name="argument">指令参数。可指定目标时间</param>
    /// <returns>执行结果</returns>
    public String? SyncTime(String? argument = null)
    {
        var now = DateTime.Now;
        var time = GetNow();

        // 指令传达的参数可能指定了时间
        if (!argument.IsNullOrEmpty())
        {
            var dt = argument.ToDateTime();
            if (dt.Year > 2000 && dt.Year < 3000) time = dt;
        }

        var ts = now - time;
        if (Math.Abs(ts.TotalMilliseconds) < 1000) return L.T("No synchronization required.");

        var msg = L.TLog("Synchronized time: {0}, offset: {1}", time.ToFullString(), ts);
        WriteLog(msg);

        var utc = time.ToUniversalTime();
        var rs = L.T("Unsupported system!");
        if (Runtime.Windows)
        {
            var st = new SYSTEMTIME
            {
                Year = (Int16)utc.Year,
                Month = (Int16)utc.Month,
                Day = (Int16)utc.Day,
                Hour = (Int16)utc.Hour,
                Minute = (Int16)utc.Minute,
                Second = (Int16)utc.Second,
                Milliseconds = (Int16)utc.Millisecond
            };

            if (!SetSystemTime(ref st))
                throw new InvalidOperationException("Unable to set system time.");

            rs = L.T("Succeeded");
        }
        else if (Runtime.Linux)
        {
            rs = "date".Execute($"-u -s \"{utc:yyyy-MM-dd HH:mm:ss}\"", 5_000);
        }
        else if (Runtime.OSX)
        {
            // macOS 修改系统时间需要 root 权限，-n 禁止交互式密码输入，避免命令挂起
            rs = "sudo".Execute($"-n date -u -s \"{utc:yyyy-MM-dd HH:mm:ss}\"", 5_000);
            if (rs.IsNullOrEmpty()) rs = L.T("Synchronization failed; root permission is required. Run manually: sudo date -u -s \"{0}\"", utc.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        // 以“时钟是否真的被改到目标时间”为准。date/sudo 失败时只返回错误文本而不抛异常，不能据此当成功
        var ok = Math.Abs((DateTime.Now - time).TotalSeconds) < 5;

        // 时间偏差较大时，需要写入RTC时钟，否则时间会被硬件时钟覆盖
        if (ok && Runtime.Linux && Math.Abs(ts.TotalSeconds) > 60)
        {
            rs += ", " + "hwclock".Execute("-u -w", 5_000);
        }

        WriteEvent(ok ? "info" : "error", "SyncTime", ok ? msg : L.TLog("Time synchronization failed: {0}", rs));
        WriteLog("{0}", rs);

        return rs;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Boolean SetSystemTime(ref SYSTEMTIME st);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public Int16 Year;
        public Int16 Month;
        public Int16 DayOfWeek;
        public Int16 Day;
        public Int16 Hour;
        public Int16 Minute;
        public Int16 Second;
        public Int16 Milliseconds;
    }

    /// <summary>执行Bash命令</summary>
    /// <param name="argument">命令参数。可以是纯字符串命令，或JSON格式{"cmd":"命令","timeout":超时毫秒}</param>
    /// <returns></returns>
    public String? RunBash(String? argument)
    {
        if (argument.IsNullOrEmpty()) return L.T("Parameter is empty.");

        var cmd = argument;
        var timeout = 30_000;

        // 尝试解析JSON格式参数
        if (argument.StartsWith("{"))
        {
            try
            {
                var dic = JsonParser.Decode(argument);
                if (dic != null)
                {
                    cmd = dic["cmd"] + "";
                    if (dic.ContainsKey("timeout")) timeout = dic["timeout"].ToInt();
                }
            }
            catch
            {
                // 解析失败则当作普通命令处理
            }
        }

        if (cmd.IsNullOrEmpty()) return L.T("Command is empty.");

        // 审计日志：记录命令执行前的信息
        var now = DateTime.Now;
        WriteLog("Executing Bash command: {0}, timeout: {1}ms", cmd, timeout);
        WriteEvent("warn", "RunBash", L.TLog("Executing command: {0}, timeout: {1}ms, time: {2}", cmd, timeout, now.ToString("yyyy-MM-dd HH:mm:ss")));

        var sw = Stopwatch.StartNew();
        try
        {
            var rs = "bash".Execute($"-c \"{cmd.Replace("\"", "\\\"")}\"", timeout);
            sw.Stop();

            // 审计日志：记录成功执行的详细信息（输出过长时截断）
            var resultPreview = rs?.Length > 1000 ? rs.Substring(0, 1000) + "..." : rs;
            WriteLog("Execution succeeded, elapsed: {0}ms, output length: {1}", sw.ElapsedMilliseconds, rs?.Length ?? 0);
            WriteEvent("info", "RunBash", L.TLog("Execution succeeded, command: {0}, elapsed: {1}ms, output length: {2}", cmd, sw.ElapsedMilliseconds, rs?.Length ?? 0));

            return rs;
        }
        catch (Exception ex)
        {
            sw.Stop();

            // 审计日志：记录失败信息
            WriteLog("Execution failed: {0}, elapsed: {1}ms", ex.Message, sw.ElapsedMilliseconds);
            WriteEvent("error", "RunBash", L.TLog("Execution failed, command: {0}, elapsed: {1}ms, error: {2}", cmd, sw.ElapsedMilliseconds, ex.Message));

            return L.T("Command execution failed: {0}", ex.Message);
        }
    }

    /// <summary>执行CMD命令</summary>
    /// <param name="argument">命令参数。可以是纯字符串命令，或JSON格式{"cmd":"命令","timeout":超时毫秒}</param>
    /// <returns></returns>
    public String? RunCmd(String? argument)
    {
        if (argument.IsNullOrEmpty()) return L.T("Parameter is empty.");

        var cmd = argument;
        var timeout = 30_000;

        // 尝试解析JSON格式参数
        if (argument.StartsWith("{"))
        {
            try
            {
                var dic = JsonParser.Decode(argument);
                if (dic != null)
                {
                    cmd = dic["cmd"] + "";
                    if (dic.ContainsKey("timeout")) timeout = dic["timeout"].ToInt();
                }
            }
            catch
            {
                // 解析失败则当作普通命令处理
            }
        }

        if (cmd.IsNullOrEmpty()) return L.T("Command is empty.");

        // 审计日志：记录命令执行前的信息
        var now = DateTime.Now;
        WriteLog("Executing CMD command: {0}, timeout: {1}ms", cmd, timeout);
        WriteEvent("warn", "RunCmd", L.TLog("Executing command: {0}, timeout: {1}ms, time: {2}", cmd, timeout, now.ToString("yyyy-MM-dd HH:mm:ss")));

        var sw = Stopwatch.StartNew();
        try
        {
            var rs = "cmd".Execute($"/c {cmd}", timeout);
            sw.Stop();

            // 审计日志：记录成功执行的详细信息（输出过长时截断）
            var resultPreview = rs?.Length > 1000 ? rs.Substring(0, 1000) + "..." : rs;
            WriteLog("Execution succeeded, elapsed: {0}ms, output length: {1}", sw.ElapsedMilliseconds, rs?.Length ?? 0);
            WriteEvent("info", "RunCmd", L.TLog("Execution succeeded, command: {0}, elapsed: {1}ms, output length: {2}", cmd, sw.ElapsedMilliseconds, rs?.Length ?? 0));

            return rs;
        }
        catch (Exception ex)
        {
            sw.Stop();

            // 审计日志：记录失败信息
            WriteLog("Execution failed: {0}, elapsed: {1}ms", ex.Message, sw.ElapsedMilliseconds);
            WriteEvent("error", "RunCmd", L.TLog("Execution failed, command: {0}, elapsed: {1}ms, error: {2}", cmd, sw.ElapsedMilliseconds, ex.Message));

            return L.T("Command execution failed: {0}", ex.Message);
        }
    }
    #endregion
}