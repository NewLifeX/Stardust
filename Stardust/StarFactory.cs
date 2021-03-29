using System;
using NewLife;
using NewLife.Configuration;
using NewLife.Http;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using Stardust.Models;
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

        /// <summary>服务名</summary>
        public String ServiceName { get; set; }
        #endregion

        #region 构造
        /// <summary>指定地址、应用和密钥，创建工厂</summary>
        /// <param name="server"></param>
        /// <param name="appId"></param>
        /// <param name="secret"></param>
        /// <returns></returns>
        public StarFactory(String server, String appId, String secret)
        {
            if (appId.IsNullOrEmpty()) appId = AssemblyX.Entry.Name;

            Server = server;
            AppId = appId;
            Secret = secret;

            Init();
        }
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

        private TokenHttpFilter _tokenFilter;

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

            if (!AppId.IsNullOrEmpty()) _tokenFilter = new TokenHttpFilter
            {
                UserName = AppId,
                Password = Secret,
            };

            XTrace.WriteLine("星尘分布式服务 Server={0} AppId={1}", Server, AppId);
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
                    if (tracer.Client is ApiHttpClient http) http.Filter = _tokenFilter;

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

                    XTrace.WriteLine("星尘配置中心 Server={0} AppId={1}", Server, AppId);

                    var http = new HttpConfigProvider
                    {
                        Server = Server,
                        AppId = AppId,
                        Secret = Secret,
                    };
                    if (http.Client is ApiHttpClient http2) http2.Filter = _tokenFilter;
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

                        Filter = _tokenFilter,
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

        /// <summary>为指定服务创建客户端，从星尘注册中心获取服务地址</summary>
        /// <param name="serviceName"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public IApiClient CreateForService(String serviceName, String tag = null)
        {
            //var ms = Dust.Resolve(serviceName, null, tag);

            var client = new ApiHttpClient
            {
                RoundRobin = true,

                Tracer = Tracer,
            };

            Bind(client, Dust.Resolve(serviceName, null, tag));
            Dust.Bind(serviceName, (k, ms) => Bind(client, ms));

            return client;
        }

        private void Bind(ApiHttpClient client, ServiceModel[] ms)
        {
            if (ms != null && ms.Length > 0)
            {
                foreach (var item in ms)
                {
                    //client.Add(item.Client, item.Address);
                    client.Services.Add(new ApiHttpClient.Service
                    {
                        Name = item.Client,
                        Address = new Uri(item.Address),
                        Weight = item.Weight,
                    });
                }
            }
        }
        #endregion

        #region 日志
        /// <summary>日志</summary>
        public ILog Log { get; set; } = XTrace.Log;
        #endregion
    }
}