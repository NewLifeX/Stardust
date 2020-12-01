using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;
using Stardust.Services;

namespace Stardust
{
    /// <summary>星星客户端</summary>
    public class StarClient : ApiHttpClient
    {
        #region 属性
        /// <summary>证书</summary>
        public String Code { get; set; }

        /// <summary>密钥</summary>
        public String Secret { get; set; }

        /// <summary>是否已登录</summary>
        public Boolean Logined { get; set; }

        /// <summary>登录完成后触发</summary>
        public event EventHandler OnLogined;

        /// <summary>最后一次登录成功后的消息</summary>
        public LoginResponse Info { get; private set; }

        /// <summary>请求到服务端并返回的延迟时间。单位ms</summary>
        public Int32 Delay { get; set; }

        /// <summary>命令队列</summary>
        public IQueueService<CommandModel> CommandQueue { get; } = new QueueService<CommandModel>();
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

            base.Dispose(disposing);
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

            if (Logined && _timer == null)
            {
                lock (this)
                {
                    if (_timer == null)
                    {
                        _timer = new TimerX(s => Ping().Wait(), null, 5_000, 60_000, "Device") { Async = true };
                    }
                }
            }

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
            var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).OrderBy(e => e).Join(",");
            var driveInfo = new DriveInfo(Path.GetPathRoot(".".GetFullPath()));
            var di = new NodeInfo
            {
                Version = asm?.FileVersion,
                Compile = asm?.Compile ?? DateTime.MinValue,

                OSName = mi.OSName,
                OSVersion = mi.OSVersion,

                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                IP = NetHelper.GetIPsWithCache().Where(ip => ip.IsIPv4() && !IPAddress.IsLoopback(ip) && ip.GetAddressBytes()[0] != 169).Join(),

                ProcessorCount = Environment.ProcessorCount,
                Memory = mi.Memory,
                AvailableMemory = mi.AvailableMemory,
                TotalSize = (UInt64)driveInfo.TotalSize,
                AvailableFreeSpace = (UInt64)driveInfo.AvailableFreeSpace,

                Processor = mi.Processor,
                CpuID = mi.CpuID,
                CpuRate = mi.CpuRate,
                UUID = mi.UUID,
                MachineGuid = mi.Guid,
                DiskID = mi.DiskID,

                Macs = mcs,
                //COMs = ps.Join(","),

                InstallPath = ".".GetFullPath(),
                Runtime = Environment.Version + "",

                Time = DateTime.UtcNow,
            };

#if !__CORE__
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

            return di;
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

                Logined = false;

                return rs;
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);

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

        #region 心跳报告
        private readonly String[] _excludes = new[] { "Idle", "System", "Registry", "smss", "csrss", "lsass", "wininit", "services", "winlogon", "fontdrvhost", "dwm", "svchost", "dllhost", "conhost", "taskhostw", "explorer", "ctfmon", "ChsIME", "WmiPrvSE", "WUDFHost", "igfxCUIServiceN", "igfxEMN", "sihost", "RuntimeBroker", "StartMenuExperienceHost", "SecurityHealthSystray", "SecurityHealthService", "ShellExperienceHost", "PerfWatson2", "audiodg" };

        /// <summary>获取心跳信息</summary>
        public PingInfo GetHeartInfo()
        {
            var ps = Process.GetProcesses();
            var pcs = new List<String>();
            foreach (var item in ps)
            {
                // 有些进程可能已退出，无法获取详细信息
                try
                {
                    var name = item.ProcessName;
                    if (name.EqualIgnoreCase(_excludes)) continue;

                    if (!pcs.Contains(name)) pcs.Add(name);
                }
                catch { }
            }

            var mi = MachineInfo.Current ?? _task.Result;
            mi.Refresh();

            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = properties.GetActiveTcpConnections();

            var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).OrderBy(e => e).Join(",");
            var driveInfo = new DriveInfo(Path.GetPathRoot(".".GetFullPath()));
            var ext = new PingInfo
            {
                AvailableMemory = mi.AvailableMemory,
                AvailableFreeSpace = (UInt64)driveInfo.AvailableFreeSpace,
                CpuRate = mi.CpuRate,
                Temperature = mi.Temperature,
                ProcessCount = ps.Length,
                TcpConnections = connections.Count(e => e.State == TcpState.Established),
                TcpTimeWait = connections.Count(e => e.State == TcpState.TimeWait),
                TcpCloseWait = connections.Count(e => e.State == TcpState.CloseWait),
                Uptime = Environment.TickCount / 1000,

                Macs = mcs,
                //COMs = ps.Join(","),

                Processes = pcs.Join(),

                Time = DateTime.UtcNow.ToLong(),
                Delay = Delay,
            };
#if __CORE__
            //ext.Uptime = Environment.TickCount64 / 1000;
