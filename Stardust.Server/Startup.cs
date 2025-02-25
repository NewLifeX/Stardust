using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using NewLife;
using NewLife.Caching;
using NewLife.Caching.Services;
using NewLife.IP;
using NewLife.Log;
using NewLife.Remoting.Extensions;
using NewLife.Serialization;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.Extensions.Caches;
using Stardust.Monitors;
using Stardust.Registry;
using Stardust.Server.Services;
using XCode;
using XCode.DataAccessLayer;

namespace Stardust.Server;

public class Startup
{
    public Startup(IConfiguration configuration) => Configuration = configuration;

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 初始化配置文件
        InitConfig();

        //var star = new StarFactory(null, "StarServer", null);
        var star = services.AddStardust("StarServer");
        if (star.Server.IsNullOrEmpty()) star.Server = "http://127.0.0.1:6600";

        // 埋点跟踪
        var tracer = star.Tracer;
        //services.AddSingleton(tracer);
        using var span = tracer?.NewSpan(nameof(ConfigureServices));
        if (tracer is StarTracer st) st.TrimSelf = false;

        // 分布式服务，使用配置中心RedisCache配置
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();

        var set = StarServerSetting.Current;
        services.AddSingleton(set);

        // 统计服务
        var traceService = new TraceStatService(tracer) { FlowPeriod = set.MonitorFlowPeriod, BatchPeriod = set.MonitorBatchPeriod };
        services.AddSingleton<ITraceStatService>(traceService);
        var appStatService = new AppDayStatService(tracer) { BatchPeriod = set.MonitorBatchPeriod };
        services.AddSingleton<IAppDayStatService>(appStatService);
        services.AddSingleton<ITraceItemStatService, TraceItemStatService>();
        //services.AddSingleton<IAlarmService, AlarmService>();

        IpResolver.Register();

        // 业务服务
        services.AddSingleton<NodeService>();
        services.AddSingleton<AppQueueService>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<RegistryService>();
        services.AddSingleton<AppOnlineService>();
        services.AddSingleton<UplinkService>();
        services.AddSingleton<DeployService>();
        services.AddSingleton<MonitorService>();

        services.AddSingleton<NodeSessionManager>();
        services.AddSingleton<AppSessionManager>();

        services.AddHttpClient();
        //services.AddResponseCompression();

