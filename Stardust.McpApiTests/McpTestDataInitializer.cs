using System.Net.Http.Headers;
using System.Text.Json;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using XCode;
using XCode.DataAccessLayer;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>面向真实 WebApi(6681) 的 MCP 测试数据初始化器（幂等）。
/// 用户要求：登录、初始化测试所需数据、开启 MCP、新增 MCP 项目以及令牌，全做成测试项目初始化数据的代码逻辑，已存在则跳过。
/// 执行顺序：登录(HTTP) → EnsureMcpEnabled(读配置) → EnsureProject → EnsureTestData → EnsureTokens。
/// 所有写入均按业务唯一键查重，已存在直接跳过并复用，可重复运行。</summary>
public static class McpTestDataInitializer
{
    /// <summary>WebApi 宿主地址（仅 API，Web 不同步）</summary>
    public const String BaseUrl = "http://127.0.0.1:6681";

    /// <summary>MCP 端点</summary>
    public const String McpEndpoint = BaseUrl + "/mcp";

    /// <summary>测试项目名</summary>
    public const String ProjectName = "MCP 测试";

    /// <summary>管理员登录（Cube /Auth/Login 明文 JSON）</summary>
    public const String AdminUser = "admin";
    public const String AdminPassword = "Admin@1234";

    /// <summary>测试数据前缀。所有在测试项目下创建的业务数据使用此前缀，避免与生产数据混淆</summary>
    public const String Prefix = "mcpapi-";

    /// <summary>初始化后的令牌字典：名称 → Token 明文</summary>
    public static readonly Dictionary<String, String> Tokens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>初始化后的测试项目</summary>
    public static GalaxyProject Project { get; private set; } = null!;

    /// <summary>初始化后的测试数据快照（供测试用例取 ID）</summary>
    public static Int32 AppId { get; private set; }
    public static Int32 NodeId { get; private set; }
    /// <summary>他人项目（项目1）的节点 Id，越权-7 跨项目节点访问靶</summary>
    public static Int32 CrossNodeId { get; private set; }
    public static Int32 ConfigId { get; private set; }
    public static Int32 DeployId { get; private set; }
    public static Int32 VersionId { get; private set; }
    public static Int32 DeployNodeId { get; private set; }
    public static Int32 PipelineId { get; private set; }
    public static Int32 PipelineRunId { get; private set; }
    public static Int64 AlarmId { get; private set; }
    public static Int64 HistoryId { get; private set; }
    public static Int32 ConfigHistoryId { get; private set; }
    public static Int64 TraceDataId { get; private set; }
    public static Int64 SampleDataId { get; private set; }

    /// <summary>真实集成：真实节点 Agent 在线节点（NodeOnline 新鲜心跳，Id=3 DGCWIAX10 等）。
    /// 0 表示测试环境无真实节点 Agent（覆盖-3 的 4 个 Agent 依赖工具回退网关受理断言）。</summary>
    public static Int32 RealNodeId { get; private set; }

    /// <summary>真实在线节点 Code（Agent 登录后服务端下发的节点编码，如 F4683130）</summary>
    public static String RealNodeCode { get; private set; } = "";

    /// <summary>真实在线节点名（机器名）</summary>
    public static String RealNodeName { get; private set; } = "";

    /// <summary>真实应用实例工厂：测试进程内嵌 AppClient 以 mcpapi-app 身份登录 6600（保活防 GC）</summary>
    private static StarFactory? _realAppFactory;

    /// <summary>初始化是否已执行（进程内一次性）</summary>
    private static Int32 _initialized;

    /// <summary>XCode DAL 是否已就绪</summary>
    private static Int32 _dalReady;

    private static readonly Object _lock = new();

