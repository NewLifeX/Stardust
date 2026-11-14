# Research — MCP HTTP 服务

**Created**: 2026-07-16
**Status**: Complete
**Spec**: [spec.md](../spec.md)

> ⚠️ **废弃说明（2026-08-03）**：本文及同目录文档中提到的 `McpController`（HTTP 端点 Controller）**已废弃并删除**，改为 `McpMiddleware`（ASP.NET 中间件）承载 `/mcp` 端点。
> **缘由**：`McpMiddleware` 在管线中注册于 `UseCube` 之前，对 `/mcp` 短路处理，可避免 Cube 的 device-id Cookie 解析（`Convert.FromBase64String`）在畸形 Cookie 时抛 `FormatException` 返回错误页，导致 MCP 客户端收到 HTML 而非 JSON-RPC。而 `McpController` 经 MVC 路由位于 `UseCube` 之后，在现有管线中始终不可达（死代码），故删除。详见 `Doc/MCP-3-客户端接入.md`。

## 概述

本文件记录 MCP HTTP 服务规划阶段的关键技术决策、研究结论与替代方案对比。所有结论均基于对 Stardust 现有代码库的实际探索，引用具体文件路径和行号。

---

## 决策 1：MCP 服务承载位置

### Decision

**McpController + McpService 放在 `Stardust.Web` 项目**，而非 spec 中原写的 `Stardust.Server`。

### Rationale

1. **依赖方向约束**：`Stardust.Web` 引用 `Stardust.Server`（单向 Web→Server），反之不成立。若 McpService 放在 Server，则无法访问 Web 层的 `DeployService.Compile/Control` 和 `PipelineService.Trigger`，而 spec 中 9 个 deploy 模块 action 全部依赖这两个服务。

2. **能力分布现状**：
   - Server 层服务：`NodeService`、`ConfigService`、`AppTokenService`、`RegistryService`、`GatewayService`、`MonitorService`（查询类为主）
   - Web 层服务：`DeployService`（编译+部署）、`PipelineService`（流水线触发+取消）
   - 27 个 action 中 9 个 deploy 类必须走 Web 层

3. **路由注册便利性**：Stardust.Web 已注册 `AddControllers` + `MapControllers`，新增 Controller 无需改动 Startup。Stardust.Server 也有相同机制，但放 Web 一次性解决所有依赖。

4. **数据库配置访问**：`StarServerSetting` 在 Server 和 Web 都能访问（通过 `services.AddSingleton(set)` 注册），无需切换。

### Alternatives Considered

- **A. 放 Server，用 HttpClient 反调 Web**：项目记忆中已有"Web 不直接引用 Server，跨项目调用用 HttpClient 反向调"的约束（针对 `[ApiFilter]` 鉴权场景）。但 MCP 服务本身不需要走鉴权链路，且引入 HttpClient 会增加网络往返开销和故障点。
- **B. 把 DeployService/PipelineService 下沉到 Server**：破坏现有分层，且这两个服务依赖 Web 层的 Controller 上下文（如 ManageProvider.UserHost），迁移成本高。
- **C. 新建独立 `Stardust.McpServer` 项目**：过度工程化，27 个 action 调用的服务分布广泛，新建项目反而要引用所有项目。

### 最终方案

- `McpController` 放 `Stardust.Web/Areas/Platform/Controllers/McpController.cs`（或 `Stardust.Web/Controllers/McpController.cs`）
- `McpService` 放 `Stardust.Web/Services/McpService.cs`
- `IMcpAction` / `McpActionBase` / `ResourceRequirement` 放 `Stardust.Web/Mcp/`
- 27 个 action 实现放 `Stardust.Web/Mcp/Actions/{Module}/`
- 3 张表（`McpToken`/`McpTokenResource`/`McpAudit`）放 `Stardust.Data/Platform/Model.xml`

---

## 决策 2：实体表定义模式

### Decision

遵循 Stardust.Data 现有的 XCode ORM 模式：在 `Stardust.Data/Platform/Model.xml` 中以 XML 声明表结构，运行 `xcode` 工具自动生成 `{中文名}.cs`（partial，含字段/索引/字段常量），手写 `{中文名}.Biz.cs`（partial，含基类 `Entity<T>`、拦截器、扩展查询）。

### Rationale