        // 配置Json
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
#if NET7_0_OR_GREATER
            // 支持模型类中的DataMember特性
            options.JsonSerializerOptions.TypeInfoResolver = DataMemberResolver.Default;
#endif
            options.JsonSerializerOptions.Converters.Add(new TypeConverter());
            options.JsonSerializerOptions.Converters.Add(new LocalTimeConverter());
            options.JsonSerializerOptions.Converters.Add(new JsonConverter<ISpanBuilder, DefaultSpanBuilder>());
            options.JsonSerializerOptions.Converters.Add(new JsonConverter<ISpan, DefaultSpan>());
            // 支持中文编码
            options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        });

        services.AddCors(options => options.AddPolicy("star_cors", builder =>
        {
            builder.AllowAnyOrigin().AllowAnyHeader();
        }));

        // 后台服务。数据保留，定时删除过期数据
        services.AddHostedService<DataRetentionService>();
        services.AddHostedService<RedisService>();
        services.AddHostedService<OnlineService>();
        services.AddHostedService<NodeOnlineService>();
        services.AddHostedService<ApolloService>();
        services.AddHostedService<ShardTableService>();
        services.AddHostedService<AlarmService>();
        services.AddHostedService<NodeStatService>();

        // 注入Remoting服务
        services.AddRemoting(set);

        //// 注册密码提供者，用于通信过程中保护密钥，避免明文传输
        //services.TryAddSingleton<IPasswordProvider>(new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 });

        // 启用接口响应压缩
        services.AddResponseCompression();

        services.AddControllers();

        //services.Configure<KestrelServerOptions>(options =>
        //{
        //    options.AllowSynchronousIO = true;
        //});
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var tracer = app.ApplicationServices.GetRequiredService<ITracer>();
        using var span = tracer?.NewSpan(nameof(Configure));

        // 调整应用表名。兼容2021年之前的版本
        FixAppTableName();

        // 初始化数据库连接。兼容2021年之前的版本
        var conns = DAL.ConnStrs;
        if (!conns.ContainsKey("StardustData"))
        {
            var target = "";
            if (conns.ContainsKey("MonitorLog"))
                target = "MonitorLog";
            else if (conns.ContainsKey("NodeLog"))
                target = "NodeLog";

            // SQLite默认分为两个库
            if (target.IsNullOrEmpty() && conns.ContainsKey("Stardust") && DAL.Create("Stardust").DbType != DatabaseType.SQLite)
            {
                target = "Stardust";
            }

            if (!target.IsNullOrEmpty())
            {
                XTrace.WriteLine("兼容旧配置，[StardustData]使用[{0}]的连接配置，建议直接设置[StardustData]的连接字符串", target);
                var dal = DAL.Create(target);
                DAL.AddConnStr("StardustData", dal.ConnStr, null, dal.DbType + "");
            }
        }
        EntityFactory.InitConnection("Stardust");
        EntityFactory.InitConnection("StardustData");

        if (!DAL.ConnStrs.ContainsKey("Cube"))
            DAL.AddConnStr("Cube", "MapTo=Membership", null, "sqlite");

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // 调整自动分表表名。20250103起使用新的分表格式，固定31张表
        ShardTableService.FixShardTable();

        // 缓存运行时安装文件
        var set = StarServerSetting.Current;
        if (!set.FileCache.IsNullOrEmpty())
            app.UseFileCache("/files", set.FileCache, () => StarServerSetting.Current.FileCacheWhiteIP);

        app.UseCors("star_cors");

        app.UseWebSockets(new WebSocketOptions()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(60),
        });

        app.UseStardust();

        if (Environment.GetEnvironmentVariable("__ASPNETCORE_BROWSER_TOOLS") is null)
            app.UseResponseCompression();
        app.UseRouting();

        app.UseAuthorization();

        // 注册退出事件
        if (app is IHost host)
            NewLife.Model.Host.RegisterExit(() => host.StopAsync().Wait());

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        // 取得StarWeb地址
        Task.Run(() => ResolveWebUrl(app.ApplicationServices));
    }

    private static void InitConfig()
    {
        // 配置
        var set = NewLife.Setting.Current;
        if (set.IsNew)
        {
            set.LogPath = "../LogServer";
            set.DataPath = "../Data";
            set.BackupPath = "../Backup";
            set.Save();
        }
#if !DEBUG
        var set2 = XCodeSetting.Current;
        if (set2.IsNew)
        {
            set2.ShowSQL = false;
            //set2.Migration = Migration.ReadOnly;
            set2.Save();
        }
#endif
        var set3 = StarServerSetting.Current;
        if (set3.IsNew)
        {
            set3.UploadPath = "../Uploads";
            set3.Save();
        }

        // 初始化数据库
        //var n = App.Meta.Count;
        //AppStat.Meta.Session.Dal.Db.ShowSQL = false;

        //var dal = App.Meta.Session.Dal;
        //dal.CheckTables();
    }

    private static void FixAppTableName()
    {
        var dal = DAL.Create("Stardust");
        var tables = dal.Tables;
        if (tables != null && !tables.Any(e => e.TableName.EqualIgnoreCase("StarApp")))
        {
            XTrace.WriteLine("未发现Star应用新表 StarApp");

            // 验证表名和部分字段名，避免误改其它表
            var dt = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase("App"));
            if (dt != null && dt.Columns.Any(e => e.ColumnName.EqualIgnoreCase("AutoActive")))
            {
                XTrace.WriteLine("发现Star应用旧表 App ，准备重命名");

                var rs = dal.Execute($"Alter Table App Rename To StarApp");
                XTrace.WriteLine("重命名结果：{0}", rs);
            }
        }

        // 修正 Node.LastActive 数据，默认取 LastLogin
        if (Node.Meta.Count > 0)
            Node.Update("LastActive=LastLogin", "LastActive is null");
    }

    private static async Task ResolveWebUrl(IServiceProvider serviceProvider)
    {
        await Task.Delay(3_000);

        var registry = serviceProvider.GetRequiredService<IRegistry>();
        if (registry == null) return;

        var addrs = await registry.ResolveAddressAsync("StarWeb");
        var str = addrs.Join(";");
        var set = StarServerSetting.Current;
        if (set.WebUrl != str && !str.IsNullOrEmpty())
        {
            set.WebUrl = str;
            set.Save();
        }
    }
}