using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using XCode;
using XCode.DataAccessLayer;
using XCode.Membership;
using Xunit;

namespace Stardust.WebApiTests.Fixtures;

/// <summary>
/// 集成测试的主机工厂：启动真实的 Stardust.WebApi 主机，并把数据库重定向到独立临时目录。
/// </summary>
/// <remarks>
/// 两个必须隔离的对象：
/// 1. SQLite 库文件——避免与正在运行的 6681 实例争抢同一个 Data/Stardust.db 导致锁冲突；
/// 2. 星尘配置中心——本机 Star.config / StarAgent 会下发 MSSQL 连接串，连不上会拖垮测试。
///
/// 连接串必须在本进程任何实体首次访问之前改写：XCode 的 DAL 从 appsettings.json 读取连接串并缓存实例，
/// 而 AddCube() 在应用 ConfigureServices 阶段就会访问 Menu/Role。若晚于这个时点改写，
/// 成员体系的菜单与角色仍落在默认 Data 目录，而用户落在临时库，表现为登录成功却处处 403。
/// </remarks>
public class WebApiTestFactory : WebApplicationFactory<Program>
{
    public const String AuthScheme = "Bearer";
    public const String DefaultUserName = "admin";
    public const String DefaultPassword = "admin";

    private readonly String _dbDir;
    private readonly Dictionary<String, String?> _savedEnv = [];
    private readonly Lazy<Task<String>> _token;

    public WebApiTestFactory()
    {
        // 关闭 XCode 的 SQL 与调试日志：测试会大量建表与查询，SQL 日志是日志体积和耗时的主要来源
        DisableDebugLog();
        XCodeSetting.Current.ShowSQL = false;
        XCodeSetting.Current.Debug = false;

        _dbDir = Path.Combine(Path.GetTempPath(), "stardust_webapi_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dbDir);

        // 先于主机启动改写连接串，保证 AddCube() 初始化成员体系时也走临时库
        OverrideConnections();

        // 星尘客户端按环境变量读取服务端地址，避免连接到本机 StarAgent 下发的外部地址
        SetEnvironment("StarServer", "http://127.0.0.1:1");

        _token = new Lazy<Task<String>>(LoginAsync);
    }

    /// <summary>把 XCode 的四个连接串改写到临时目录。DAL 以连接名为键缓存实例，改写后需重新创建</summary>
    private void OverrideConnections()
    {
        foreach (var item in BuildConnectionStrings())
            DAL.AddConnStr(item.Key, item.Value, null, "SQLite");
    }

    /// <summary>把 XCode 配置文件改成关闭调试与 SQL 日志。适用工程启动时会重新读取该文件，仅靠内存赋值不可靠</summary>
    private static void DisableDebugLog()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Config");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, "XCode.config");
            if (File.Exists(file))
            {
                var xml = File.ReadAllText(file);
                var changed = false;
                if (xml.Contains("<ShowSQL>true</ShowSQL>"))
                {
                    xml = xml.Replace("<ShowSQL>true</ShowSQL>", "<ShowSQL>false</ShowSQL>");
                    changed = true;
                }
                if (xml.Contains("<Debug>true</Debug>"))
                {
                    xml = xml.Replace("<Debug>true</Debug>", "<Debug>false</Debug>");
                    changed = true;
                }
                if (changed) File.WriteAllText(file, xml);
            }
            else
            {
                File.WriteAllText(file, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<XCode>\r\n  <Debug>false</Debug>\r\n  <ShowSQL>false</ShowSQL>\r\n</XCode>\r\n");
            }
        }
        catch
        {
            // 配置文件不可写时退回到内存赋值
        }
    }

