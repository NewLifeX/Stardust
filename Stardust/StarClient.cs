using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
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
        #endregion

        #region 登录
        /// <summary>登录</summary>
        /// <returns></returns>
        public async Task<Object> Login()
        {
            XTrace.WriteLine("登录：{0}", Code);

            var info = GetLoginInfo();

            Logined = false;

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

            if (Logined && _timer == null)
            {
                lock (this)
                {
                    if (_timer == null)
                    {
                        _timer = new TimerX(s => Ping(), null, 5_000, 60_000, "Device") { Async = true };
                    }
                }
            }

            return Info;
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
            var di = new NodeInfo
            {
                Version = asm.FileVersion,
                Compile = asm.Compile,

                OSName = mi.OSName,
                OSVersion = mi.OSVersion,

                MachineName = Environment.MachineName,
                UserName = Environment.UserName,

                ProcessorCount = Environment.ProcessorCount,
                Memory = mi.Memory,
                AvailableMemory = mi.AvailableMemory,
                Processor = mi.Processor,
                CpuID = mi.CpuID,
                CpuRate = mi.CpuRate,
                UUID = mi.UUID,
                MachineGuid = mi.Guid,

                Macs = mcs,
                //COMs = ps.Join(","),

                InstallPath = ".".GetFullPath(),
                Runtime = Environment.Version + "",

                Time = DateTime.Now,
            };

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
        /// <summary>获取心跳信息</summary>
        public PingInfo GetHeartInfo()
        {
            var asm = AssemblyX.Entry;
            //var ps = System.IO.Ports.SerialPort.GetPortNames();
            var pcs = new List<Process>();
            foreach (var item in Process.GetProcesses().OrderBy(e => e.SessionId).ThenBy(e => e.ProcessName))
            {
                var name = item.ProcessName;
                if (name.EqualIgnoreCase("svchost", "dllhost", "conhost")) continue;

                if (!pcs.Contains(item)) pcs.Add(item);
            }

            var mi = MachineInfo.Current;
            mi.Refresh();

            var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).OrderBy(e => e).Join(",");
            var ext = new PingInfo
            {
                AvailableMemory = mi.AvailableMemory,
                CpuRate = mi.CpuRate,

                Macs = mcs,
                //COMs = ps.Join(","),

                Processes = pcs.Join(",", e => e.ProcessName),

                Time = DateTime.Now.ToLong(),
                Delay = Delay,
            };

            return ext;
        }

        private TimerX _timer;
        /// <summary>心跳</summary>
        /// <returns></returns>
        public async Task<Object> Ping()
        {
            XTrace.WriteLine("心跳");

            var inf = GetHeartInfo();

            try
            {
                var rs = await PingAsync(inf);
                if (rs != null)
                {
                    var dt = rs.Time.ToDateTime();
                    if (dt.Year > 2000)
                    {
                        // 计算延迟
                        var ts = DateTime.Now - dt;
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
                if (ex is AggregateException agg)
                {
                    if (agg.InnerExceptions[0] is ApiException aex && aex.Code == 402)
                    {
                        XTrace.WriteLine("重新登录");
                        return Login();
                    }
                }

                XTrace.WriteLine("心跳异常 {0}", (String)ex.GetTrue().Message);

                throw;
            }
        }

        /// <summary>心跳</summary>
        /// <param name="inf"></param>
        /// <returns></returns>
        private async Task<PingResponse> PingAsync(PingInfo inf) => await PostAsync<PingResponse>("Node/Ping", inf);

        /// <summary>上报命令结果，如截屏、抓日志</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task<Object> ReportAsync(Int32 id, Byte[] data) => await PostAsync<Object>("Device/Report?Id=" + id, data);
        #endregion

        #region 更新
        /// <summary>更新</summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public async Task<UpgradeInfo> UpgradeAsync(String channel) => await GetAsync<UpgradeInfo>("Node/Upgrade", new { channel });
        #endregion   
    }
}