1. **项目一致性**：现有所有实体（GalaxyProject、Node、App、AppDeploy、AppPipeline 等）均采用此模式，详见 `Stardust.Data/Platform/Model.xml:44-69`（GalaxyProject 定义）和 `Stardust.Data/Platform/星系项目.Biz.cs:11`（业务类）。

2. **xcode 自动重写**：项目记忆明确"xcode 自动重写 `Stardust.Data/Deployment/*.cs` / `*.Biz.cs` 并生成对应 Web Controllers"——新增表只需更新 Model.xml 并执行 `xcode` 工具，自动生成实体类和基础 Controller 骨架。

3. **Cube 集成**：`<Option>` 节点的 `<CubeOutput>../../Stardust.Web/Areas/Platform</CubeOutput>`（`Model.xml:41`）会自动在 Web 项目生成 Cube EntityController，省去手写 CRUD。

### 实现要点

- **命名空间**：`Stardust.Data.Platform`（由 `<Namespace>` 节点决定）
- **连接名**：`Stardust`（由 `<ConnName>` 节点决定）
- **审计字段**：统一包含 `CreateUserId/CreateTime/CreateIP/UpdateUserId/UpdateTime/UpdateIP`（camelCase，与 GalaxyProject 一致），`Category="扩展"`
- **主键**：`Id`（Int32，Identity=true，PrimaryKey=true）
- **索引**：唯一索引前缀 `IU_`，普通索引前缀 `IX_`
- **Map 属性**：仅当需要导航属性时使用（如 `TokenId` 可 `Map=Stardust.Data.Platform.McpToken@Id@Name` 生成 `TokenName` 显示字段）

### Alternatives Considered

- **A. 用 EF Core Code-First**：与现有 XCode 体系冲突，引入两套 ORM。
- **B. 直接手写实体类不用 xcode**：失去自动生成字段常量、索引器、Cube Controller 的能力，且违反项目约定。

---

## 决策 3：资源授权校验的间接查找实现

### Decision

`ResourceRequirement.Indirect=true` 时，框架层通过预注册的"间接解析器"字典反查资源 ID 对应的 ProjectId/AppId，而非在每个 action 内重复实现。

### Rationale

1. **AppPipelineRun 无 ProjectId 字段**：探索发现 `AppPipelineRun` 表（`Deployment/Model.xml:305-335`）没有 `ProjectId` 字段，必须通过 `PipelineId → AppPipeline.ProjectId` 二级查找。

2. **AppDeploy 有 ProjectId**：直接查找 `AppDeploy.FindById(deploy_id).ProjectId` 即可。

3. **AppPipeline 有 ProjectId**：直接查找 `AppPipeline.FindById(pipeline_id).ProjectId` 即可。

4. **避免 action 重复实现**：27 个 action 中 9 个 deploy 类都需要间接查找，若每个 action 内自行实现会导致重复代码。

### 实现方案

在 `McpService` 中注册 `Dictionary<String, Func<Int32, Int32>>` 间接解析器：

```csharp
_indirectResolvers = new()
{
    ["AppDeploy"] = id => AppDeploy.FindById(id)?.ProjectId ?? 0,
    ["AppPipeline"] = id => AppPipeline.FindById(id)?.ProjectId ?? 0,
    ["AppPipelineRun"] = id => AppPipelineRun.FindById(id)?.PipelineId is { } pid 
                              ? AppPipeline.FindById(pid)?.ProjectId ?? 0 : 0,
    ["App"] = id => App.FindById(id)?.ProjectId ?? 0,
    ["Node"] = id => Node.FindByID(id)?.ProjectId ?? 0,
};
```

`ResourceRequirement` 声明 `IndirectEntity="AppDeploy"`，框架层自动调用解析器获取 ProjectId，再校验 Token 是否授权该 ProjectId。

### Alternatives Considered

- **A. 每个 action 内自行校验**：违反 spec FR4.9"action 内不再重复资源授权校验"的约束。
- **B. 反射动态调用**：性能差且类型不安全。

---

## 决策 4：MCP 协议端点路由

### Decision

`McpController` 使用绝对路径特性路由 `[Route("/mcp")]` + `[ApiController]` + `[ApiFilter]`，方法用 `[HttpPost]`。

### Rationale

