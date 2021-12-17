using System;
using System.IO;
using System.Threading.Tasks;
using NewLife;
using NewLife.Configuration;
using NewLife.Http;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using Stardust.Models;
using Stardust.Monitors;

namespace Stardust
{
    /// <summary>星尘工厂</summary>
    /// <remarks>
    /// 星尘代理 https://www.yuque.com/smartstone/blood/staragent_install
    /// 监控中心 https://www.yuque.com/smartstone/blood/stardust_monitor
    /// 配置中心 https://www.yuque.com/smartstone/blood/stardust_configcenter
    /// </remarks>
    public class StarFactory : DisposeBase
    {
        #region 属性
        /// <summary>服务器地址</summary>
        public String Server { get; set; }

        /// <summary>应用</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

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
        /// <summary>
        /// 实例化星尘工厂，先后读取appsettings.json、本地StarAgent、star.config
        /// </summary>
        public StarFactory() => Init();

        /// <summary>实例化星尘工厂，指定地址、应用和密钥，创建工厂</summary>
        /// <param name="server">服务端地址。为空时先后读取appsettings.json、本地StarAgent、star.config</param>
        /// <param name="appId">应用标识。为空时读取star.config</param>
        /// <param name="secret">应用密钥。为空时读取star.config</param>
        /// <returns></returns>
        public StarFactory(String server, String appId, String secret)
        {
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
        private Boolean Valid()
        {
            //if (Server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));
            //if (AppId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(AppId));

            if (Server.IsNullOrEmpty() || AppId.IsNullOrEmpty()) return false;

            if (_client == null)
            {
                if (!AppId.IsNullOrEmpty()) _tokenFilter = new TokenHttpFilter
                {
                    UserName = AppId,
                    Password = Secret,
                };

                _client = new ApiHttpClient(Server) { Filter = _tokenFilter };

                var set = StarSetting.Current;
                if (set.Debug) _client.Log = XTrace.Log;
            }

            return true;
        }
        #endregion

        #region 本地代理
        /// <summary>本地星尘代理</summary>
        public LocalStarClient Local { get; private set; }

        private void Init()
        {
            Local = new LocalStarClient();

            // 读取本地appsetting
            if (Server.IsNullOrEmpty() && File.Exists("appsettings.Development.json".GetFullPath()))
            {
                using var json = new JsonConfigProvider { FileName = "appsettings.Development.json" };
                json.LoadAll();

                Server = json["StarServer"];
            }
            if (Server.IsNullOrEmpty() && File.Exists("appsettings.json".GetFullPath()))
            {
                using var json = new JsonConfigProvider { FileName = "appsettings.json" };
                json.LoadAll();

                Server = json["StarServer"];
            }

            if (!Server.IsNullOrEmpty() && Local.Server.IsNullOrEmpty()) Local.Server = Server;

            try
            {
                var inf = Local.GetInfo();
                var server = inf?.Server;
                if (!server.IsNullOrEmpty())
                {
                    if (Server.IsNullOrEmpty()) Server = server;
                    XTrace.WriteLine("星尘探测：{0}", server);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("星尘探测失败！{0}", ex.Message);
            }

            // 如果探测不到本地应用，则使用配置
            var set = StarSetting.Current;
            if (Server.IsNullOrEmpty()) Server = set.Server;
            if (AppId.IsNullOrEmpty()) AppId = set.AppKey;
            if (Secret.IsNullOrEmpty()) Secret = set.Secret;

            var asm = AssemblyX.Entry;
            if (asm != null)
            {
                if (AppId.IsNullOrEmpty()) AppId = asm.Name;
                if (AppName.IsNullOrEmpty()) AppName = asm.Title;
            }

            XTrace.WriteLine("星尘分布式服务 Server={0} AppId={1}", Server, AppId);

            var ioc = ObjectContainer.Current;
            ioc.AddSingleton(this);
            ioc.AddSingleton(p => Tracer);
            ioc.AddSingleton(p => Config);
            ioc.AddSingleton(p => Service);
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
                    if (!Valid()) return null;

                    XTrace.WriteLine("初始化星尘监控中心，采样并定期上报应用性能埋点数据，包括Api接口、Http请求、数据库操作、Redis操作等");

                    var tracer = new StarTracer(Server)
                    {
                        AppId = AppId,
                        AppName = AppName,
                        //Secret = Secret,
                        Client = _client,

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
        /// <remarks>
        /// 文档 https://www.yuque.com/smartstone/blood/stardust_configcenter
        /// </remarks>
        public IConfigProvider Config
        {
            get
            {
                if (_config == null)
                {
                    if (!Valid()) return null;

                    XTrace.WriteLine("初始化星尘配置中心，提供集中配置管理能力，自动从配置中心加载配置数据");

                    var config = new HttpConfigProvider
                    {
                        Server = Server,
                        AppId = AppId,
                        //Secret = Secret,
                        Client = _client,
                    };
                    //config.LoadAll();

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
                    if (!Valid()) return null;

                    XTrace.WriteLine("初始化星尘注册中心，提供服务注册与发布能力");

                    var client = new DustClient(Server)
                    {
                        AppId = AppId,
                        //Secret = Secret,
                        Client = _client,
                    };

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
                    var addrs = item.Address.Split(",");
                    foreach (var elm in addrs)
                    {
                        client.Services.Add(new ApiHttpClient.Service
                        {
                            Name = item.Client,
                            Address = new Uri(elm),
                            Weight = item.Weight,
                        });
                    }
                }
            }
        }
        #endregion

        #region 其它
        /// <summary>发送节点命令</summary>
        /// <param name="nodeCode"></param>
        /// <param name="command"></param>
        /// <param name="argument"></param>
        /// <param name="expire"></param>
        /// <returns></returns>
        public async Task<Int32> SendNodeCommand(String nodeCode, String command, String argument = null, Int32 expire = 3600)
        {
            if (!Valid()) return -1;

            return await _client.PostAsync<Int32>("Node/SendCommand", new { nodeCode, command, argument, expire });
        }
        #endregion

        #region 日志
        /// <summary>日志。默认 XTrace.Log</summary>
        public ILog Log { get; set; } = XTrace.Log;
        #endregion
    }
}