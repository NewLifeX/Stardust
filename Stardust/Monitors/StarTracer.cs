using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using Stardust.Models;

namespace Stardust.Monitors
{
    /// <summary>星尘性能追踪器，追踪数据提交到星尘平台</summary>
    /// <remarks>其它项目有可能直接使用这个类代码，用于提交监控数据</remarks>
    public class StarTracer : DefaultTracer
    {
        #region 属性
        /// <summary>应用标识</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        ///// <summary>应用密钥</summary>
        //public String Secret { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认120</summary>
        public Int32 MaxFails { get; set; } = 120;

        /// <summary>要排除的操作名</summary>
        public String[] Excludes { get; set; }

        /// <summary>Api客户端</summary>
        public IApiClient Client { get; set; }

        private readonly String _version;
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly Queue<TraceModel> _fails = new Queue<TraceModel>();
        private AppInfo _appInfo;
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public StarTracer()
        {
            Period = 60;

            var set = Setting.Current;
            AppId = set.AppKey;
            //Secret = set.Secret;

            if (set.Debug) Log = XTrace.Log;

            try
            {
                var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
                var asm = AssemblyX.Entry ?? executing;
                if (asm != null)
                {
                    if (AppId == null) AppId = asm.Name;
                    AppName = asm.Title;
                    _version = asm.Version;
                }

                ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
            }
            catch { }
        }

        /// <summary>指定服务端地址来实例化追踪器</summary>
        /// <param name="server"></param>
        public StarTracer(String server) : this()
        {
            var http = new ApiHttpClient(server)
            {
                Tracer = this
            };
            Client = http;

            var set = Setting.Current;
            if (!AppId.IsNullOrEmpty() && !set.Secret.IsNullOrEmpty())
                http.Filter = new TokenHttpFilter { UserName = AppId, Password = set.Secret };
        }
        #endregion

        #region 核心业务
        //private TokenModel _token;
        //private DateTime _expire;
        //private void CheckAuthorize()
        //{
        //    if (_token == null || Client.Token.IsNullOrEmpty())
        //    {
        //        // 申请令牌
        //        _token = Client.Invoke<TokenModel>("OAuth/Token", new
        //        {
        //            grant_type = "password",
        //            username = AppId,
        //            password = Secret
        //        });
        //        Client.Token = _token.AccessToken;

        //        WriteLog("申请令牌：{0}", _token.AccessToken);

        //        // 提前一分钟过期
        //        _expire = DateTime.Now.AddSeconds(_token.ExpireIn - 600);
        //    }
        //    else if (_token != null && DateTime.Now > _expire)
        //    {
        //        // 刷新令牌
        //        _token = Client.Invoke<TokenModel>("OAuth/Token", new
        //        {
        //            grant_type = "refresh_token",
        //            refresh_token = _token.RefreshToken,
        //        });
        //        Client.Token = _token.AccessToken;

        //        WriteLog("刷新令牌：{0}", _token.AccessToken);
        //    }
        //}

        private Boolean _inited;
        private void Init()
        {
            if (_inited) return;

            // 自动从本地星尘代理获取地址
            if (Client == null)
            {
                try
                {
                    var client = new LocalStarClient();
                    var inf = client.GetInfo();
                    if (!inf.Server.IsNullOrEmpty()) Client = new ApiHttpClient(inf.Server);
                }
                catch { }
            }

            var server = Client is ApiHttpClient http ? http.Services.Join(",", e => e.Address) : (Client + "");
            WriteLog("星尘监控中心 Server={0} AppId={1} ClientId={2}", server, AppId, ClientId);

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
            if (_appInfo == null)
                _appInfo = new AppInfo(_process) { Version = _version };
            else
                _appInfo.Refresh();

            // 发送，失败后进入队列
            var model = new TraceModel
            {
                AppId = AppId,
                AppName = AppName,
                ClientId = ClientId,
                Version = _version,
                Info = _appInfo,

                Builders = builders
            };
            try
            {
                //// 检查令牌
                //if (!Secret.IsNullOrEmpty()) CheckAuthorize();

                var rs = Client.Invoke<TraceResponse>("Trace/Report", model);
                // 处理响应参数
                if (rs != null)
                {
                    if (rs.Period > 0) Period = rs.Period;
                    if (rs.MaxSamples > 0) MaxSamples = rs.MaxSamples;
                    if (rs.MaxErrors > 0) MaxErrors = rs.MaxErrors;
                    if (rs.Timeout > 0) Timeout = rs.Timeout;
                    Excludes = rs.Excludes;
                }
            }
            catch (ApiException ex)
            {
                //if (ex.Code == 401 || ex.Code == 403) _token = null;

                Log?.Error(ex + "");
            }
            catch (Exception ex)
            {
                //if (ex is ApiException ae && (ae.Code == 401 || ae.Code == 403)) _token = null;

                if (ex.GetTrue() is not HttpRequestException)
                    Log?.Error(ex + "");

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

        #region 方法
        /// <summary>全局注入</summary>
        public void AttachGlobal()
        {
            DefaultTracer.Instance = this;
            ApiHelper.Tracer = this;

#if NET50
            // 订阅Http事件
            var observer = new DiagnosticListenerObserver { Tracer = this };
            observer.Subscribe(new HttpDiagnosticListener());
#endif

            // 反射处理XCode追踪
            {
                var type = "XCode.DataAccessLayer.DAL".GetTypeEx(false);
                var pi = type?.GetPropertyEx("GlobalTracer");
                if (pi != null && pi.PropertyType == typeof(ITracer)) pi.SetValue(null, this, null);
            }

            // 反射处理Cube追踪
            {
                var type = "NewLife.Cube.WebMiddleware.TracerMiddleware".GetTypeEx(false);
                var pi = type?.GetPropertyEx("Tracer");
                if (pi != null && pi.PropertyType == typeof(ITracer)) pi.SetValue(null, this, null);
            }

            // 反射处理Star追踪
            {
                var type = "Stardust.Extensions.TracerMiddleware".GetTypeEx(false);
                var pi = type?.GetPropertyEx("Tracer");
                if (pi != null && pi.PropertyType == typeof(ITracer)) pi.SetValue(null, this, null);
            }
        }
        #endregion

        #region 全局注册
        /// <summary>全局注册星尘性能追踪器</summary>
        /// <param name="server">星尘监控中心地址，为空时自动从本地探测</param>
        /// <returns></returns>
        public static StarTracer Register(String server = null)
        {
            if (server.IsNullOrEmpty())
            {
                var set = Setting.Current;
                server = set.Server;
            }
            if (server.IsNullOrEmpty())
            {
                var local = new LocalStarClient();
                var inf = local.GetInfo();
                server = inf?.Server;

                if (!server.IsNullOrEmpty()) XTrace.WriteLine("星尘探测：{0}", server);
            }
            if (server.IsNullOrEmpty()) return null;

            if (Instance is StarTracer tracer && tracer.Client is ApiHttpClient) return tracer;

            tracer = new StarTracer(server) { Log = XTrace.Log };
            tracer.AttachGlobal();

            return tracer;
        }
        #endregion
    }
}