1. **现有路由风格**：项目全部使用特性路由，无 `api/` 前缀，无 Convention。`HomeController` 用 `[Route("/")]`（`HomeController.cs:20`）、`NodeController` 用 `[HttpGet("/node/notify")]`（`NodeController.cs:257`）已证明绝对路径可行。

2. **MCP 协议要求**：客户端需固定端点 `POST /mcp`，不能用 `/mcp/index` 或 `/mcp/call` 等多段路径。绝对路径 `[Route("/mcp")]` + 方法级 `[HttpPost]` 可精确得到 `POST /mcp`。

3. **`[ApiFilter]` 适用性**：`[ApiFilter]` 统一处理异常包装和响应格式化。但 MCP 协议要求返回 JSON-RPC 2.0 格式（`{"jsonrpc":"2.0","id":...,"result":...}`），与 `[ApiFilter]` 默认的 `{code:0,data:...}` 格式冲突。

### 关键调整

**MCP 端点不使用 `[ApiFilter]`**，参考 `PipelineWebhookController`（项目记忆中提到"Web API：`/Pipeline/Webhook`，无 `[ApiFilter]`"）。MCP Controller 直接返回 JSON-RPC 格式的 `JsonResult`，异常自行包装为 JSON-RPC error。

### Controller 骨架

```csharp
[ApiController]
[Route("/mcp")]
public class McpController(McpService mcpService, StarServerSetting setting) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        if (!setting.EnableMcp) return NotFound();
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var response = await mcpService.HandleAsync(body, UserHost);
        return Json(response);
    }
}
```

### Alternatives Considered

- **A. 用 `[Route("[controller]")]`**：得到 `/mcp`，但依赖 Controller 命名为 `McpController`，且无法精确控制单段路径。
- **B. 用 `[Route("mcp")]`**：等价于 A，得到 `/mcp`，但相对路径在某些路由配置下可能被拼接前缀。

---

## 决策 5：IMcpAction 接口与反射注册机制

### Decision

`IMcpAction` 接口定义 5 个只读属性 + 1 个 `InvokeAsync` 方法。`McpService` 在构造函数中通过 `Assembly.GetTypes()` 反射扫描所有实现类，自动实例化并注册到 `Dictionary<String, IMcpAction>`。

### Rationale

1. **扩展性需求**：spec FR7.6 要求"新增动作只需添加实现类，无需修改 McpService"。

2. **项目风格一致**：Stardust.Server 的 Controller 也是通过 `AddControllers()` 自动发现注册，无显式注册代码。

3. **依赖注入兼容**：实现类可通过主构造函数注入 Service（如 `StarFactory`、`DeployService`、`ConfigService`）。

### 实现方案

```csharp
public interface IMcpAction
{
    String Name { get; }              // snake_case，如 node_send_command
    String Description { get; }       // 面向 LLM 的功能说明
    String Module { get; }            // node/app/config/deploy/gateway/monitor/system
    JObject InputSchema { get; }      // JSON Schema 参数定义
    ResourceRequirement? RequiredResource { get; }  // null 表示无资源依赖
    Task<Object> InvokeAsync(JsonElement params, CancellationToken ct);
}

public sealed class ResourceRequirement
{
    public String ResourceType { get; init; }      // project / node / app
    public String IdField { get; init; }            // params 中的字段名
    public Boolean Indirect { get; init; }          // 是否需间接查找
    public String? IndirectEntity { get; init; }    // 间接查找的实体名
}
```

`McpService` 注册逻辑：

```csharp
public McpService(IServiceProvider sp)
{
    _sp = sp;
    _actions = new();
    foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
    {
        if (type.IsClass && !type.IsAbstract && typeof(IMcpAction).IsAssignableFrom(type))
        {
            var action = (IMcpAction)ActivatorUtilities.CreateInstance(sp, type);
            _actions[action.Name] = action;
        }
    }
}
```

### 注意事项

- **程序集范围**：反射需扫描 `Stardust.Web` 程序集（`Assembly.GetExecutingAssembly()`），不是 `Stardust.Server`。
- **依赖注入**：使用 `ActivatorUtilities.CreateInstance` 支持 action 构造函数注入 Service。
- **单例 vs Scoped**：action 实例在 McpService 构造时一次性创建，由于依赖的 Service 都是 Singleton，action 实质上也是 Singleton。

### Alternatives Considered