    /// <summary>确保 XCode DAL 初始化并强制 WAL（测试进程直连 Bin\Data\Stardust.db，与 6681 服务并发读写）</summary>
    private static void EnsureDal()
    {
        if (Interlocked.Exchange(ref _dalReady, 1) == 1) return;
        // 触发实体 Meta 初始化（读 appsettings.json ConnectionStrings:Stardust）
        _ = McpToken.Meta.Session;
        var dal = McpToken.Meta.Session.Dal;
        try
        {
            dal.Execute("PRAGMA journal_mode=WAL;");
            dal.Execute("PRAGMA busy_timeout=30000;");
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"[McpApiTests] EnsureDal PRAGMA 失败：{ex.Message}");
        }
        // 与 6681 服务启动流程（Startup.cs）对齐：反向工程建表（自动跳过 TraceData/SampleData 等分表实体）
        EntityFactory.InitConnection("Stardust");
        EntityFactory.InitConnection("StardustData");
        // TraceData/SampleData 按天分表（Trace{dd} 分库），需先初始化分表策略，否则 FindAll 查询主表失败
        StarDataHelper.SplitSqliteTables();
    }

    /// <summary>判断是否为 SQLite 数据库锁定异常（并发竞争，可重试）</summary>
    private static Boolean IsDatabaseLocked(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e.Message != null && e.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>幂等初始化全部测试数据。可被多次调用，第二次起所有步骤直接跳过。</summary>
    public static async Task EnsureAllAsync(ITestOutputHelper? output = null)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            output?.WriteLine("✅ 初始化已在本进程执行过，跳过");
            return;
        }

        lock (_lock)
        {
            EnsureDal();

            // 1. 开启 MCP（读 dbConfig_StarServer.json，幂等）
            EnsureMcpEnabled(output);

            // 2. 测试项目（已存在则跳过）
            EnsureProject(output);

            // 3. 测试数据（应用/节点/配置/部署/流水线/告警，已存在则跳过）
            EnsureTestData(output);

            // 3.5 真实集成（真实节点 Agent / 应用实例 / 命令权限放行）
            EnsureRealAgentNode(output);
            EnsureAllowControl(output);
            EnsureRealAppClient(output);
            EnsureDeployNodeTarget(output);

            // 4. 多类测试令牌（已存在则跳过）
            EnsureTokens(output);
        }

        // 5. 登录（HTTP 验证，放最后以便输出完整）
        await EnsureLoginAsync(output);
    }

    /// <summary>确保 EnableMcp 开启（读 dbConfig_StarServer.json，已 true 则跳过；未开则写入）。
    /// 注意：服务运行中改配置文件不热生效，此处仅保证文件态正确；实际生效需服务重启时加载。</summary>
    private static void EnsureMcpEnabled(ITestOutputHelper? output)
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Data"));
        var file = Path.Combine(dir, "dbConfig_StarServer.json");
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        if (!File.Exists(file))
        {
            // 回退：仓库 Bin\Data
            file = Path.Combine(root, "Bin", "Data", "dbConfig_StarServer.json");
        }
        if (!File.Exists(file))
        {
            output?.WriteLine("⚠ 未找到 dbConfig_StarServer.json，跳过 EnableMcp 检查（6681 若已启动且 /mcp 可用则不受影响）");
            return;
        }

        var text = File.ReadAllText(file);
        var doc = JsonDocument.Parse(text);
        var enable = doc.RootElement.TryGetProperty("EnableMcp", out var e) && e.ValueKind == JsonValueKind.String && e.GetString() == "true";
        if (enable)
        {
            output?.WriteLine("✅ EnableMcp 已开启，跳过写入");
        }
        else
        {
            // 写入 true（保留原格式：每个 key 一行 "Key":"Value"，用最简 JSON 合并）
            var dict = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
            dict["EnableMcp"] = "true";
            var sb = new System.Text.StringBuilder("{\r\n");
            var idx = 0;
            foreach (var kv in dict)
            {
                sb.Append($"  \"{kv.Key}\":\"{kv.Value}\"");
                if (idx++ < dict.Count - 1) sb.Append(',');
                sb.Append("\r\n");
            }
            sb.Append("}");
            File.WriteAllText(file, sb.ToString());
            output?.WriteLine("✅ 已写入 EnableMcp=true 到 dbConfig_StarServer.json（需重启 6681 生效）");
        }
    }

    /// <summary>确保「MCP 测试」项目存在（Id=4 已存在则跳过）</summary>
    private static void EnsureProject(ITestOutputHelper? output)
    {
        var project = GalaxyProject.FindAll().FirstOrDefault(e => e.Name == ProjectName);
        if (project != null)
        {
            Project = project;
            output?.WriteLine($"✅ 项目「{ProjectName}」已存在（Id={project.Id}），跳过");
            return;
        }

        var p = new GalaxyProject { Name = ProjectName, Enable = true, Remark = "MCP API 端到端测试专用项目（Stardust.McpApiTests 初始化）" };
        p.Insert();
        Project = p;
        output?.WriteLine($"✅ 新建项目「{ProjectName}」（Id={p.Id}）");
    }

    /// <summary>确保测试数据存在（按业务唯一键查重，已存在跳过）</summary>
    private static void EnsureTestData(ITestOutputHelper? output)
    {
        var pid = Project.Id;

        // --- 应用 ---
        var app = App.FindAll().FirstOrDefault(e => e.Name == Prefix + "app" && e.ProjectId == pid);
        if (app == null)
        {
            app = new App { ProjectId = pid, Name = Prefix + "app", DisplayName = "MCP API 测试应用", Category = "test", Enable = true, Version = "1.0.0", Secret = "test-secret" };
            app.Insert();
            output?.WriteLine($"✅ 新建应用 {app.Name}（Id={app.Id}）");
        }
        AppId = app.Id;

        // --- 节点 ---
        var node = Node.FindAll().FirstOrDefault(e => e.Code == Prefix + "node" && e.ProjectId == pid);
        if (node == null)
        {
            node = new Node { ProjectId = pid, Name = Prefix + "node", Code = Prefix + "node", Category = "test", Enable = true, Version = "1.0.0", IP = "127.0.0.1", Secret = "test-node-secret" };
            node.Insert();
            output?.WriteLine($"✅ 新建节点 {node.Name}（Id={node.ID}）");
        }
        NodeId = node.ID;

        // --- 他人项目节点（越权-7 靶）---
        // 属于项目1（默认项目）的节点，专供跨项目越权测试：全权限令牌（绑项目4）访问它必须被 McpGuard.Node 拒绝。
        // 不能复用测试项目自己的节点（NodeId 已被本初始化器占用最小 Id），否则"跨项目"前提失效。
        var otherNode = Node.FindAll().FirstOrDefault(e => e.Code == Prefix + "crossnode");
        if (otherNode == null)
        {
            otherNode = new Node { ProjectId = 1, Name = Prefix + "crossnode", Code = Prefix + "crossnode", Category = "test", Enable = true, Version = "1.0.0", IP = "127.0.0.1", Secret = "test-node-secret" };
            otherNode.Insert();
            output?.WriteLine($"✅ 新建他人项目节点 {otherNode.Name}（Id={otherNode.ID}，ProjectId=1）");
        }
        CrossNodeId = otherNode.ID;

        // --- 配置集 + 配置项（含敏感 key，供脱敏测试） ---
        var cfg = AppConfig.FindAll().FirstOrDefault(e => e.Name == Prefix + "config" && e.ProjectId == pid);
        if (cfg == null)
        {
            cfg = new AppConfig { ProjectId = pid, AppId = AppId, Name = Prefix + "config", DisplayName = "MCP API 测试配置", Enable = true, Version = 1 };
            cfg.Insert();
            output?.WriteLine($"✅ 新建配置集 {cfg.Name}（Id={cfg.Id}）");
        }
        ConfigId = cfg.Id;

        // 普通键 + 敏感键（密码/密钥类，验证 config_get 脱敏）
        var ck1 = ConfigData.FindAllByApp(cfg.Id).FirstOrDefault(e => e.Key == "ServerName");
        if (ck1 == null)
        {
            var cd = new ConfigData { ConfigId = cfg.Id, Key = "ServerName", Value = "mcp-api-value", Enable = true, Version = 1 };
            cd.Insert();
            output?.WriteLine($"✅ 新建配置项 {cd.Key}");
        }
        var ck2 = ConfigData.FindAllByApp(cfg.Id).FirstOrDefault(e => e.Key == "Password");
        if (ck2 == null)
        {
            var cd = new ConfigData { ConfigId = cfg.Id, Key = "Password", Value = "top-secret-password", Enable = true, Version = 1 };
            cd.Insert();
            output?.WriteLine($"✅ 新建敏感配置项 {cd.Key}");
        }

        // --- 部署集 + 版本 + 部署节点 ---
        var deploy = AppDeploy.FindAll().FirstOrDefault(e => e.Name == Prefix + "deploy" && e.ProjectId == pid);
        if (deploy == null)
        {
            deploy = new AppDeploy { ProjectId = pid, AppId = AppId, Name = Prefix + "deploy", Category = "test", Enable = true, Version = "1.0.0" };
            deploy.Insert();
            output?.WriteLine($"✅ 新建部署集 {deploy.Name}（Id={deploy.Id}）");
        }
        DeployId = deploy.Id;

        var ver = AppDeployVersion.FindAll().FirstOrDefault(e => e.DeployId == DeployId && e.Version == "1.0.0");
        if (ver == null)
        {
            ver = new AppDeployVersion { DeployId = DeployId, Version = "1.0.0", Enable = true, Url = "http://127.0.0.1/pkg/1.0.0.zip", Size = 1024, CommitId = "abc123" };
            ver.Insert();
            output?.WriteLine($"✅ 新建部署版本 {ver.Version}（Id={ver.Id}）");
        }
        VersionId = ver.Id;

        var dn = AppDeployNode.FindAll().FirstOrDefault(e => e.DeployId == DeployId && e.NodeId == NodeId);
        if (dn == null)
        {
            dn = new AppDeployNode { DeployId = DeployId, NodeId = NodeId, IP = "127.0.0.1", Enable = true, Version = "1.0.0" };
            dn.Insert();
            output?.WriteLine($"✅ 新建部署节点（Id={dn.Id}）");
        }
        DeployNodeId = dn.Id;

        // --- 流水线 + 运行 ---
        var pl = AppPipeline.FindAll().FirstOrDefault(e => e.DeployId == DeployId && e.Name == Prefix + "pipeline");
        if (pl == null)
        {
            pl = new AppPipeline { DeployId = DeployId, Name = Prefix + "pipeline", Enable = true };
            pl.Insert();
            output?.WriteLine($"✅ 新建流水线 {pl.Name}（Id={pl.Id}）");
        }
        PipelineId = pl.Id;

        var run = AppPipelineRun.FindAll().FirstOrDefault(e => e.PipelineId == PipelineId);
        if (run == null)
        {
            run = new AppPipelineRun
            {
                PipelineId = PipelineId,
                Status = Stardust.Models.PipelineStatus.Success,
                TriggerSource = "manual",
                CommitId = "abc123",
                CommitMessage = "mcp api test run",
                CommitAuthor = "mcp-test",
                Branch = "main",
            };
            run.Insert();
            output?.WriteLine($"✅ 新建流水线运行（Id={run.Id}）");
        }
        PipelineRunId = run.Id;

        // --- 告警记录（Name 必须与应用名一致，alarm_list 按项目应用名过滤） ---
        var alarm = AlarmRecord.FindAll().FirstOrDefault(e => e.Name == app.Name);
        if (alarm == null)
        {
            alarm = new AlarmRecord
            {
                Name = app.Name,
                Category = app.Category,
                Action = "测试告警",
                Status = AlarmStatuses.Alarming,
                StartTime = DateTime.Now.AddMinutes(-10),
                Times = 1,
                Content = "MCP API 测试告警内容",
            };
            alarm.Insert();
            output?.WriteLine($"✅ 新建告警记录（Id={alarm.Id}）");
        }
        AlarmId = alarm.Id;

        // --- 部署历史（deploy_history / now_changes 数据源） ---
        var hist = AppDeployHistory.FindAll().FirstOrDefault(e => e.DeployId == DeployId && e.Remark == Prefix + "init");
        if (hist == null)
        {
            hist = new AppDeployHistory { DeployId = DeployId, NodeId = NodeId, Action = "install", Success = true, Remark = Prefix + "init", CreateTime = DateTime.Now.AddMinutes(-5) };
            hist.Insert();
            output?.WriteLine($"✅ 新建部署历史（Id={hist.Id}）");
        }
        HistoryId = hist.Id;

        // --- 配置历史（config_rollback 数据源。必须 Action=Publish 且 Remark 为可解析的 AppConfig JSON 快照） ---
        var ch = ConfigHistory.FindAll().FirstOrDefault(e => e.ConfigId == ConfigId && e.Action == "Publish");
        if (ch == null)
        {
            // Remark 必须能 ToJsonEntity<AppConfig> 解析（ConfigRollbackTool 依赖），此处保存 cfg 的 JSON 快照
            ch = new ConfigHistory { ConfigId = ConfigId, Action = "Publish", Success = true, Remark = cfg.ToJson(), CreateTime = DateTime.Now.AddMinutes(-5) };
            ch.Insert();
            output?.WriteLine($"✅ 新建配置历史（Id={ch.Id}，Remark=AppConfig快照）");
        }
        ConfigHistoryId = ch.Id;

        // --- 调用链采样（trace_search / trace_detail 数据源） ---
        // TraceData/SampleData 为按天分表实体（Trace{dd} 分库），查询必须包在 CreateShard 作用域内
        TraceData? td = null;
        using (var split = TraceData.Meta.CreateShard(DateTime.Today))
        {
            td = TraceData.FindAll().FirstOrDefault(e => e.AppId == AppId && e.Name == Prefix + "trace");
        }
        if (td == null)
        {
            td = new TraceData { AppId = AppId, NodeId = NodeId, ClientId = "mcp-test", Name = Prefix + "trace", Cost = 10, Errors = 0, Total = 1, StatDate = DateTime.Today, StartTime = DateTime.Now.AddMinutes(-5).ToLong(), CreateTime = DateTime.Now.AddMinutes(-5) };
            td.Insert();
            output?.WriteLine($"✅ 新建 TraceData（Id={td.Id}）");
        }
        TraceDataId = td.Id;

        SampleData? sd = null;
        using (var split = SampleData.Meta.CreateShard(DateTime.Today))
        {
            sd = SampleData.FindAll().FirstOrDefault(e => e.DataId == td.Id && e.SpanId == Prefix + "span");
        }
        if (sd == null)
        {
            sd = new SampleData { DataId = td.Id, TraceId = "mcp-api-" + td.Id, SpanId = Prefix + "span", ParentId = null, Cost = 10, Success = true, StartTime = DateTime.Now.AddMinutes(-5).ToLong(), CreateTime = DateTime.Now.AddMinutes(-5) };
            sd.Insert();
            output?.WriteLine($"✅ 新建 SampleData（Id={sd.Id}）");
        }
        SampleDataId = sd.Id;

        // --- 应用在线 / 节点在线（node_list online / app_health） ---
        var ao = AppOnline.FindAll().FirstOrDefault(e => e.AppId == AppId && e.NodeId == NodeId);
        if (ao == null)
        {
            ao = new AppOnline { AppId = AppId, NodeId = NodeId, ProjectId = pid, Name = app.Name, Version = "1.0.0", ProcessId = 1001, IP = "127.0.0.1", CreateTime = DateTime.Now.AddMinutes(-5), UpdateTime = DateTime.Now };
            ao.Insert();
            output?.WriteLine($"✅ 新建应用在线记录");
        }
        var no = NodeOnline.FindAll().FirstOrDefault(e => e.NodeID == NodeId);
        if (no == null)
        {
            no = new NodeOnline { NodeID = NodeId, ProjectId = pid, Name = node.Name, IP = "127.0.0.1", CpuRate = 0.1, MemoryUsed = 100, CreateTime = DateTime.Now.AddMinutes(-5), UpdateTime = DateTime.Now };
            no.Insert();
            output?.WriteLine($"✅ 新建节点在线记录");
        }

        // --- 应用日志（app_logs） ---
        // AppClientLog 是分表实体（TimeShardPolicy），但 StarDataHelper.SplitSqliteTables 未为它建分库，
        // 服务端 SQLite 库中也从未建过主表（app_logs 工具因此失败）。这里直接裸 SQL 幂等建主表 + 写入，
        // 与服务端共享同一 StardustData.db，服务端 app_logs 即可正常读取。
        {
            var dal4 = DAL.Create("StardustData");
            var msg = Prefix + "hello";
            try
            {
                dal4.Execute("CREATE TABLE IF NOT EXISTS AppClientLog (Id INTEGER PRIMARY KEY AUTOINCREMENT, AppId INTEGER NOT NULL, ClientId TEXT, Time TEXT, ThreadId TEXT, Kind TEXT, Name TEXT, Message TEXT, CreateTime DATETIME, CreateIP TEXT)");
                var cnt = dal4.ExecuteScalar<Int32>($"Select Count(*) From AppClientLog Where AppId={AppId} And Message='{msg}'");
                if (cnt == 0)
                {
                    var now = DateTime.Now.AddMinutes(-5).ToString("yyyy-MM-dd HH:mm:ss");
                    dal4.Execute($"Insert Into AppClientLog (AppId,ClientId,Time,Kind,Message,CreateTime) Values({AppId},'mcp-test','{now}','Info','{msg}','{now}')");
                    output?.WriteLine("✅ 新建应用日志（裸 SQL 建主表+写入）");
                }
            }
            catch (Exception ex)
            {
                output?.WriteLine($"⚠️ AppClientLog 裸 SQL 处理失败（不影响其他初始化）：{ex.Message}");
            }
        }
    }

    /// <summary>确保真实节点 Agent 在线（幂等）。轮询 NodeOnline（StardustData.db）直至出现本项目新鲜心跳
    /// （UpdateTime 距今 ≤120s，真实 Agent 每 ~10s 心跳刷新）；60s 内未出现则跳过（覆盖-3 回退网关受理）。
    /// 注意：本方法必须在本机已启动真实 StarServer(6600)+StarAgent 的前提下才能拿到真实节点。</summary>
    private static void EnsureRealAgentNode(ITestOutputHelper? output)
    {
        var pid = Project.Id;
        var deadline = DateTime.Now.AddSeconds(60);
        NodeOnline? no = null;
        while (DateTime.Now < deadline)
        {
            // 排除本初始化器为 node_list/app_health 造的假在线记录（mcpapi-* 节点，无会话），
            // 只认真实 Agent 注册的节点，否则假记录的新鲜心跳会顶替真实节点污染 RealNodeId
            no = NodeOnline.FindAll(NodeOnline._.ProjectId == pid)
                .Where(e => !(Node.FindByID(e.NodeID)?.Code ?? "").StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.UpdateTime)
                .FirstOrDefault();
            if (no != null && no.UpdateTime > DateTime.Now.AddSeconds(-120)) break;
            Thread.Sleep(3000);
        }
        if (no == null || no.UpdateTime <= DateTime.Now.AddSeconds(-120))
        {
            output?.WriteLine("⚠ 未发现真实在线节点（NodeOnline 无新鲜心跳），覆盖-3 的 4 个 Agent 依赖工具回退网关受理断言");
            return;
        }
        RealNodeId = no.NodeID;
        var node = Node.FindByID(RealNodeId);
        RealNodeCode = node?.Code ?? "";
        RealNodeName = node?.Name ?? no.Name;
        output?.WriteLine($"✅ 真实节点 Agent 在线：Id={RealNodeId} Code={RealNodeCode} Name={RealNodeName}（心跳 {no.UpdateTime:HH:mm:ss}）");
    }

    /// <summary>放行命令权限（幂等）：StarWebApi（6681 MCP 命令调用身份）与 mcpapi-app 的 AllowControlNodes 置 "*"。
    /// NodeService.SendCommand 权限闸口：AllowControlNodes 为空且 AllowControlNodesWhenEmpty=false → Unauthorized。</summary>
    private static void EnsureAllowControl(ITestOutputHelper? output)
    {
        foreach (var name in new[] { "StarWebApi", "mcpapi-app" })
        {
            var app = App.FindAll(App._.Name == name).FirstOrDefault();
            if (app == null) continue;
            if (app.AllowControlNodes != "*")
            {
                app.AllowControlNodes = "*";
                app.Update();
                output?.WriteLine($"✅ 放行 {name} 节点控制权限（AllowControlNodes=\"*\"）");
            }
        }
    }

    /// <summary>内嵌真实应用实例（幂等）：清理 mcpapi-app 的手工 AppOnline 假记录，并以 mcpapi-app 身份
    /// new StarFactory("http://127.0.0.1:6600", "mcpapi-app", "test-secret") 登录 6600，注册真实 AppOnline 实例。
    /// 此后 stardust_app_command/app_process 会真实推达本进程 AppClient 并收到回包。</summary>
    private static void EnsureRealAppClient(ITestOutputHelper? output)
    {
        // 清理手工 AppOnline 假实例（会话不存在，推送会空等 5s 超时；真实实例会自动注册）
        AppOnline.Delete(AppOnline._.AppId == AppId);
        try
        {
            _realAppFactory ??= new StarFactory("http://127.0.0.1:6600", "mcpapi-app", "test-secret");

            // 注册测试命令处理器：app_process 的 start/restart 推达本进程实例后可回真实成功包。
            // 不注册时客户端会回「命令不存在」错误包（Status=取消），断言会退化成靠错误回包假绿。
            var realApp = _realAppFactory.App;
            if (realApp != null)
            {
                realApp.RegisterCommand("app/start", a => "start-ok");
                realApp.RegisterCommand("app/restart", a => "restart-ok");
            }

            var deadline = DateTime.Now.AddSeconds(30);
            var online = false;
            while (DateTime.Now < deadline)
            {
                if (AppOnline.FindAll(AppOnline._.AppId == AppId).Any())
                {
                    online = true;
                    break;
                }
                Thread.Sleep(2000);
            }
            output?.WriteLine(online
                ? "✅ mcpapi-app 真实应用实例在线（AppClient 已登录 6600，AppCommand 将真实推达）"
                : "⚠ mcpapi-app 应用实例 30s 内未上线（app_command/app_process 回退网关受理断言）");
        }
        catch (Exception ex)
        {
            output?.WriteLine($"⚠ mcpapi-app AppClient 登录失败：{ex.Message}");
        }
    }

    /// <summary>部署节点目标改为真实节点（幂等）：AppDeployNode 的 NodeId 指向真实在线 Agent 节点，
    /// 使 stardust_deploy_node_operate 的 deploy/start 命令真实推达 Agent。</summary>
    private static void EnsureDeployNodeTarget(ITestOutputHelper? output)
    {
        if (RealNodeId == 0) return;
        var dn = AppDeployNode.FindById(DeployNodeId);
        if (dn != null && dn.NodeId != RealNodeId)
        {
            dn.NodeId = RealNodeId;
            dn.Update();
            output?.WriteLine($"✅ 部署节点 {dn.Id} 目标节点改为真实节点 Id={RealNodeId}（Code={RealNodeCode}）");
        }
    }

    /// <summary>确保多类测试令牌存在（按 Name 查重，已存在跳过）。令牌绑定测试项目/资源授权。</summary>
    private static void EnsureTokens(ITestOutputHelper? output)
    {
        var pid = Project.Id;

        // 主令牌：全 scope（全部域的 read/write）+ 项目/应用/节点全资源授权
        var main = McpToken.FindAll(McpToken._.Name == Prefix + "full").FirstOrDefault();
        if (main == null)
        {
            main = new McpToken
            {
                Name = Prefix + "full",
                Token = McpToken.GenerateToken(),
                Enable = true,
                ExpireTime = DateTime.MinValue,
                ProjectId = pid,
                UserId = 0,
                Scopes = "apps:read,apps:write,apps:admin,nodes:read,nodes:write,nodes:admin,config:read,config:write,deploy:read,deploy:write,monitor:read,monitor:write,now:read,platform:read,platform:write",
            };
            main.Insert();
            // 资源授权：项目全量 + 应用全量 + 节点全量
            foreach (var rt in new[] { McpResourceType.Project, McpResourceType.App, McpResourceType.Node })
            {
                var r = new McpTokenResource { TokenId = main.Id, ResourceType = rt.ToStorageName(), IsAll = true, Enable = true };
                r.Insert();
            }
            output?.WriteLine($"✅ 新建主令牌 {main.Name}");
        }
        // 幂等自愈：早期创建的令牌可能缺 L2 admin scope（apps:admin/nodes:admin），补全后 tools/list 才会暴露 app_command/node_command
        {
            var needAdmin = new[] { "apps:admin", "nodes:admin" };
            var missing = needAdmin.Where(s => !main.Scopes.Split(',').Select(e => e.Trim()).Contains(s)).ToArray();
            if (missing.Length > 0)
            {
                main.Scopes = String.Join(",", (main.Scopes + "," + String.Join(",", missing)).Split(',').Select(e => e.Trim()).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
                main.Update();
                output?.WriteLine($"✅ 主令牌补全 admin scope：{String.Join(",", missing)}");
            }
        }
        Tokens[main.Name] = main.Token;

        // 零 scope 令牌（仅 whoami 可用）
        var zero = McpToken.FindAll(McpToken._.Name == Prefix + "zeroscope").FirstOrDefault();
        if (zero == null)
        {
            zero = new McpToken
            {
                Name = Prefix + "zeroscope",
                Token = McpToken.GenerateToken(),
                Enable = true,
                ExpireTime = DateTime.MinValue,
                ProjectId = pid,
                UserId = 0,
                Scopes = "",
            };
            zero.Insert();
            output?.WriteLine($"✅ 新建零 scope 令牌 {zero.Name}");
        }
        Tokens[zero.Name] = zero.Token;

        // 未绑定项目令牌（ProjectId=0，业务工具应 -32003）
        var unbound = McpToken.FindAll(McpToken._.Name == Prefix + "unbound").FirstOrDefault();
        if (unbound == null)
        {
            unbound = new McpToken
            {
                Name = Prefix + "unbound",
                Token = McpToken.GenerateToken(),
                Enable = true,
                ExpireTime = DateTime.MinValue,
                ProjectId = 0,
                UserId = 0,
                Scopes = "apps:read,nodes:read,config:read,deploy:read,monitor:read,now:read,platform:read",
            };
            unbound.Insert();
            output?.WriteLine($"✅ 新建未绑定项目令牌 {unbound.Name}");
        }
        Tokens[unbound.Name] = unbound.Token;

        // 禁用令牌
        var disabled = McpToken.FindAll(McpToken._.Name == Prefix + "disabled").FirstOrDefault();
        if (disabled == null)
        {
            disabled = new McpToken
            {
                Name = Prefix + "disabled",
                Token = McpToken.GenerateToken(),
                Enable = false,
                ExpireTime = DateTime.MinValue,
                ProjectId = pid,
                UserId = 0,
                Scopes = "apps:read,nodes:read,config:read,deploy:read,monitor:read,now:read,platform:read",
            };
            disabled.Insert();
            output?.WriteLine($"✅ 新建禁用令牌 {disabled.Name}");
        }
        Tokens[disabled.Name] = disabled.Token;

        // 过期令牌
        var expired = McpToken.FindAll(McpToken._.Name == Prefix + "expired").FirstOrDefault();
        if (expired == null)
        {
            expired = new McpToken
            {
                Name = Prefix + "expired",
                Token = McpToken.GenerateToken(),
                Enable = true,
                ExpireTime = DateTime.Now.AddMinutes(-5),
                ProjectId = pid,
                UserId = 0,
                Scopes = "apps:read,nodes:read,config:read,deploy:read,monitor:read,now:read,platform:read",
            };
            expired.Insert();
            output?.WriteLine($"✅ 新建过期令牌 {expired.Name}");
        }
        Tokens[expired.Name] = expired.Token;

        // 非成员用户令牌：绑定 UserId=1（admin）但非 ProjectUser 成员 → NeedProject -32003
        var nonMember = McpToken.FindAll(McpToken._.Name == Prefix + "nonmember").FirstOrDefault();
        if (nonMember == null)
        {
            nonMember = new McpToken
            {
                Name = Prefix + "nonmember",
                Token = McpToken.GenerateToken(),
                Enable = true,
                ExpireTime = DateTime.MinValue,
                ProjectId = pid,
                UserId = 1,
                Scopes = "apps:read,nodes:read,config:read,deploy:read,monitor:read,now:read,platform:read",
            };
            nonMember.Insert();
            // 资源授权项目全量（CheckResource 通过；NeedProject 成员校验应拒绝）
            var r = new McpTokenResource { TokenId = nonMember.Id, ResourceType = McpResourceType.Project.ToStorageName(), IsAll = true, Enable = true };
            r.Insert();
            output?.WriteLine($"✅ 新建非成员用户令牌 {nonMember.Name}");
        }
        Tokens[nonMember.Name] = nonMember.Token;

        // 未授权资源令牌：只授权项目，未授权应用/节点 → CheckResource 拒绝
        var unauth = McpToken.FindAll(McpToken._.Name == Prefix + "unauth").FirstOrDefault();
        if (unauth == null)
        {
            unauth = new McpToken
            {
                Name = Prefix + "unauth",
                Token = McpToken.GenerateToken(),
                Enable = true,
                ExpireTime = DateTime.MinValue,
                ProjectId = pid,
                UserId = 0,
                Scopes = "apps:read,nodes:read,config:read,deploy:read,monitor:read,now:read,platform:read",
            };
            unauth.Insert();
            var rp = new McpTokenResource { TokenId = unauth.Id, ResourceType = McpResourceType.Project.ToStorageName(), ResourceId = pid, IsAll = false, Enable = true };
            rp.Insert();
            output?.WriteLine($"✅ 新建未授权资源令牌 {unauth.Name}");
        }
        Tokens[unauth.Name] = unauth.Token;
    }

    /// <summary>管理员登录（HTTP POST /Auth/Login），拿取 JWT。验证登录通路与管理员可用性。</summary>
    private static async Task EnsureLoginAsync(ITestOutputHelper? output)
    {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var body = new StringContent(JsonSerializer.Serialize(new { username = AdminUser, password = AdminPassword }), System.Text.Encoding.UTF8, "application/json");
        try
        {
            var resp = await client.PostAsync("/Auth/Login", body);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                output?.WriteLine($"⚠ 登录 HTTP {(Int32)resp.StatusCode}：{text.Cut(300)}");
                return;
            }
            var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("AccessToken", out var at))
            {
                output?.WriteLine("✅ 管理员登录成功（AccessToken 已获取）");
            }
            else if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 0)
            {
                output?.WriteLine($"⚠ 登录被拦截 code={code.GetInt32()}（验证码/MFA 策略），MCP 鉴权用令牌不受影响");
            }
            else
            {
                output?.WriteLine($"⚠ 登录响应未含 AccessToken：{text.Cut(300)}");
            }
        }
        catch (Exception ex)
        {
            output?.WriteLine($"⚠ 登录请求失败：{ex.Message}");
        }
    }
}
