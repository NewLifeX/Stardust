using System;
using NewLife;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Monitors;

namespace Stardust
{
    /// <summary>星尘工厂</summary>
    public class StarFactory
    {
        #region 属性
        /// <summary>服务器地址</summary>
        public String Server { get; set; }

        /// <summary>应用</summary>
        public String AppId { get; set; }

        /// <summary>应用密钥</summary>
        public String Secret { get; set; }
        #endregion

        #region 构造
        /// <summary>创建工厂</summary>
        /// <param name="appId">应用</param>
        /// <returns></returns>
        public StarFactory(String appId)
        {
            AppId = appId;
            //var set = Setting.Current;
            //Server = set.Server;
            //AppId = set.AppKey;
            //Secret = set.Secret;

            Init();
        }

        ///// <summary>指定地址、应用和密钥，创建工厂</summary>
        ///// <param name="server"></param>
        ///// <param name="appId"></param>
        ///// <param name="secrect"></param>
        ///// <returns></returns>
        //public StarFactory(String server, String appId, String secrect)
        //{
        //    Server = server;
        //    AppId = appId;
        //    Secret = secrect;

        //    Init();
        //}
        #endregion

        #region 方法
        private void Valid()
        {
            if (Server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));
            if (AppId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(AppId));
        }
        #endregion

        #region 本地代理
        /// <summary>本地星尘代理</summary>
        public LocalStarClient Local { get; private set; }

        private void Init()
        {
            Local = new LocalStarClient();

            // 读取本地appsetting
            if (Server.IsNullOrEmpty())
            {
                var json = new JsonConfigProvider { FileName = "appsettings.json" };
                json.LoadAll();

                Server = json["StarServer"];
            }

            if (Server.IsNullOrEmpty())
            {
                try
                {
                    var inf = Local.GetInfo();
                    var server = inf?.Server;
                    if (!server.IsNullOrEmpty())
                    {
                        Server = server;
                        XTrace.WriteLine("星尘探测：{0}", server);
                    }
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }

            // 如果探测不到本地应用，则使用配置
            if (Server.IsNullOrEmpty())
            {
                var set = Setting.Current;
                Server = set.Server;
            }
            if (AppId.IsNullOrEmpty())
            {
                var set = Setting.Current;
                AppId = set.AppKey;
                Secret = set.Secret;
            }
        }
        #endregion

        #region 监控中心
        private StarTracer _tracer;
        /// <summary>监控中心</summary>
        public ITracer Tracer
        {
            get
            {
                if (_tracer == null)
                {
                    Valid();

                    var tracer = new StarTracer(Server)
                    {
                        AppId = AppId,
                        Secret = Secret,

                        Log = Log
                    };
                    tracer.AttachGlobal();

                    _tracer = tracer;
                }

                return _tracer;
            }
        }
        #endregion

        #region 配置中心
        private HttpConfigProvider _config;
        /// <summary>配置中心</summary>
        public IConfigProvider Config
        {
            get
            {
                if (_config == null)
                {
                    Valid();

                    var http = new HttpConfigProvider
                    {
                        Server = Server,
                        AppId = AppId,
                        Secret = Secret,
                    };
                    http.LoadAll();

                    _config = http;
                }

                return _config;
            }
        }
        #endregion

        #region 注册中心
        private DustClient _dustClient;
        /// <summary>注册中心</summary>
        public DustClient Dust
        {
            get
            {
                if (_dustClient == null)
                {
                    Valid();

                    var client = new DustClient(Server)
                    {
                        AppId = AppId,
                        Secret = Secret,

                        Log = Log,
                    };
                    client.OnLogined += (s, e) =>
                    {
                        if (_tracer.Client is ApiHttpClient client) client.Token = _dustClient.Token;
                        //if (_configProvider.Client is ApiHttpClient client) client.Token = _dustClient.Token;
                    };

                    _dustClient = client;
                }

                return _dustClient;
            }
        }
        #endregion

        #region 日志
        /// <summary>日志</summary>
        public ILog Log { get; set; } = XTrace.Log;
        #endregion
    }
}