using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using NewLife;
using NewLife.Caching.Services;
using NewLife.Caching;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Log;
using Stardust.Data.Configs;
using Stardust.Extensions.Caches;
using Stardust.Server.Services;
using Stardust.Web.Services;
using XCode;
using XCode.DataAccessLayer;
using Stardust.Data.Deployment;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Server;
using Stardust.Data.Platform;
using Stardust.Data.Monitors;
using NewLife.Threading;
using Microsoft.AspNetCore.Http.Features;

namespace Stardust.Web;

public class Startup
{
    public Startup(IConfiguration configuration) => Configuration = configuration;

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // 初始化配置文件
        InitConfig();

        var star = services.AddStardust("StarWeb");
        using var span = star.Tracer?.NewSpan(nameof(ConfigureServices));

        // 启用配置中心，务必在数据库操作和生成雪花Id之前
        //_ = star.Config;
        var config = star.GetConfig();

        // 分布式服务，使用配置中心RedisCache配置
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();

        // 统计
        services.AddSingleton<IAppDayStatService, AppDayStatService>();
        services.AddSingleton<ITraceItemStatService, TraceItemStatService>();
        services.AddSingleton<ITraceStatService, TraceStatService>();

        services.AddSingleton<IRedisService, RedisService>();

        services.AddSingleton<TokenService>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<AppOnlineService>();
        services.AddSingleton<DeployService>();
        services.AddSingleton<NewLife.Cube.Services.TokenService>();

        //services.AddResponseCompression();

        // 后台服务。数据保留，定时删除过期数据
        services.AddHostedService<ApolloService>();
        services.AddHostedService<NodeStatService>();
        services.AddHostedService<FixDataHostedService>();

        // 启用接口响应压缩
        services.AddResponseCompression();