- **A. 用 `IServiceCollection.AddSingleton<IMcpAction, XxxAction>()` 显式注册**：违反 spec FR7.6"无需修改 McpService 注册代码"。
- **B. 用 Source Generator 编译时生成注册代码**：过度工程化，反射一次性成本可接受（启动时 27 个类）。

---

## 决策 6：`pipeline_cancel` 实现方案

### Decision

新增 `PipelineService.Cancel(Int64 runId, String userHost)` 方法，将 `AppPipelineRun.Status` 从 `Pending`/`Building`/`Deploying` 改为 `Cancelled`，并尝试中断正在执行的 `RunAsync` 任务。

### Rationale

1. **能力缺失确认**：探索发现 `PipelineService` 现有方法仅有 `Trigger`/`Reprocess`/`HandleWebhookAsync`，无 `Cancel`。但 `PipelineStatus` 枚举已包含 `Cancelled` 值，说明设计上预留了取消语义。

2. **取消粒度**：由于 `RunAsync` 内部调用 `DeployService.Compile` 和 `DeployService.Control`，真正的硬中断（kill 进程）难以实现。采用"标记取消 + 软中断"策略：
   - 设置 `AppPipelineRun.Status = Cancelled`
   - `RunAsync` 在每个阶段切换点检查 `Status == Cancelled`，若是则终止后续步骤
   - 已下发的编译/部署命令无法撤销（节点会继续执行），但流水线不再等待结果

### 实现方案

```csharp
public class PipelineService(...)
{
    public async Task<Boolean> Cancel(Int64 runId, String userHost)
    {
        var run = AppPipelineRun.FindById(runId);
        if (run == null) return false;
        if (run.Status is PipelineStatus.Success or PipelineStatus.Failed or PipelineStatus.Cancelled)
            return false;  // 已终态，不可取消

        run.Status = PipelineStatus.Cancelled;
        run.Update();
        // 写历史记录
        run.WriteHistory("取消流水线", true, $"由 {userHost} 取消", userHost);
        return true;
    }
}
```

`RunAsync` 内部在编译前、部署前检查：

```csharp
if (run.Status == PipelineStatus.Cancelled)
{
    _logger.LogInformation("流水线 {RunId} 已取消，跳过后续步骤", run.Id);
    return;
}
```

### Alternatives Considered

- **A. 用 CancellationTokenSource 硬中断**：需重构 `RunAsync` 全链路传递 token，改动大且无法中断已下发的节点命令。
- **B. 不实现 cancel，仅返回错误**：违反 spec FR8.4 要求。

---

## 决策 7：`app_restart/stop/start` 实现路径

### Decision

通过 `DeployService.Control(app, deployNode, action, ...)` 走部署节点路径，而非 `StarFactory.SendAppCommandAsync`。

### Rationale

1. **探索发现**：`DeployService.Control`（`Stardust.Web/Services/DeployService.cs:98`）支持 action=`start`/`stop`/`restart`，内部下发 `deploy/start`、`deploy/stop`、`deploy/restart` 命令到 StarAgent，由 StarAgent 控制应用进程。

2. **应用直接命令的限制**：`StarFactory.SendAppCommandAsync` 下发的是应用层命令（如 `app/restart`），需要应用本身实现该命令处理器。并非所有应用都支持。

3. **部署路径的优势**：`DeployService.Control` 通过 StarAgent 控制进程，与应用是否实现命令处理器无关，覆盖面更广。

### 实现方案

`app_restart` action 内部逻辑：

```csharp
public async Task<Object> InvokeAsync(JsonElement params, CancellationToken ct)
{
    var appId = params.GetProperty("app_id").GetInt32();
    var app = App.FindById(appId);
    var deploy = AppDeploy.FindByAppId(appId);  // 取该应用的部署集
    var node = AppDeployNode.FindByDeployId(deploy.Id);  // 取目标节点
    await _deployService.Control(deploy, node, "restart", UserHost, 0, 0, null, ct);
    return new { success = true, message = "重启命令已下发" };
}
```

### 资源依赖声明

`RequiredResource = new() { ResourceType = "app", IdField = "app_id" }`

### 注意事项

- 若应用有多个部署节点，需明确操作哪一个。spec 中 `app_restart` 入参仅 `app_id`，需要扩展为 `app_id` + 可选 `node_id`（不传则操作主节点）。
- 实现时优先取 `IsMain=true` 或第一个启用的 `AppDeployNode`。

---

