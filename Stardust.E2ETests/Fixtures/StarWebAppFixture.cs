using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Stardust.E2ETests.Fixtures;

/// <summary>StarWeb（魔方 MVC 管理后台）端到端测试夹具</summary>
/// <remarks>
/// 默认行为：把 <c>Bin/Web</c> 构建产物复制到临时目录，改写连接串指向独立 SQLite 库后启动真实 Kestrel 站点，
/// 全程不接触开发库 <c>Bin/Data</c>。设置环境变量 <c>STARWEB_BASE_URL</c> 时改为直连已运行的站点。
/// </remarks>
public sealed class StarWebAppFixture : IDisposable
{
    private const String EnvBaseUrl = "STARWEB_BASE_URL";
    private const String EnvUser = "STARWEB_USER";
    private const String EnvPass = "STARWEB_PASS";

    private readonly Process? _process;
    private readonly String? _root;
    private readonly ConcurrentQueue<String> _output = new();

    /// <summary>站点根地址，例如 http://127.0.0.1:53001</summary>
    public String BaseUrl { get; }

    /// <summary>仓库根目录</summary>
    public String RepoRoot { get; } = FindRepoRoot();

    /// <summary>Membership 数据库路径。外部站点模式下为空</summary>
    public String? MembershipDb { get; }

    /// <summary>登录用户名</summary>
    public String UserName { get; } = Environment.GetEnvironmentVariable(EnvUser) is { Length: > 0 } u ? u : "admin";

    /// <summary>登录密码</summary>
    public String Password { get; } = Environment.GetEnvironmentVariable(EnvPass) is { Length: > 0 } p ? p : "admin";

    /// <summary>登录页地址</summary>
    public String LoginUrl => BaseUrl + "/Admin/User/Login";

    public StarWebAppFixture()
    {
        var external = Environment.GetEnvironmentVariable(EnvBaseUrl);
        if (!String.IsNullOrEmpty(external))
        {
            BaseUrl = external.TrimEnd('/');
            MembershipDb = Environment.GetEnvironmentVariable("STARWEB_MEMBERSHIP_DB");
            return;
        }

        var repo = FindRepoRoot();
        var source = Path.Combine(repo, "Bin", "Web");
        if (!File.Exists(Path.Combine(source, "StarWeb.dll")))
            throw new InvalidOperationException($"未找到 StarWeb 构建产物：{source}。请先执行 dotnet build 星尘.slnx -c Release");

        _root = Path.Combine(Path.GetTempPath(), "stardust_e2e_web_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        CopyDirectory(source, _root, ["Data", "Config", "Log", "LogWeb", "wwwLog", "FileCache", "Uploads"]);

        var dataDir = Path.Combine(_root, "Data");
        Directory.CreateDirectory(dataDir);
        RewriteAppSettings(Path.Combine(_root, "appsettings.json"), dataDir.Replace('\\', '/'));

        MembershipDb = Path.Combine(dataDir, "Membership.db");
        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var psi = new ProcessStartInfo("dotnet", $"StarWeb.dll --urls {BaseUrl}")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 StarWeb 进程");
        _process.OutputDataReceived += (_, e) => Keep(e.Data);
        _process.ErrorDataReceived += (_, e) => Keep(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        WaitForReady(TimeSpan.FromMinutes(5));
        WaitForMenus(TimeSpan.FromMinutes(2));
    }

    /// <summary>读取站点全部可见菜单页面。菜单组节点（仅区域命名空间）没有实际页面，予以排除</summary>
    /// <returns>菜单页面列表</returns>
    public IReadOnlyList<MenuPage> GetMenuPages()
    {
        if (MembershipDb == null || !File.Exists(MembershipDb)) return [];

        var list = new List<MenuPage>();
        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = MembershipDb,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "select Name, Url, FullName from Menu where Visible=1 and Url is not null and Url<>'' and Url<>'~' and Url not like 'http%' and Url not like 'javascript:%' order by ParentID, Sort";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var url = reader.GetString(1).Trim();
            var fullName = reader.IsDBNull(2) ? "" : reader.GetString(2);

            // 区域根节点（如 /Configs）只是菜单分组，访问没有页面
            if (fullName.EndsWith(".Controllers", StringComparison.Ordinal)) continue;

            if (url.StartsWith('~')) url = url[1..];
            if (!url.StartsWith('/')) url = "/" + url;
            if (!seen.Add(url)) continue;

            list.Add(new MenuPage(name, url));
        }

        return list;
    }

    private void WaitForReady(TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var sw = Stopwatch.StartNew();
        var last = "";
        while (sw.Elapsed < timeout)
        {
            if (_process!.HasExited) throw new InvalidOperationException($"StarWeb 启动失败（exit={_process.ExitCode}）：{Environment.NewLine}{Tail()}");

            try
            {
                using var resp = http.GetAsync(LoginUrl).GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode) return;
                last = $"HTTP {(Int32)resp.StatusCode}";
            }
            catch (Exception ex)
            {
                last = ex.Message;
            }

            Thread.Sleep(1000);
        }

        throw new TimeoutException($"StarWeb 未在 {timeout.TotalSeconds:0}s 内就绪（{last}）：{Environment.NewLine}{Tail()}");
    }