        // 取消上传包大小限制
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = Int32.MaxValue;
        });
        // 取消表单上传包大小限制
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = Int64.MaxValue;
        });
        services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = Int32.MaxValue;
        });

        services.AddControllersWithViews();
        services.AddCube();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var tracer = app.ApplicationServices.GetRequiredService<ITracer>();
        using var span = tracer?.NewSpan(nameof(Configure));

        // 调整应用表名
        FixTable();

        // 初始化数据库连接
        var conns = DAL.ConnStrs;
        if (!conns.ContainsKey("StardustData"))
        {
            var target = "";
            if (conns.ContainsKey("MonitorLog"))
                target = "MonitorLog";
            else if (conns.ContainsKey("NodeLog"))
                target = "NodeLog";
            //else if (conns.ContainsKey("Stardust"))
            //    target = "Stardust";

            if (!target.IsNullOrEmpty())
            {
                XTrace.WriteLine("兼容旧配置，[StardustData]使用[{0}]的连接配置，建议直接设置[StardustData]的连接字符串", target);
                var dal = DAL.Create(target);
                DAL.AddConnStr("StardustData", dal.ConnStr, null, dal.DbType + "");
            }
        }
        EntityFactory.InitConnection("Stardust");
        EntityFactory.InitConnection("StardustData");

        TrimOldAppConfig();
        //InitProject();
        ThreadPoolX.QueueUserWorkItem(InitProject);
        ThreadPoolX.QueueUserWorkItem(FixAppDeployNode);

        // 使用Cube前添加自己的管道
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/CubeHome/Error");

        Usewwwroot(app, env);

        app.UseStardust();

        // 缓存运行时安装文件
        var set = StarServerSetting.Current;
        if (!set.FileCache.IsNullOrEmpty())
            app.UseFileCache("/files", set.FileCache, () => StarServerSetting.Current.FileCacheWhiteIP);

        //app.UseStardust();
        if (Environment.GetEnvironmentVariable("__ASPNETCORE_BROWSER_TOOLS") is null)
            app.UseResponseCompression();
        app.UseCube(env);

        // 注册退出事件
        if (app is IHost host)
            NewLife.Model.Host.RegisterExit(() => host.StopAsync().Wait());

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=CubeHome}/{action=Index}/{id?}");
        });

        // 发布服务到星尘注册中心，需要指定服务名
        app.RegisterService("StarWeb", null, env.EnvironmentName, "/cube/info");

        //// 从星尘注册中心消费服务，指定需要消费的服务名
        //app.ConsumeService("StarWeb");
    }

    private static void InitConfig()
    {
        // 配置
        var set = NewLife.Setting.Current;
        if (set.IsNew)
        {
            set.LogPath = "../LogWeb";
            set.DataPath = "../Data";
            set.BackupPath = "../Backup";
            set.Save();
        }
        var set2 = CubeSetting.Current;
        if (set2.IsNew || set2.UploadPath == "Uploads")
        {
            XTrace.WriteLine("修正上传目录");
            set2.UploadPath = "../Uploads";
            set2.AvatarPath = "../Avatars";
            set2.Skin = "layui";
            set2.Save();
        }
        if (set2.StartPage.EqualIgnoreCase("/Admin/User/Info"))
        {
            set2.StartPage = "/Platform/GalaxyProject";
            set2.Save();
        }
    }

    private static void FixTable()
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

        // 强行设置反向工程，修改字段长度
        var ts = new[] {
            AppDeployNode.Meta.Table.DataTable,
            AppOnline.Meta.Table.DataTable,
            AppMeter.Meta.Table.DataTable,
            Node.Meta.Table.DataTable,
        };
        dal.Db.CreateMetaData().SetTables(Migration.Full, ts);
    }

    private static void TrimOldAppConfig() => AppConfig.TrimAll();

    private static void InitProject()
    {
        // 初始一个默认项目
        var projects = GalaxyProject.FindAll();
        var def = projects.FirstOrDefault(e => e.Name == "默认");
        if (def == null)
        {
            def = new GalaxyProject { Name = "默认", Enable = true };
            def.Insert();

            projects.Add(def);
        }

        // 根据分类新建项目
        foreach (var item in App.FindAll(null, null, null, 0, 10000))
        {
            if (item.ProjectId != 0) continue;

            FixProject(item, projects, def);
        }

        foreach (var item in AppTracer.FindAll(null, null, null, 0, 10000))
        {
            if (item.ProjectId != 0) continue;

            FixProject(item, projects, def);
        }

        foreach (var item in AppConfig.FindAll(null, null, null, 0, 10000))
        {
            if (item.ProjectId != 0) continue;

            FixProject(item, projects, def);
        }

        foreach (var item in AppDeploy.FindAll(null, null, null, 0, 10000))
        {
            if (item.ProjectId != 0) continue;

            FixProject(item, projects, def);
        }

        var deployNodes = AppDeployNode.FindAll(null, null, null, 0, 10000);
        foreach (var item in Node.FindAll(null, null, null, 0, 10000))
        {
            if (item.ProjectId != 0) continue;

            // 根据发布节点找一下应用
            var deployNode = deployNodes.FirstOrDefault(e => e.NodeId == item.ID && e.Enable);
            deployNode ??= deployNodes.FirstOrDefault(e => e.NodeId == item.ID);
            if (deployNode != null)
            {
                var app = deployNode.Deploy;
                if (app != null)
                {
                    item.ProjectId = app.ProjectId;
                    item.Update();
                }
            }

            FixProject(item, projects, def);
        }

        foreach (var item in RedisNode.FindAll(null, null, null, 0, 10000))
        {
            if (item.ProjectId != 0) continue;

            FixProject(item, projects, def);
        }
    }

    static void FixProject(IEntity entity, IList<GalaxyProject> projects, GalaxyProject def)
    {
        var category = entity["Category"] as String;
        if (category.IsNullOrEmpty())
        {
            if (entity["Name"] is String name && name.EqualIgnoreCase("StarServer", "StarWeb", "StarAgent", "AntServer", "AntWeb", "AntAgent"))
                category = "基础平台";
        }

        if (!category.IsNullOrEmpty())
        {
            var prj = projects.FirstOrDefault(e => e.Name.EqualIgnoreCase(category));
            if (prj == null)
            {
                prj = new GalaxyProject { Name = category, Enable = true };
                prj.Insert();

                projects.Add(prj);
            }

            //entity.ProjectId = prj.Id;
            entity.SetItem("ProjectId", prj.Id);
        }
        else if (def != null)
        {
            //entity.ProjectId = def.Id;
            entity.SetItem("ProjectId", def.Id);
        }

        entity.Update();
    }

    private static void FixAppDeployNode()
    {
        // 从应用发布节点表中找到删除所有无效的test/test2
        var app = AppDeploy.FindByName("Test");
        if (app != null)
        {
            var list = AppDeployNode.FindAllByAppId(app.Id);
            list.Where(e => !e.Enable).Delete();
        }

        app = AppDeploy.FindByName("Test2");
        if (app != null)
        {
            var list = AppDeployNode.FindAllByAppId(app.Id);
            list.Where(e => !e.Enable).Delete();
        }
    }

    private static IApplicationBuilder Usewwwroot(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // 独立静态文件设置，魔方自己的静态资源内嵌在程序集里面
        var options = new StaticFileOptions();
        {
            var embeddedProvider = new CubeEmbeddedFileProvider(Assembly.GetExecutingAssembly(), "Stardust.Web.wwwroot");
            if (!env.WebRootPath.IsNullOrEmpty() && Directory.Exists(env.WebRootPath))
                options.FileProvider = new CompositeFileProvider(new PhysicalFileProvider(env.WebRootPath), embeddedProvider);
            else
                options.FileProvider = embeddedProvider;
        }
        app.UseStaticFiles(options);

        return app;
    }
}