## 决策 8：`get_audit_log` 实现

### Decision

直接查询新增的 `McpAudit` 表，按 `TokenId` 过滤返回当前 Token 的调用记录。

### Rationale

1. **不复用 AppCommand**：`AppCommand` 表记录的是 `App/SendCommand` 和 `Node/SendCommand` 的下发记录，不包含 MCP 工具调用本身（如 `list_actions`、`search_resources` 这类查询不触发命令下发）。

2. **不复用 Cube 通用日志**：`/Admin/Log` 是用户操作日志，粒度粗且不区分 Token。

3. **McpAudit 已在 spec 中定义**：新增 `McpAudit` 表（见 spec "核心设计 8"），每次 `tools.call` 都记录一条，是 `get_audit_log` 的天然数据源。

### 实现方案

```csharp
public async Task<Object> InvokeAsync(JsonElement params, CancellationToken ct)
{
    var tokenId = McpContext.Current.TokenId;
    var page = params.TryGetProperty("page", out var p) ? p.GetInt32() : 1;
    var pageSize = params.TryGetProperty("page_size", out var ps) ? ps.GetInt32() : 20;
    var success = params.TryGetProperty("success", out var s) ? (bool?)s.GetBoolean() : null;

    var pp = new PageParameter { PageIndex = page, PageSize = pageSize };
    var list = McpAudit.Search(tokenId, success, DateTime.MinValue, DateTime.Now, null, pp);
    return list.Select(a => new {
        a.Id, a.ToolName, a.ActionName, a.Success, a.ErrorMessage,
        a.Duration, a.CreateTime
    });
}
```

---

## 决策 9：Token 字符串生成与校验

### Decision

Token 字符串格式：`sdmcp_` + 32 位 Base62 随机字符（总长 38 位），使用 `NewLife.Rand.NextString(32, true, true, false)` 生成。校验时使用恒定时间比较防止时序攻击。

### Rationale

1. **前缀设计**：`sdmcp_` 前缀便于识别 MCP Token（区别于 App.Secret、AppPipeline.Token），调试日志中一眼可辨。

2. **长度选择**：32 位 Base62 = 约 190 位熵，远超安全阈值。

3. **恒定时间比较**：spec 非功能需求要求"Token 校验使用恒定时间比较防时序攻击"。用 `CryptographicOperations.FixedTimeEquals` 或逐字节 XOR 比较。

### 实现方案

```csharp
// 生成
public static String GenerateToken() => "sdmcp_" + Rand.NextString(32, true, true, false);

// 校验（恒定时间）
public static Boolean SafeEquals(String a, String b)
{
    if (a == null || b == null || a.Length != b.Length) return false;
    var result = 0;
    foreach (var (x, y) in a.Zip(b)) result |= x ^ y;
    return result == 0;
}
```

### 注意

- Token 字符串在 `McpToken.Token` 字段中存储，长度 64（spec 定义），实际存储 38 字符。
- Token 字符串不在日志中明文打印（spec 非功能需求），审计日志的 `Arguments` 字段需脱敏（移除 `token`/`secret`/`authorization` 字段）。

---

## 决策 10：MCP 协议版本与客户端兼容性

### Decision

实现 MCP 协议版本 `2024-11-05`（HTTP transport），兼容主流客户端（Claude Desktop / Trae / Cursor）。

### Rationale

1. **协议稳定性**：`2024-11-05` 是 MCP 协议首个稳定 HTTP transport 版本，主流客户端均已支持。

2. **不实现 SSE**：spec Out of Scope 明确"暂不实现 SSE 长连接"。纯 HTTP 单请求单响应模式足以满足所有 action 调用（无流式输出需求）。

3. **协议方法子集**：仅实现 `initialize` / `tools.list` / `tools.call` 三个核心方法，不实现 `resources`/`prompts`/`notifications`（spec Out of Scope）。

