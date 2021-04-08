using System;
using System.Collections.Generic;
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
    public class StarFactory : DisposeBase
    {
        #region 属性
        /// <summary>服务器地址</summary>
        public String Server { get; set; }

        /// <summary>应用</summary>
        public String AppId { get; set; }

        /// <summary>应用密钥</summary>
        public String Secret { get; set; }

        ///// <summary>服务名</summary>
        //public String ServiceName { get; set; }

        /// <summary>客户端</summary>
        public IApiClient Client => _client;

        private ApiHttpClient _client;
        private TokenHttpFilter _tokenFilter;
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

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            _tracer.TryDispose();
            _config.TryDispose();
            _dustClient.TryDispose();
        }
        #endregion

        #region 方法
        private void Valid()
        {
            if (Server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));
            if (AppId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(AppId));

            if (_client == null)
            {
                if (!AppId.IsNullOrEmpty()) _tokenFilter = new TokenHttpFilter
                {
                    UserName = AppId,
                    Password = Secret,
                };

                _client = new ApiHttpClient(Server) { Filter = _tokenFilter };
            }
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
            var set = Setting.Current;
            if (Server.IsNullOrEmpty()) Server = set.Server;
            if (AppId.IsNullOrEmpty()) AppId = set.AppKey;
            if (Secret.IsNullOrEmpty()) Secret = set.Secret;

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
                        //Secret = Secret,
                        Client = _client,

                        Log = Log
                    };
                    //if (tracer.Client is ApiHttpClient http) http.Filter = _tokenFilter;

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

                    var config = new HttpConfigProvider
                    {
                        Server = Server,
                        AppId = AppId,
                        //Secret = Secret,
                        Client = _client,
                    };
                    config.LoadAll();
                    //// 需要使用一次以后，才能够得到Client实例
                    //if (config.Client is ApiHttpClient http) http.Filter = _tokenFilter;

                    _config = config;
                }

                return _config;
            }
        }
        #endregion

        #region 注册中心
        private DustClient _dustClient;
        /// <summary>注册中心，服务注册与发现</summary>
        public DustClient Service
        {
            get
            {
                if (_dustClient == null)
                {
                    Valid();

                    var client = new DustClient(Server)
                    {
                        AppId = AppId,
                        //Secret = Secret,
                        Client = _client,

                        //Filter = _tokenFilter,
                        //Log = Log,
                    };
                    //client.OnLogined += (s, e) =>
                    //{
                    //    if (_tracer.Client is ApiHttpClient client) client.Token = _dustClient.Token;
                    //    //if (_configProvider.Client is ApiHttpClient client) client.Token = _dustClient.Token;
                    //};

                    _dustClient = client;
                }

                return _dustClient;
            }
        }

        //private IDictionary<String, IApiClient> _services = new Dictionary<String, IApiClient>();
        /// <summary>为指定服务创建客户端，从星尘注册中心获取服务地址。单例，应避免频繁创建客户端</summary>
        /// <param name="serviceName"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public IApiClient CreateForService(String serviceName, String tag = null)
        {
            //if (_services.TryGetValue(serviceName, out var client)) return client;

            var http = new ApiHttpClient
            {
                RoundRobin = true,

                Tracer = Tracer,
            };

            var models = Service.ResolveAsync(serviceName, null, tag).Result;

            Bind(http, models);

            Service.Bind(serviceName, (k, ms) => Bind(http, ms));

            //_services[serviceName] = http;

            return http;
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