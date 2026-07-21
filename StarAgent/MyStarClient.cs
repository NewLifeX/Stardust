using System.Diagnostics;
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
    protected override async Task OnPing(Object state)
    {
        await base.OnPing(state);

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
                    WriteLog("同步时间失败：{0}", ex.Message);
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
                WriteLog("同步时间间隔变更为：{0}秒", syncTime);

                set.SyncTime = syncTime;
                set.Save();
            }
        }

        return rs;
    }
    #endregion

    #region 更新
    public override Task<IUpgradeInfo?> Upgrade(String? channel, CancellationToken cancellationToken = default)
    {
        if (channel.IsNullOrEmpty()) channel = AgentSetting.Channel;

        return base.Upgrade(channel, cancellationToken);
    }

    protected override void Restart(Upgrade upgrade)
    {
        // 带有-s参数就算是服务中运行
        var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());
        var pid = Process.GetCurrentProcess().Id;

        // 使用当前进程可执行文件名，支持重命名场景（如 MyAgent.exe）
        var exeName = GetExecutableName();

        // 以服务方式运行时，重启服务，否则采取拉起进程的方式
        if (inService || Service.Host is DefaultHost host && host.InService)
        {
            this.WriteInfoEvent("Upgrade", "强制更新完成，准备重启后台服务！PID=" + pid);

            // 冒烟测试：尝试拉起新版，若失败等 5s 重试一次（应对慢速存储落盘 / JIT 预热）
            var rs = upgrade.Run(exeName, "-restart -upgrade", 3_000);
            if (!rs)
            {
                var delay = 5_000;
                this.WriteInfoEvent("Upgrade", $"新版首次启动失败，等待{delay}ms后重试。{upgrade.LastErrorMessage}");
                Thread.Sleep(delay);
                rs = upgrade.Run(exeName, "-restart -upgrade", 3_000);
            }

            //!! 这里不需要自杀，外部命令重启服务会结束当前进程
            if (rs)
            {
                this.WriteInfoEvent("Upgrade", "强制更新完成，新进程已拉起，等待当前服务被重启！");
            }
            else
            {
                // 两次均失败，确认新版有致命缺陷，回滚 .del 恢复旧版文件
                upgrade.Rollback();
                this.WriteInfoEvent("Upgrade", "新版启动失败（冒烟测试不通过），已回滚旧版文件，当前服务继续运行。" + upgrade.LastErrorMessage);
            }
        }
        else
        {
            this.WriteInfoEvent("Upgrade", "强制更新完成，准备拉起新进程！PID=" + pid);

            // 冒烟测试：尝试拉起新版，若失败等 5s 重试一次
            var rs = upgrade.Run(exeName, "-run -upgrade", 3_000);
            if (!rs)
            {
                var delay = 5_000;
                this.WriteInfoEvent("Upgrade", $"新版首次启动失败，等待{delay}ms后重试。{upgrade.LastErrorMessage}");
                Thread.Sleep(delay);
                rs = upgrade.Run(exeName, "-run -upgrade", 3_000);
            }

            if (rs)
            {
                Service.StopWork("Upgrade");

                this.WriteInfoEvent("Upgrade", "强制更新完成，新进程已拉起，准备退出当前进程！PID=" + pid);

                upgrade.KillSelf();
            }
            else
            {
                // 两次均失败，确认新版有致命缺陷，回滚 .del 恢复旧版文件
                upgrade.Rollback();
                this.WriteInfoEvent("Upgrade", "新版启动失败（冒烟测试不通过），已回滚旧版文件，当前服务继续运行。" + upgrade.LastErrorMessage);
            }
        }
    }

    /// <summary>获取当前进程可执行文件名（不含扩展名），支持重命名场景。dotnet/mono 宿主下返回真正目标程序集名</summary>
    private static String GetExecutableName() =>
        Process.GetCurrentProcess().GetProcessName() ?? "StarAgent";

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
        }, TaskCreationOptions.LongRunning);

        return "success";
    }

    /// <summary>设置通道</summary>
    /// <param name="argument"></param>
    /// <returns></returns>
    private String? SetChannel(String? argument)
    {
        if (argument.IsNullOrEmpty()) return "参数为空";

        var set = AgentSetting;
        set.Channel = argument;
        set.Save();

        return "success " + argument;
    }

    /// <summary>同步时间</summary>
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
        if (Math.Abs(ts.TotalMilliseconds) < 1000) return "无需同步";

        var msg = $"同步时间为：{time.ToFullString()}，偏差：{ts}";
        WriteLog(msg);
        WriteEvent("info", "SyncTime", msg);
        time = time.ToUniversalTime();

        var rs = "不支持的系统！";
        if (Runtime.Windows)
        {
            var st = new SYSTEMTIME
            {
                Year = (Int16)time.Year,
                Month = (Int16)time.Month,
                Day = (Int16)time.Day,
                Hour = (Int16)time.Hour,
                Minute = (Int16)time.Minute,
                Second = (Int16)time.Second,
                Milliseconds = (Int16)time.Millisecond
            };

            if (!SetSystemTime(ref st))
                throw new InvalidOperationException("Unable to set system time.");

            rs = "成功";
        }
        else if (Runtime.Linux)
        {
            rs = "date".Execute($"-u -s \"{time:yyyy-MM-dd HH:mm:ss}\"", 5_000);

            // 时间偏差较大时，需要写入RTC时钟，否则时间会被硬件时钟覆盖
            if (Math.Abs(ts.TotalSeconds) > 60)
            {
                rs += "，" + "hwclock".Execute("-u -w", 5_000);
            }
        }

        WriteLog(rs);

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
        if (argument.IsNullOrEmpty()) return "参数为空";

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

        if (cmd.IsNullOrEmpty()) return "命令为空";

        // 审计日志：记录命令执行前的信息
        var now = DateTime.Now;
        WriteLog("执行Bash命令：{0}，超时：{1}ms", cmd, timeout);
        WriteEvent("warn", "RunBash", $"执行命令：{cmd}，超时：{timeout}ms，时间：{now:yyyy-MM-dd HH:mm:ss}");

        var sw = Stopwatch.StartNew();
        try
        {
            var rs = "bash".Execute($"-c \"{cmd.Replace("\"", "\\\"")}\"", timeout);
            sw.Stop();

            // 审计日志：记录成功执行的详细信息（输出过长时截断）
            var resultPreview = rs?.Length > 1000 ? rs.Substring(0, 1000) + "..." : rs;
            WriteLog("执行成功，耗时：{0}ms，输出长度：{1}", sw.ElapsedMilliseconds, rs?.Length ?? 0);
            WriteEvent("info", "RunBash", $"执行成功，命令：{cmd}，耗时：{sw.ElapsedMilliseconds}ms，输出长度：{rs?.Length ?? 0}");

            return rs;
        }
        catch (Exception ex)
        {
            sw.Stop();

            // 审计日志：记录失败信息
            WriteLog("执行失败：{0}，耗时：{1}ms", ex.Message, sw.ElapsedMilliseconds);
            WriteEvent("error", "RunBash", $"执行失败，命令：{cmd}，耗时：{sw.ElapsedMilliseconds}ms，错误：{ex.Message}");

            return $"执行失败：{ex.Message}";
        }
    }

    /// <summary>执行CMD命令</summary>
    /// <param name="argument">命令参数。可以是纯字符串命令，或JSON格式{"cmd":"命令","timeout":超时毫秒}</param>
    /// <returns></returns>
    public String? RunCmd(String? argument)
    {
        if (argument.IsNullOrEmpty()) return "参数为空";

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

        if (cmd.IsNullOrEmpty()) return "命令为空";

        // 审计日志：记录命令执行前的信息
        var now = DateTime.Now;
        WriteLog("执行CMD命令：{0}，超时：{1}ms", cmd, timeout);
        WriteEvent("warn", "RunCmd", $"执行命令：{cmd}，超时：{timeout}ms，时间：{now:yyyy-MM-dd HH:mm:ss}");

        var sw = Stopwatch.StartNew();
        try
        {
            var rs = "cmd".Execute($"/c {cmd}", timeout);
            sw.Stop();

            // 审计日志：记录成功执行的详细信息（输出过长时截断）
            var resultPreview = rs?.Length > 1000 ? rs.Substring(0, 1000) + "..." : rs;
            WriteLog("执行成功，耗时：{0}ms，输出长度：{1}", sw.ElapsedMilliseconds, rs?.Length ?? 0);
            WriteEvent("info", "RunCmd", $"执行成功，命令：{cmd}，耗时：{sw.ElapsedMilliseconds}ms，输出长度：{rs?.Length ?? 0}");

            return rs;
        }
        catch (Exception ex)
        {
            sw.Stop();

            // 审计日志：记录失败信息
            WriteLog("执行失败：{0}，耗时：{1}ms", ex.Message, sw.ElapsedMilliseconds);
            WriteEvent("error", "RunCmd", $"执行失败，命令：{cmd}，耗时：{sw.ElapsedMilliseconds}ms，错误：{ex.Message}");

            return $"执行失败：{ex.Message}";
        }
    }
    #endregion
}