### `initialize` 响应格式

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2024-11-05",
    "serverInfo": {
      "name": "Stardust",
      "version": "1.0.0"
    },
    "capabilities": {
      "tools": {}
    }
  }
}
```

### `tools.list` 响应格式

返回 5 个固定工具的 JSON Schema 定义。每个工具的 `inputSchema` 遵循 JSON Schema draft-07。

---

## 决策 11：Web 端 Token 管理页面

### Decision

新增 3 个 Cube EntityController：
- `McpTokenController : EntityController<McpToken>`（`/Platform/McpToken`）
- `McpTokenResourceController : EntityController<McpTokenResource>`（`/Platform/McpTokenResource`）
- `McpAuditController : EntityController<McpAudit>`（`/Platform/McpAudit`）

Token 表单支持勾选资源（项目/节点/应用）。

### Rationale

1. **Cube 自动生成**：xcode 工具会基于 Model.xml 自动生成基础 Controller 骨架到 `Stardust.Web/Areas/Platform/Controllers/`（由 `<CubeOutput>` 配置决定）。

2. **现有模式参考**：`GalaxyProjectController`（`Areas/Platform/Controllers/GalaxyProjectController.cs:14`）是 Platform Area 的标准 EntityController，可作为模板。

3. **资源勾选实现**：参考 `AppDeployNode` 的 `_List_Toolbar_Batch.cshtml`（`Areas/Deployment/Views/AppDeployNode/_List_Toolbar_Batch.cshtml:37-40`）使用 `_Form_ListBox` 局部视图渲染多选。Token 表单需自定义 `_Form_Body.cshtml`，分三组（项目/节点/应用）渲染勾选框。

### Token 表单关键逻辑

```csharp
// McpTokenController.OnInsert
protected override Int32 OnInsert(McpToken entity)
{
    if (entity.Token.IsNullOrEmpty())
        entity.Token = McpToken.GenerateToken();
    entity.CallCount = 0;
    return base.OnInsert(entity);
}