    private void WaitForMenus(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = MembershipDb,
                    Mode = SqliteOpenMode.ReadOnly,
                }.ToString());
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "select count(*) from Menu";
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 50) return;
            }
            catch
            {
                // 数据库尚未创建或正被写入，继续等待
            }

            Thread.Sleep(1000);
        }
    }

    private void Keep(String? line)
    {
        if (String.IsNullOrEmpty(line)) return;

        _output.Enqueue(line);
        while (_output.Count > 400) _output.TryDequeue(out _);
    }

    private String Tail() => String.Join(Environment.NewLine, _output.TakeLast(40));

    private static String FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "星尘.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未能从输出目录定位仓库根目录（星尘.slnx）");
    }

    private static Int32 GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void RewriteAppSettings(String file, String dataDir)
    {
        var json = File.ReadAllText(file);
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) ?? throw new InvalidOperationException($"无法解析 {file}");

        var conns = root["ConnectionStrings"] ?? throw new InvalidOperationException($"appsettings.json 缺少 ConnectionStrings：{file}");
        conns["Stardust"] = $"Data Source={dataDir}/Stardust.db;Provider=SQLite;Busy Timeout=30000;Journal Mode=WAL;Synchronous=Normal";
        conns["StardustData"] = $"Data Source={dataDir}/StardustData.db;Provider=SQLite;Busy Timeout=30000;Journal Mode=WAL;Synchronous=Normal";
        conns["Membership"] = $"Data Source={dataDir}/Membership.db;Provider=SQLite;Busy Timeout=30000;Journal Mode=WAL;Synchronous=Normal";
        root["StarServer"] = "http://127.0.0.1:1";

        File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyDirectory(String source, String target, String[] excludes)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);

        foreach (var dir in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(dir);
            if (excludes.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;

            CopyDirectory(dir, Path.Combine(target, name), excludes);
        }
    }

    public void Dispose()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited) _process.Kill(true);
                _process.WaitForExit(10_000);
            }
            catch
            {
                // 进程已退出
            }

            _process.Dispose();
        }

        if (_root != null)
        {
            // 释放测试进程内 SQLite 连接池持有的文件句柄，否则临时库无法删除
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // 日志目录由 NewLife 以“根目录 + Log”拼接生成，位于临时目录之外，需要一并清理
            foreach (var dir in new[] { _root, _root + "Log" })
            {
                for (var i = 0; i < 10; i++)
                {
                    try
                    {
                        if (Directory.Exists(dir)) Directory.Delete(dir, true);
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }
    }
}

/// <summary>菜单页面</summary>
/// <param name="Name">菜单名称</param>
/// <param name="Url">页面相对地址</param>
public sealed record MenuPage(String Name, String Url);