#endif
            // 开始时间 Environment.TickCount 很容易溢出，导致开机24天后变成负数。
            // 后来在 netcore3.0 增加了Environment.TickCount64
            // 现在借助 Stopwatch 来解决
            if (Stopwatch.IsHighResolution) ext.Uptime = (Int32)(Stopwatch.GetTimestamp() / Stopwatch.Frequency);

            return ext;
        }

        //[DllImport("kernel32.dll")]
        //private static extern UInt64 GetTickCount64();

        private TimerX _timer;
        /// <summary>心跳</summary>
        /// <returns></returns>
        public async Task<Object> Ping()
        {
            //XTrace.WriteLine("心跳");

            var inf = GetHeartInfo();

            try
            {
                var rs = await PingAsync(inf);
                if (rs != null)
                {
                    // 由服务器改变采样频率
                    if (rs.Period > 0) _timer.Period = rs.Period * 1000;

                    var dt = rs.Time.ToDateTime();
                    if (dt.Year > 2000)
                    {
                        // 计算延迟
                        var ts = DateTime.UtcNow - dt;
                        var ms = (Int32)ts.TotalMilliseconds;
                        if (Delay > 0)
                            Delay = (Delay + ms) / 2;
                        else
                            Delay = ms;
                    }

                    // 推队列
                    if (rs.Commands != null && rs.Commands.Length > 0)
                    {
                        foreach (var item in rs.Commands)
                        {
                            CommandQueue.Public(item.Command, item);
                        }
                    }
                }

                return rs;
            }
            catch (Exception ex)
            {
                var ex2 = ex.GetTrue();
                if (ex2 is ApiException aex && (aex.Code == 402 || aex.Code == 403))
                {
                    XTrace.WriteLine("重新登录");
                    return Login();
                }

                XTrace.WriteLine("心跳异常 {0}", (String)ex.GetTrue().Message);

                throw;
            }
        }

        /// <summary>心跳</summary>
        /// <param name="inf"></param>
        /// <returns></returns>
        private async Task<PingResponse> PingAsync(PingInfo inf) => await PostAsync<PingResponse>("Node/Ping", inf);

        private TraceService _trace;
        /// <summary>使用跟踪服务</summary>
        public void UseTrace()
        {
            _trace = new TraceService
            {
                Queue = CommandQueue,
                Callback = (id, data) => ReportAsync(id, data).Wait(),
            };
            _trace.Init();
        }

        /// <summary>上报命令结果，如截屏、抓日志</summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task<Object> ReportAsync(Int32 id, Byte[] data) => await PostAsync<Object>("Device/Report?Id=" + id, data);
        #endregion

        #region 更新
        /// <summary>获取更新信息</summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public async Task<UpgradeInfo> Upgrade(String channel)
        {
            XTrace.WriteLine("检查更新：{0}", channel);

            var rs = await UpgradeAsync(channel);
            if (rs != null)
            {
                XTrace.WriteLine("发现更新：{0}", rs.ToJson(true));
            }

            return rs;
        }

        /// <summary>执行更新</summary>
        /// <param name="ur"></param>
        /// <returns></returns>
        public Boolean ProcessUpgrade(UpgradeInfo ur)
        {
            XTrace.WriteLine("执行更新：{0} {1}", ur.Version, ur.Source);

            var dest = ".";
            var url = ur.Source;

            try
            {
                // 需要下载更新包
                if (!url.IsNullOrEmpty())
                {
                    var fileName = Path.GetFileName(url);
                    if (!fileName.EndsWithIgnoreCase(".zip")) fileName = Rand.NextString(8) + ".zip";
                    fileName = "Update".CombinePath(fileName).EnsureDirectory(true);

                    // 清理
                    NewLife.Net.Upgrade.DeleteBuckup(dest);

                    // 下载
                    var sw = Stopwatch.StartNew();
                    var client = new HttpClient();
                    client.DownloadFileAsync(url, fileName).Wait();

                    sw.Stop();
                    XTrace.WriteLine("下载 {0} 到 {1} 完成，耗时 {2} 。", url, fileName, sw.Elapsed);

                    // 解压
                    var source = fileName.TrimEnd(".zip");
                    if (Directory.Exists(source)) Directory.Delete(source, true);
                    source.EnsureDirectory(false);
                    fileName.AsFile().Extract(source, true);

                    // 覆盖
                    NewLife.Net.Upgrade.CopyAndReplace(source, dest);
                    if (Directory.Exists(source)) Directory.Delete(source, true);
                }

                // 升级处理命令，可选
                var cmd = ur.Executor?.Trim();
                if (!cmd.IsNullOrEmpty())
                {
                    XTrace.WriteLine("执行更新命令：{0}", cmd);

                    var si = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                    };
                    var p = cmd.IndexOf(' ');
                    if (p < 0)
                        si.FileName = cmd;
                    else
                    {
                        si.FileName = cmd.Substring(0, p);
                        si.Arguments = cmd.Substring(p + 1);
                    }

                    Process.Start(si);
                }

                return true;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("更新失败！");
                XTrace.WriteException(ex);

                return false;
            }
        }

        /// <summary>更新</summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public async Task<UpgradeInfo> UpgradeAsync(String channel) => await GetAsync<UpgradeInfo>("Node/Upgrade", new { channel });
        #endregion
    }
}