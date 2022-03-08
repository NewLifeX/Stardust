using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NewLife;
using NewLife.Configuration;
using NewLife.Http;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Security;
using Stardust.Configs;
using Stardust.Models;
using Stardust.Monitors;
using Stardust.Registry;

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

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        ///// <summary>服务名</summary>
        //public String ServiceName { get; set; }

        /// <summary>客户端</summary>
        public IApiClient Client => _client;

        /// <summary>应用客户端</summary>
        public AppClient App => _client;

        /// <summary>配置信息。从配置中心返回的信息头</summary>
        public ConfigInfo ConfigInfo => (_config as StarHttpConfigProvider)?.ConfigInfo;

        /// <summary>本地星尘代理</summary>
        public LocalStarClient Local { get; private set; }

        private AppClient _client;
        private TokenHttpFilter _tokenFilter;
        //private AppClient _appClient;
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
            //_appClient.TryDispose();
        }

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

            // 借助本地StarAgent获取服务器地址
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

            // 生成ClientId，用于唯一标识当前实例，默认IP@pid
            try
            {
                var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
                var asm = AssemblyX.Entry ?? executing;
                if (asm != null)
                {
                    if (AppId.IsNullOrEmpty()) AppId = asm.Name;
                    if (AppName.IsNullOrEmpty()) AppName = asm.Title;
                }

                ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
            }
            catch
            {
                ClientId = Rand.NextString(8);
            }

            XTrace.WriteLine("星尘分布式服务 Server={0} AppId={1} ClientId={2}", Server, AppId, ClientId);

            Valid();

            var ioc = ObjectContainer.Current;
            ioc.AddSingleton(this);
            ioc.AddSingleton(p => Tracer);
            ioc.AddSingleton(p => Config);
            ioc.AddSingleton(p => Service);
        }

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
                    ClientId = ClientId,
                };

                var client = new AppClient(Server)
                {
                    AppId = AppId,
                    AppName = AppName,
                    ClientId = ClientId,
                    NodeCode = Local?.Info?.Code,
                    Filter = _tokenFilter
                };

                var set = StarSetting.Current;
                if (set.Debug) client.Log = XTrace.Log;

                //var tracer = new StarTracer(Server)
                //{
                //    AppId = AppId,
                //    AppName = AppName,
                //    ClientId = ClientId,
                //    //Client = _client,

                //    Log = Log
                //};
                //client.Tracer = tracer;
                //tracer.Client = client;

                //tracer.AttachGlobal();

                client.Start();

                //_appClient = client;
                _client = client;
            }

            return true;
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

                    XTrace.WriteLine("初始化星尘监控中心，采样并定期上报应用性能埋点数据，包括Api接口、Http请求、数据库操作、Redis操作等。可用于监控系统健康状态，分析分布式系统的性能瓶颈。");

                    var tracer = new StarTracer(Server)
                    {
                        AppId = AppId,
                        AppName = AppName,
                        //Secret = Secret,
                        ClientId = ClientId,
                        Client = _client,

                        Log = Log
                    };
                    _client.Tracer = tracer;

                    tracer.AttachGlobal();
                    _tracer = tracer;
                    //_tracer = _client.Tracer as StarTracer;
                }

                return _tracer;
            }
        }
        #endregion

        #region 配置中心
        private HttpConfigProvider _config;
        /// <summary>配置中心。务必在数据库操作和生成雪花Id之前使用激活</summary>
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

                    XTrace.WriteLine("初始化星尘配置中心，提供集中配置管理能力，自动从配置中心加载配置数据，包括XCode数据库连接。配置中心同时支持分配应用实例的唯一WorkerId，确保Snowflake算法能够生成绝对唯一的雪花Id");

                    var config = new StarHttpConfigProvider
                    {
                        Server = Server,
                        AppId = AppId,
                        //Secret = Secret,
                        ClientId = ClientId,
                        Client = _client,
                    };
                    //if (!ClientId.IsNullOrEmpty()) config.ClientId = ClientId;
                    config.Attach(_client);
                    config.LoadAll();

                    _config = config;
                }

                return _config;
            }
        }
        #endregion

        #region 注册中心
        private Boolean _initService;
        /// <summary>注册中心，服务注册与发现</summary>
        public IRegistry Service
        {
            get
            {
                if (!_initService)
                {
                    if (!Valid()) return null;

                    _initService = true;
                    //_appClient = _client as AppClient;

                    XTrace.WriteLine("初始化星尘注册中心，提供服务注册与发布能力");
                }

                return _client;
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
                var count = client.Services.Count;
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

                // 删掉旧的
                for (var i = count - 1; i >= 0; i--)
                {
                    client.Services.RemoveAt(i);
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

            return await _client.PostAsync<Int32>("Node/SendCommand", new { Code = nodeCode, command, argument, expire });
        }

        /// <summary>发送应用命令</summary>
        /// <param name="appId"></param>
        /// <param name="command"></param>
        /// <param name="argument"></param>
        /// <param name="expire"></param>
        /// <returns></returns>
        public async Task<Int32> SendAppCommand(String appId, String command, String argument = null, Int32 expire = 3600)
        {
            if (!Valid()) return -1;

            return await _client.PostAsync<Int32>("App/SendCommand", new { Code = appId, command, argument, expire });
        }
        #endregion

        #region 日志
        /// <summary>日志。默认 XTrace.Log</summary>
        public ILog Log { get; set; } = XTrace.Log;
        #endregion
    }
}