// McpTokenController 静态构造
static McpTokenController()
{
    ListFields.RemoveField("Token", "LastIP", "LastTime", "CallCount", "TraceId");
    // Token 字段在列表页隐藏，仅在详情/编辑页可见
}
```

### 资源勾选视图

需新建 `Areas/Platform/Views/McpToken/_Form_Body.cshtml`：
- 遍历 `GalaxyProject.FindAllWithCache()` 渲染项目勾选框
- 遍历 `Node.FindAllWithCache()` 渲染节点勾选框
- 遍历 `App.FindAllWithCache()` 渲染应用勾选框
- 每组支持"全部资源"勾选
- 保存时在 `OnInsert/OnUpdate` 中同步写入 `McpTokenResource` 表

---

## 决策 12：JSON-RPC 错误码映射

### Decision

| JSON-RPC code | 含义 | 触发条件 |
|---|---|---|
| `-32700` | Parse error | JSON 解析失败 |
| `-32600` | Invalid Request | 非 JSON-RPC 2.0 格式 |
| `-32601` | Method not found | 未识别的 method 或被 `McpActionSet` 禁用的 action |
| `-32602` | Invalid params | 参数缺失或不符合 `input_schema` |
| `-32603` | Internal error | 未捕获的异常 |
| `-32001` | Unauthorized | Token 不存在/禁用/过期 |
| `-32002` | Timeout | 超时（默认 30 秒） |
| `-32003` | Forbidden | 资源未授权 |
| `-32000` | Server error | action 业务异常 |

### Rationale

- `-32700` 到 `-32603` 是 JSON-RPC 2.0 规范预定义错误码。
- `-32000` 到 `-32099` 是规范保留的"server error"范围，可自定义。
- spec FR10.1-FR10.5 已定义这些错误码，此处仅补充 `-32600`（spec 未明确但协议要求）。

---

## 决策 13：并发与性能

### Decision

- `McpService` 注册为 Singleton，内部字典 `ConcurrentDictionary<String, IMcpAction>` 保证线程安全。
- 单次工具调用 P95 < 500ms（不含下游 RPC 等待，如 `SendNodeCommandAsync` 的 timeout=5s）。
- 支持 50 并发 MCP 调用（Kestrel 默认并发上限远超此值，无需特殊配置）。
- `McpAudit` 写入异步化（`Task.Run`），不阻塞主调用返回。

### Rationale

1. **Singleton 安全性**：所有依赖的 Service 都是 Singleton，action 实例也是 Singleton，无状态，线程安全。

2. **审计异步写入**：参考 `NodeService.WriteHistory` 的异步模式，避免审计日志写入拖慢主调用。

3. **数据库连接池**：XCode 默认连接池大小 100，50 并发足够。

---

## 决策 14：配置项变更立即生效

### Decision

`EnableMcp` 和 `McpActionSet` 通过 `StarServerSetting`（DbConfigProvider）存储，配置变更立即生效（无需重启）。

### Rationale

1. **DbConfigProvider 机制**：`StarServerSetting.Provider = new DbConfigProvider { ... }`（`Setting.cs:25`），配置存储在数据库 `Config` 表中。每次访问 `StarServerSetting.Current.EnableMcp` 会从数据库读取（有短时缓存）。

2. **McpService 读取方式**：每次 `/mcp` 请求时读取 `setting.EnableMcp`，而非启动时缓存。若关闭则直接返回 404。

3. **McpActionSet 过滤**：每次 `list_actions` 和 `invoke_action` 时读取 `setting.McpActionSet`，动态过滤动作。

---

## 决策 15：缺漏识别与补充

### 探索中发现的额外问题

1. **`app_restart/stop/start` 入参需扩展**：原 spec 中入参仅 `app_id`，但 `DeployService.Control` 需要 `AppDeployNode`（部署节点）。若应用有多个节点，需明确操作哪个。
   - **方案**：入参增加可选 `node_id`，不传时取主节点（第一个启用的 `AppDeployNode`）。

2. **`gateway_list_clusters` 无独立 Controller**：探索发现 `Stardust.Web/Areas/Gateway/Controllers/` 下无 `GatewayClusterController`，但 `GatewayCluster.Search` 实体方法存在。MCP action 直接调用实体方法即可，不依赖 Controller。

3. **`AppDeploy.AppId` 字段无 Map**：`Deployment/Model.xml:48` 中 `AppDeploy.AppId` 仅描述"对应StarApp"，无 `Map` 属性。查找 AppDeploy 时需通过 `AppDeploy.FindByAppId(appId)` 或 `AppDeploy.Search(projectId, appId, ...)`。

4. **`DeployService` 两个同名类**：
   - `Stardust.Server/Services/DeployService.cs`：处理 `AppDeployVersion`、`AppDeployNode` 与节点 Ping
   - `Stardust.Web/Services/DeployService.cs`：处理编译/下发（`Compile`/`Control`）
   - MCP action 应注入 Web 层的 `DeployService`（命名空间 `Stardust.Web.Services`），注意 `using` 区分。

5. **`StarFactory.SendNodeCommand` 已废弃**：探索发现旧版返回 `Int32` 的 `SendNodeCommand` 已标注 `[Obsolete]`。MCP action 必须使用新版 `SendNodeCommandAsync`（返回 `Task<CommandReplyModel?>`）。

### 已在 spec 中覆盖但需在 plan 中明确

- 5 个 MCP 工具的 `inputSchema` JSON Schema 完整定义（见 `contracts/mcp-protocol.md`）
- 3 张表的完整 Model.xml 定义（见 `data-model.md`）
- 27 个 action 的 `RequiredResource` 声明矩阵（见 `plan.md`）

---

## 总结

| # | 决策 | 关键选择 |
|---|---|---|
| 1 | MCP 服务承载位置 | `Stardust.Web` 项目（非 Server） |
| 2 | 实体表定义模式 | XCode Model.xml + xcode 自动生成 |
| 3 | 间接资源校验 | 预注册解析器字典 |
| 4 | MCP 端点路由 | `[Route("/mcp")]` + 不用 `[ApiFilter]` |
| 5 | IMcpAction 注册 | 反射扫描自动注册 |
| 6 | pipeline_cancel | 标记取消 + RunAsync 软中断 |
| 7 | app_restart/stop/start | 走 DeployService.Control 路径 |
| 8 | get_audit_log | 查新增 McpAudit 表 |
| 9 | Token 字符串 | `sdmcp_` + 32 位 Base62 |
| 10 | MCP 协议版本 | `2024-11-05` HTTP transport |
| 11 | Web Token 管理 | Cube EntityController + 自定义表单 |
| 12 | JSON-RPC 错误码 | 规范码 + 自定义 `-32001~-32003` |
| 13 | 并发性能 | Singleton + ConcurrentDictionary + 异步审计 |
| 14 | 配置即时生效 | DbConfigProvider 每次读取 |
| 15 | 缺漏补充 | app_restart 入参扩展 + DeployService 命名空间区分 |

所有技术决策已明确，无 `[NEEDS CLARIFICATION]` 残留。可进入 Phase 1 设计阶段。
