using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using XCode.DataAccessLayer;
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
/// 隔离手段是进程级环境变量，因此由本类负责在 Dispose 中原样还原，
/// 不把污染留给同一进程内的其它测试工程。
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
        _dbDir = Path.Combine(Path.GetTempPath(), "stardust_webapi_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dbDir);

        // NewLife.XCode 的 DAL 从 NewLife.Setting（合并 IConfiguration 环境变量提供器）读取连接串，
        // 用 ConnectionStrings__X 双下划线环境变量格式最可靠（与 Cube E2E 测试一致）。
        // StarFactory.Init 读取顺序为 环境变量 > appsettings > 本地StarAgent，环境变量非空即可跳过后者。
        SetEnvironment("ConnectionStrings__Stardust", $"Data Source={_dbDir}/Stardust.db;provider=sqlite");
        SetEnvironment("ConnectionStrings__StardustData", $"Data Source={_dbDir}/StardustData.db;provider=sqlite");
        SetEnvironment("ConnectionStrings__Membership", $"Data Source={_dbDir}/Membership.db;provider=sqlite");
        SetEnvironment("ConnectionStrings__Cube", $"Data Source={_dbDir}/Cube.db;provider=sqlite");
        SetEnvironment("StarServer", "http://127.0.0.1:1");

        _token = new Lazy<Task<String>>(LoginAsync);
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
        // 关键：应用 ConfigureServices（AddStardust/AddCube 等）会依据 appsettings.json 的 MSSQL
        // 连接串提前初始化 DAL 并缓存。这里在应用 ConfigureServices 之后再次强制写入 SQLite 连接串，
        // 并用 DAL.AddConnStr 清除已缓存的 DAL 实例，确保 FixTable 与后续实体操作都走独立 SQLite 库。
        builder.ConfigureServices((_, _) =>
        {
            foreach (var item in BuildConnectionStrings())
                DAL.AddConnStr(item.Key, item.Value, null, "SQLite");
        });

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
