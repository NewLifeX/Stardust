using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NewLife;
using NewLife.Common;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using Stardust.Models;

namespace Stardust.Monitors
{
    /// <summary>星尘性能跟踪器，跟踪数据提交到星尘平台</summary>
    /// <remarks>其它项目有可能直接使用这个类代码，用于提交监控数据</remarks>
    public class StarTracer : DefaultTracer
    {
        #region 属性
        /// <summary>应用标识</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        /// <summary>应用密钥</summary>
        public String AppSecret { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认120</summary>
        public Int32 MaxFails { get; set; } = 120;

        /// <summary>要排除的操作名</summary>
        public String[] Excludes { get; set; }

        /// <summary>Api客户端</summary>
        public IApiClient Client { get; set; }

        private String _version;
        private Process _process = Process.GetCurrentProcess();
        private readonly Queue<TraceModel> _fails = new Queue<TraceModel>();
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public StarTracer()
        {
            Period = 60;

            var sys = SysConfig.Current;
            //AppId = sys.Name;
            AppName = sys.DisplayName;

            var set = Setting.Current;
            AppId = set.AppKey;
            AppSecret = set.Secret;

            if (set.Debug) Log = XTrace.Log;

            try
            {
                var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
                if (executing != null) _version = executing.Version;

                var asm = AssemblyX.Entry ?? executing;
                ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}@{asm?.Version}";
            }
            catch { }
        }

        /// <summary>指定服务端地址来实例化跟踪器</summary>
        /// <param name="server"></param>
        public StarTracer(String server) : this()
        {
            var http = new ApiHttpClient(server)
            {
                Tracer = this
            };
            Client = http;
        }
        #endregion

        #region 核心业务
        private TokenModel _token;
        private DateTime _expire;
        private void CheckAuthorize()
        {
            if (_token == null && Client.Token.IsNullOrEmpty())
            {
                // 申请令牌
                _token = Client.Invoke<TokenModel>("OAuth/Token", new
                {
                    grant_type = "password",
                    username = AppId,
                    password = AppSecret
                });
                Client.Token = _token.AccessToken;

                // 提前一分钟过期
                _expire = DateTime.Now.AddSeconds(_token.ExpireIn - 60);
            }
            else if (_token != null && DateTime.Now > _expire)
            {
                // 刷新令牌
                _token = Client.Invoke<TokenModel>("OAuth/Token", new
                {
                    grant_type = "refresh_token",
                    refresh_token = _token.RefreshToken,
                });
                Client.Token = _token.AccessToken;
            }
        }

        private Boolean _inited;
        private void Init()
        {
            if (_inited) return;

            var server = Client is ApiHttpClient http ? http.Services.Join(",", e => e.Address) : (Client + "");
            WriteLog("StarTracer.Start AppId={0} ClientId={1} Server={2}", AppId, ClientId, server);

            _inited = true;
        }

        /// <summary>处理Span集合。默认输出日志，可重定义输出控制台</summary>
        protected override void ProcessSpans(ISpanBuilder[] builders)
        {
            if (builders == null) return;

            // 剔除项
            if (Excludes != null) builders = builders.Where(e => !Excludes.Any(y => y.IsMatch(e.Name))).ToArray();
            builders = builders.Where(e => !e.Name.EndsWithIgnoreCase("/Trace/Report")).ToArray();
            if (builders.Length == 0) return;

            // 初始化
            Init();

            // 构建应用信息
            var info = new AppInfo(_process);

            try
            {
                // 调用WindowApi获取进程的连接数
                var tcps = NetHelper.GetAllTcpConnections();
                if (tcps != null && tcps.Length > 0)
                {
                    var pid = Process.GetCurrentProcess().Id;
                    info.Connections = tcps.Count(e => e.ProcessId == pid);
                }
            }
            catch { }

            // 发送，失败后进入队列
            var model = new TraceModel
            {
                AppId = AppId,
                AppName = AppName,
                ClientId = ClientId,
                Version = _version,
                Info = info,

                Builders = builders
            };
            try
            {
                // 检查令牌
                if (!AppSecret.IsNullOrEmpty()) CheckAuthorize();

                var rs = Client.Invoke<TraceResponse>("Trace/Report", model);
                // 处理响应参数
                if (rs != null)
                {
                    if (rs.Period > 0) Period = rs.Period;
                    if (rs.MaxSamples > 0) MaxSamples = rs.MaxSamples;
                    if (rs.MaxErrors > 0) MaxErrors = rs.MaxErrors;
                    if (rs.Timeout > 0) Timeout = rs.Timeout;
                    if (rs.Excludes != null) Excludes = rs.Excludes;
                }
            }
            catch (ApiException ex)
            {
                Log?.Error(ex + "");
            }
            catch (Exception ex)
            {
                //XTrace.WriteException(ex);
                Log?.Error(ex + "");
                //throw;

                if (_fails.Count < MaxFails) _fails.Enqueue(model);
                return;
            }

            // 如果发送成功，则继续发送以前失败的数据
            while (_fails.Count > 0)
            {
                model = _fails.Dequeue();
                try
                {
                    Client.Invoke<Object>("Trace/Report", model);
                }
                catch (ApiException ex)
                {
                    Log?.Error(ex + "");
                }
                catch (Exception ex)
                {
                    XTrace.WriteLine("二次上报失败，放弃该批次采样数据，{0}", model.Builders.FirstOrDefault()?.StartTime.ToDateTime());
                    XTrace.WriteException(ex);
                    //Log?.Error(ex + "");

                    // 星尘收集器上报，二次失败后放弃该批次数据，因其很可能是错误数据
                    //_fails.Enqueue(model);
                    break;
                }
            }
        }
        #endregion

        #region 全局注册
        /// <summary>全局注册星尘性能跟踪器</summary>
        /// <param name="server">星尘监控中心地址</param>
        /// <returns></returns>
        public static StarTracer Register(String server)
        {
            if (server.IsNullOrEmpty()) return null;

            if (Instance is StarTracer tracer && tracer.Client is ApiHttpClient) return tracer;

            tracer = new StarTracer(server) { Log = XTrace.Log };
            DefaultTracer.Instance = tracer;

            return tracer;
        }
        #endregion
    }
}