    /// <summary>连接串。构造函数之外，主机配置阶段还要再用一次。</summary>
    private Dictionary<String, String> BuildConnectionStrings() => new()
    {
        ["Stardust"] = $"Data Source={_dbDir}/Stardust.db;provider=sqlite",
        ["StardustData"] = $"Data Source={_dbDir}/StardustData.db;provider=sqlite",
        ["Membership"] = $"Data Source={_dbDir}/Membership.db;provider=sqlite",
        ["Cube"] = $"Data Source={_dbDir}/Cube.db;provider=sqlite",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 主机配置阶段再覆盖一次：应用 ConfigureServices（AddStardust/AddCube 等）可能依据 appsettings.json
        // 重新解析连接串，这里确保 FixTable 与后续实体操作都走临时 SQLite 库。
        builder.ConfigureServices((_, _) => OverrideConnections());

        // 同步 IConfiguration 侧的连接串，供从配置读取连接串的组件使用。
        // 用 UseSetting 而非 AddInMemoryCollection，省掉对 Microsoft.Extensions.Configuration 包的依赖。
        foreach (var item in BuildConnectionStrings())
            builder.UseSetting($"ConnectionStrings:{item.Key}", item.Value);
    }

    /// <summary>带 Bearer 认证头的 HttpClient。首次调用触发一次登录并缓存 token。</summary>
    public async Task<HttpClient> CreateAuthenticatedClient()
    {
        var token = await _token.Value;
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthScheme, token);
        return client;
    }

    /// <summary>真实登录拿 token。用裸 client 调用，避免递归进 CreateAuthenticatedClient。</summary>
    private async Task<String> LoginAsync()
    {
        using var bareClient = CreateClient();

        // 菜单扫描与角色授权都是主机启动后的后台任务，必须先等它们落库，否则首个业务请求会因无权限被拒
        Assert.True(WaitForCubeBootstrap(TimeSpan.FromSeconds(30)), "Cube 成员体系未在 30 秒内完成引导（菜单或系统角色授权缺失）");

        var resp = await bareClient.PostAsJsonAsync("/Auth/Login", new
        {
            Username = DefaultUserName,
            Password = DefaultPassword,
        });

        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, $"登录失败：HTTP {(Int32)resp.StatusCode}，响应={body}");

        var token = ParseToken(body);
        Assert.False(String.IsNullOrEmpty(token), $"登录响应未含 token，响应={body}");

        return token!;
    }

    /// <summary>
    /// 等待 Cube 成员体系引导完成：目标菜单已落库，且系统角色已获得该菜单的查看权限。
    /// </summary>
    /// <remarks>
    /// 菜单由 <c>AreaBase.RegisterArea</c> 的后台任务扫描生成，角色授权随后由 <c>Role.CheckRole</c> 补齐，
    /// 两者都是异步的。这里按"菜单存在 + 系统角色拥有 Detail 权限"判定就绪，而不是固定 sleep。
    /// 查询使用不带缓存的 FindAll 重载，避免实体缓存把空结果留住。
    /// </remarks>
    /// <param name="timeout">最长等待时间</param>
    /// <returns>就绪返回 true，超时返回 false</returns>
    private static Boolean WaitForCubeBootstrap(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                var menu = Menu.FindAll("Name='AppConfig'", null, null, 0, 1).FirstOrDefault();
                if (menu != null)
                {
                    var role = Role.FindAll("IsSystem=1", null, null, 0, 1).FirstOrDefault();
                    if (role != null && role.Has(menu.ID, PermissionFlags.Detail)) return true;
                }
            }
            catch (Exception)
            {
                // 数据库尚未建表或正被写入，继续等待
            }

            Thread.Sleep(200);
        }

        return false;
    }

    /// <summary>兼容 access_token / AccessToken 两种字段名解析令牌。</summary>
    private static String? ParseToken(String json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty("access_token", out var at1) && !String.IsNullOrEmpty(at1.GetString())) return at1.GetString();
            if (data.TryGetProperty("AccessToken", out var at2) && !String.IsNullOrEmpty(at2.GetString())) return at2.GetString();
        }
        return null;
    }

    private void SetEnvironment(String name, String value)
    {
        _savedEnv[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            // 必须先停主机（base.Dispose 释放 SQLite 连接池与文件锁），再删目录；
            // 否则目录内文件仍被锁，Directory.Delete 会抛 IOException。
            base.Dispose(disposing);

            foreach (var item in _savedEnv) Environment.SetEnvironmentVariable(item.Key, item.Value);

            try
            {
                Directory.Delete(_dbDir, true);
            }
            catch (IOException)
            {
                // 极端情况下锁未即时释放，留待系统清理临时目录，不影响测试结果
            }
        }
    }
}
