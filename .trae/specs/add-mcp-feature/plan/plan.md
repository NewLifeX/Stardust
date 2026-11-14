# Implementation Plan — MCP HTTP 服务

**Created**: 2026-07-16
**Spec**: [spec.md](../spec.md)
**Branch**: `feat/add-mcp-feature`

> ⚠️ **废弃说明（2026-08-03）**：本文及同目录文档中提到的 `McpController`（HTTP 端点 Controller）**已废弃并删除**，改为 `McpMiddleware`（ASP.NET 中间件）承载 `/mcp` 端点。
> **缘由**：`McpMiddleware` 在管线中注册于 `UseCube` 之前，对 `/mcp` 短路处理，可避免 Cube 的 device-id Cookie 解析（`Convert.FromBase64String`）在畸形 Cookie 时抛 `FormatException` 返回错误页，导致 MCP 客户端收到 HTML 而非 JSON-RPC。而 `McpController` 经 MVC 路由位于 `UseCube` 之后，在现有管线中始终不可达（死代码），故删除。详见 `Doc/MCP-3-客户端接入.md`。

## Technical Context

| 项目 | 详情 |
|---|---|
| 语言 | C# (.NET 10) |
| 框架 | ASP.NET Core + NewLife.Cube + NewLife.Remoting + XCode ORM |
| 主要项目 | `Stardust.Web`（承载 MCP 服务）、`Stardust.Data`（实体定义）、`Stardust.Server`（基础服务） |
| 数据库 | SQL Server / MySQL / SQLite（通过 XCode 多数据库支持） |
| 协议 | JSON-RPC 2.0 over HTTP（MCP `2024-11-05`） |
| 关键依赖 | NewLife.Remoting.Extensions（`[ApiFilter]`/`BaseController`）、NewLife.Cube（EntityController）、Stardust（StarFactory） |

## Constitution Check

本项目无 `.specify/memory/constitution.md`，遵循项目记忆和 AGENTS.md 中的约定：

| 约束 | 状态 | 说明 |
|---|---|---|
| 最小设计、最小改动 | ✓ | 新增功能独立于现有代码，不改动已有 Controller/Service 行为 |
| xcode 自动生成实体 | ✓ | 3 张新表通过 Model.xml + xcode 自动生成 |
| Web→Server 单向引用 | ✓ | MCP 服务放 Web 层，避免反向引用 |
| `[ApiFilter]` 来自 NewLife.Remoting.Extensions | ✓ | MCP 端点不使用 `[ApiFilter]`（参考 PipelineWebhookController） |
| `Entity._.FieldName` 完整限定 | ✓ | 业务方法中使用 `_.Token`/`_.TokenId` 等 |
| `DeployService` 两处定义需区分命名空间 | ✓ | 注入 `Stardust.Web.Services.DeployService` |
| `StarFactory.SendNodeCommand` 已废弃 | ✓ | 使用 `SendNodeCommandAsync` 新版 |
| GitNexus impact 分析 | 待实现时执行 | 编辑现有符号前需运行 `impact` 分析 |

无 Gate 阻塞。

---

## Phase 0: Outline & Research

已完成，详见 [research.md](./research.md)。

关键决策摘要：

1. **MCP 服务位置**：`Stardust.Web` 项目（访问 Web 层的 DeployService/PipelineService）
2. **实体表**：XCode Model.xml + xcode 自动生成 3 张表
3. **间接资源校验**：预注册解析器字典（`AppDeploy`/`AppPipeline`/`AppPipelineRun` → ProjectId）
4. **MCP 端点路由**：`[Route("/mcp")]` + 不使用 `[ApiFilter]`
5. **IMcpAction 注册**：反射扫描 `Assembly.GetExecutingAssembly()` 自动注册
6. **pipeline_cancel**：新增 `PipelineService.Cancel` 方法（标记取消 + 软中断）
7. **app_restart/stop/start**：通过 `DeployService.Control(action)` 走部署节点路径
8. **Token 格式**：`sdmcp_` + 32 位 Base62，恒定时间比较

---

## Phase 1: Design & Contracts

### 1.1 数据模型

已完成，详见 [data-model.md](./data-model.md)。

3 张新表：
- `McpToken`（MCP 令牌）— 16 字段，3 索引
- `McpTokenResource`（令牌资源授权）— 9 字段，3 索引
- `McpAudit`（审计日志）— 13 字段，5 索引

`StarServerSetting` 扩展 2 字段：`EnableMcp`（默认 false）、`McpActionSet`（默认 `*`）

### 1.2 接口契约

已完成，详见 [contracts/mcp-protocol.md](./contracts/mcp-protocol.md)。

- 3 个 JSON-RPC 方法：`initialize` / `tools.list` / `tools.call`
- 5 个 MCP 工具：`list_authorized_resources` / `search_resources` / `get_resource` / `list_actions` / `invoke_action`
- 9 个错误码定义
- 完整的 inputSchema JSON Schema 定义

### 1.3 Quickstart 验证

已完成，详见 [quickstart.md](./quickstart.md)。

11 个端到端验证场景覆盖：开关/鉴权/上下文/搜索/写操作/间接校验/动作过滤/审计/Web 管理/扩展/客户端集成。

---

## 实施阶段规划

### 阶段 1：基础设施（表 + 配置 + 接口定义）

**目标**：建立数据模型、配置项、接口抽象。

**任务清单**：

1. **更新 `Stardust.Data/Platform/Model.xml`**
   - 在 `<Tables>` 节点末尾追加 3 个 `<Table>` 节点（McpToken / McpTokenResource / McpAudit）
   - 字段定义见 [data-model.md](./data-model.md)

2. **运行 xcode 生成实体类**
   - 执行 `Stardust.Data/xcodetool.bat`
   - 自动生成 6 个 .cs 文件 + 3 个 Cube Controller 骨架

3. **手写业务方法到 `*.Biz.cs`**
   - `MCP令牌.Biz.cs`：`GenerateToken` / `FindByToken` / `Reset` / `IsValid` / `RecordCall` / `SafeEquals`
   - `MCP令牌资源.Biz.cs`：`FindAllByToken` / `IsAuthorized` / `GetAuthorizedProjectIds` / `SyncByToken`
   - `MCP审计日志.Biz.cs`：`Search` / `WriteAsync` / `SanitizeArguments`

4. **扩展 `Stardust.Server/Setting.cs`**
   - 在 `StarServerSetting` 类中新增 `EnableMcp`（默认 false）和 `McpActionSet`（默认 `*`）字段

5. **定义 MCP 接口与基类**
   - 新建 `Stardust.Web/Mcp/IMcpAction.cs`：接口 + `McpActionBase` 基类 + `ResourceRequirement` 类
   - 新建 `Stardust.Web/Mcp/McpContext.cs`：当前调用的上下文（TokenId/TokenName/CallerIp/UserAgent）

**验证**：
- 编译通过
- 数据库自动创建 3 张表（XCode 启动时自动建表）
- Web `/Platform/McpToken` 页面可访问（Cube 自动生成的 CRUD）

---

### 阶段 2：McpService 核心服务

**目标**：实现 Token 校验、资源授权、动作注册、审计日志的核心服务。

**任务清单**：

1. **新建 `Stardust.Web/Services/McpService.cs`**
   - 构造函数注入 `IServiceProvider`、`StarServerSetting`
   - 反射扫描注册所有 `IMcpAction` 实现到 `ConcurrentDictionary<String, IMcpAction>`
   - 注册间接资源解析器字典（`AppDeploy`/`AppPipeline`/`AppPipelineRun` → ProjectId）

2. **实现 `HandleAsync(String body, String ip, String ua)` 方法**
   - 解析 JSON-RPC 请求
   - Token 校验（`McpToken.FindByToken` + `SafeEquals` + `IsValid`）
   - 路由到 `initialize` / `tools.list` / `tools.call`
   - `tools.call` 内部分发到 5 个工具处理方法

3. **实现 5 个工具处理方法**
   - `HandleListAuthorizedResources`：查 `McpTokenResource.FindAllByToken` 返回授权资源
   - `HandleSearchResources`：跨 6 类资源搜索，按授权范围过滤
   - `HandleGetResource`：框架层资源校验 + 路由到对应资源 provider
   - `HandleListActions`：按 `McpActionSet` 过滤返回动作清单
   - `HandleInvokeAction`：action 查找 + inputSchema 校验 + **框架层资源授权校验** + 调用 `InvokeAsync`

4. **实现框架层资源授权校验**
   - `ValidateResourceAccess(Int32 tokenId, ResourceRequirement req, JsonElement params)`
   - 直接校验：从 params 提取 IdField，查 `McpTokenResource.IsAuthorized`
   - 间接校验：通过 `_indirectResolvers[req.IndirectEntity]` 反查 ProjectId/AppId

5. **实现审计日志写入**
   - `McpAudit.WriteAsync` 异步写入（不阻塞主调用）
   - 敏感字段脱敏（`SanitizeArguments`）

**验证**：
- 单元测试：Token 校验逻辑、资源授权校验逻辑、间接解析器
- `list_authorized_resources` 工具可用

---

### 阶段 3：McpController HTTP 端点

**目标**：实现 `POST /mcp` HTTP 端点。

**任务清单**：

1. **新建 `Stardust.Web/Controllers/McpController.cs`**（或 `Areas/Platform/Controllers/`）
   - `[ApiController]` + `[Route("/mcp")]`
   - 不使用 `[ApiFilter]`（返回 JSON-RPC 格式）
   - 主构造函数注入 `McpService`、`StarServerSetting`
   - `[HttpPost]` 方法：检查 `EnableMcp` → 读取 body → 调用 `mcpService.HandleAsync` → 返回 `JsonResult`

2. **注册服务到 `Startup.cs`**
   - 在 `ConfigureServices` 中添加 `services.AddSingleton<McpService>()`
   - 无需修改路由配置（`MapControllers` 自动发现）

**验证**：
- `curl -X POST http://localhost:6600/mcp` 返回 JSON-RPC 响应
- `EnableMcp=false` 时返回 404

---

### 阶段 4：27 个 Action 实现

**目标**：实现全部 27 个 `IMcpAction` 实现类。

**任务清单**（按模块组织）：

#### 4.1 节点模块（4 个）— `Stardust.Web/Mcp/Actions/Node/`

| Action 类名 | 依赖注入 |
|---|---|
| `NodeListOnlineAction` | `NodeOnline`（静态） |
| `NodeSendCommandAction` | `StarFactory` |
| `NodeUpgradeAction` | `StarFactory` |
| `NodeSearchAction` | `Node`（静态） |

#### 4.2 应用模块（7 个）— `Stardust.Web/Mcp/Actions/App/`

| Action 类名 | 依赖注入 |
|---|---|
| `AppListOnlineAction` | `App`（静态） |
| `AppSendCommandAction` | `StarFactory` |
| `AppResolveServiceAction` | `RegistryService` |
| `AppSearchServiceAction` | `AppService`（静态） |
| `AppRestartAction` | `DeployService`（Web 层） |
| `AppStopAction` | `DeployService`（Web 层） |
| `AppStartAction` | `DeployService`（Web 层） |

#### 4.3 配置模块（2 个）— `Stardust.Web/Mcp/Actions/Config/`

| Action 类名 | 依赖注入 |
|---|---|
| `ConfigGetAction` | `ConfigService` |
| `ConfigSetAction` | `ConfigService` |

#### 4.4 部署模块（9 个）— `Stardust.Web/Mcp/Actions/Deploy/`

| Action 类名 | 依赖注入 | 备注 |
|---|---|---|
| `DeployListAction` | `AppDeploy`（静态） | |
| `DeployCompileAction` | `DeployService`（Web 层） | |
| `DeployListVersionsAction` | `AppDeployVersion`（静态） | |
| `DeployListHistoryAction` | `AppDeployHistory`（静态） | |
| `DeployListNodesAction` | `AppDeployNode`（静态） | |
| `DeployInstallAction` | `DeployService`（Web 层） | |
| `PipelineTriggerAction` | `PipelineService` | |
| `PipelineGetRunAction` | `AppPipelineRun`（静态） | |
| `PipelineCancelAction` | `PipelineService` | 需新增 `PipelineService.Cancel` 方法 |

#### 4.5 网关模块（2 个）— `Stardust.Web/Mcp/Actions/Gateway/`

| Action 类名 | 依赖注入 |
|---|---|
| `GatewayListRoutesAction` | `GatewayRoute`（静态） |
| `GatewayListClustersAction` | `GatewayCluster`（静态） |

#### 4.6 监控模块（2 个）— `Stardust.Web/Mcp/Actions/Monitor/`

| Action 类名 | 依赖注入 |
|---|---|
| `MonitorTraceSearchAction` | `TraceData`（静态） |
| `MonitorAlarmListAction` | `AlarmRecord`（静态） |

#### 4.7 系统模块（1 个）— `Stardust.Web/Mcp/Actions/System/`

| Action 类名 | 依赖注入 |
|---|---|
| `GetAuditLogAction` | `McpAudit`（静态） |

#### 4.8 补充：`PipelineService.Cancel` 方法

在 `Stardust.Web/Services/PipelineService.cs` 中新增：

```csharp
public async Task<Boolean> Cancel(Int64 runId, String userHost)
{
    var run = AppPipelineRun.FindById(runId);
    if (run == null) return false;
    if (run.Status is PipelineStatus.Success or PipelineStatus.Failed or PipelineStatus.Cancelled)
        return false;
    run.Status = PipelineStatus.Cancelled;
    run.Update();
    run.WriteHistory("取消流水线", true, $"由 {userHost} 取消", userHost);
    return true;
}
```

并在 `RunAsync` 方法的编译前、部署前检查 `run.Status == PipelineStatus.Cancelled`。

**验证**：
- `list_actions` 返回 27 个 action
- 每个 action 的 `inputSchema` 正确
- 抽样测试：`node_send_command`、`deploy_list`、`pipeline_trigger`、`get_audit_log`

---

### 阶段 5：资源 Provider 实现

**目标**：为 `get_resource` 工具实现 6 类资源的详情查询。

**任务清单**：

1. **定义 `IResourceProvider` 接口**
   ```csharp
   public interface IResourceProvider
   {
       String ResourceType { get; }
       Task<Object> GetAsync(Int32 id);
   }
   ```

2. **实现 6 个 Provider**（`Stardust.Web/Mcp/Resources/`）
   - `ProjectResourceProvider`：`GalaxyProject.FindById` → 返回项目详情
   - `NodeResourceProvider`：`Node.FindByID` → 返回节点详情
   - `AppResourceProvider`：`App.FindById` → 返回应用详情
   - `DeployResourceProvider`：`AppDeploy.FindById` → 返回部署集详情
   - `PipelineResourceProvider`：`AppPipeline.FindById` → 返回流水线详情
   - `ServiceResourceProvider`：`AppService.FindById` → 返回服务详情

3. **在 `McpService` 中注册 Provider**
   - 反射扫描或手动注册到 `Dictionary<String, IResourceProvider>`

**验证**：
- `get_resource(project, 1)` 返回项目详情
- `get_resource(node, 100)` 返回节点详情
- 未授权资源返回 -32003

---

### 阶段 6：Web 管理页面

**目标**：完善 Token 管理 Web 页面，支持资源勾选。

**任务清单**：

1. **完善 `McpTokenController`**（xcode 自动生成的骨架）
   - 静态构造函数：`ListFields.RemoveField("Token", ...)` 在列表页隐藏敏感字段
   - 重写 `OnInsert`：自动生成 Token 字符串
   - 重写 `OnUpdate`：同步 `McpTokenResource` 表
   - 新增 `Reset` Action：重置 Token 字符串
   - `LogOnChange = true`

2. **新建 `McpToken/_Form_Body.cshtml`**
   - 遍历 `GalaxyProject.FindAllWithCache()` 渲染项目勾选框
   - 遍历 `Node.FindAllWithCache()` 渲染节点勾选框
   - 遍历 `App.FindAllWithCache()` 渲染应用勾选框
   - 每组支持"全部资源"勾选（`IsAll=true`）
   - 保存时同步到 `McpTokenResource` 表

3. **完善 `McpAuditController`**
   - 静态构造函数：定制列表字段（隐藏 Arguments 长文本）
   - 重写 `Search`：支持按 TokenId/ToolName/ActionName/Success 筛选

4. **完善 `McpTokenResourceController`**
   - 静态构造函数：定制列表字段
   - 通常仅查看，不允许直接 CRUD（通过 Token 表单管理）

**验证**：
- Web `/Platform/McpToken` 可创建/编辑/禁用/重置 Token
- 创建时可勾选授权资源
- `/Platform/McpAudit` 可查看审计日志

---

### 阶段 7：测试与集成验证

**目标**：端到端验证全部功能。

**任务清单**：

1. **单元测试**（`Stardust.ServerTests/Mcp/`）
   - `McpTokenTests`：Token 生成/校验/恒定时间比较
   - `McpTokenResourceTests`：授权判断/IsAll/间接校验
   - `McpServiceTests`：JSON-RPC 解析/方法路由/错误码
   - `McpActionTests`：27 个 action 的 inputSchema 校验

2. **集成测试**
   - 按 [quickstart.md](./quickstart.md) 的 11 个场景逐项验证
   - 重点验证资源授权边界（未授权 -32003、间接校验、多资源同时校验）

3. **客户端集成测试**
   - 配置 Claude Desktop / Trae / Cursor 连接
   - 验证 LLM 工作流：搜索→获取→发现→执行

**验证**：
- 所有测试通过
- 无回归（现有功能不受影响）

---

## 27 Action 的 RequiredResource 声明矩阵

| # | Action Name | Module | RequiredResource | IndirectEntity |
|---|---|---|---|---|
| 1 | node_list_online | node | null（列表过滤） | - |
| 2 | node_send_command | node | `{type:"node", field:"node_id"}` | - |
| 3 | node_upgrade | node | `{type:"node", field:"node_id"}` | - |
| 4 | node_search | node | null（列表过滤） | - |
| 5 | app_list_online | app | null（列表过滤） | - |
| 6 | app_send_command | app | `{type:"app", field:"app_id"}` | - |
| 7 | app_resolve_service | app | null（公开） | - |
| 8 | app_search_service | app | null（公开） | - |
| 9 | app_restart | app | `{type:"app", field:"app_id"}` | - |
| 10 | app_stop | app | `{type:"app", field:"app_id"}` | - |
| 11 | app_start | app | `{type:"app", field:"app_id"}` | - |
| 12 | config_get | config | `{type:"app", field:"app_id"}` | - |
| 13 | config_set | config | `{type:"app", field:"app_id"}` | - |
| 14 | deploy_list | deploy | null（列表过滤） | - |
| 15 | deploy_compile | deploy | `{type:"project", field:"deploy_id", indirect:true, entity:"AppDeploy"}` + `{type:"node", field:"node_id"}`（可选） | AppDeploy |
| 16 | deploy_list_versions | deploy | `{type:"project", field:"deploy_id", indirect:true, entity:"AppDeploy"}` | AppDeploy |
| 17 | deploy_list_history | deploy | `{type:"project", field:"deploy_id", indirect:true, entity:"AppDeploy"}` | AppDeploy |
| 18 | deploy_list_nodes | deploy | `{type:"project", field:"deploy_id", indirect:true, entity:"AppDeploy"}` | AppDeploy |
| 19 | deploy_install | deploy | `{type:"project", field:"deploy_id", indirect:true, entity:"AppDeploy"}` + `{type:"node", field:"node_id"}` | AppDeploy |
| 20 | pipeline_trigger | deploy | `{type:"project", field:"pipeline_id", indirect:true, entity:"AppPipeline"}` | AppPipeline |
| 21 | pipeline_get_run | deploy | `{type:"project", field:"run_id", indirect:true, entity:"AppPipelineRun"}` | AppPipelineRun |
| 22 | pipeline_cancel | deploy | `{type:"project", field:"run_id", indirect:true, entity:"AppPipelineRun"}` | AppPipelineRun |
| 23 | gateway_list_routes | gateway | null | - |
| 24 | gateway_list_clusters | gateway | null | - |
| 25 | monitor_trace_search | monitor | `{type:"app", field:"app_id"}`（可选，若传 app_id） | - |
| 26 | monitor_alarm_list | monitor | `{type:"app", field:"app_id"}`（可选，若传 app_id） | - |
| 27 | get_audit_log | system | null（仅查当前 Token） | - |

---

## 文件清单

### 新增文件

| 文件路径 | 说明 |
|---|---|
| `Stardust.Data/Platform/Model.xml`（修改） | 追加 3 个 Table 节点 |
| `Stardust.Data/Platform/MCP令牌.cs` | xcode 自动生成 |
| `Stardust.Data/Platform/MCP令牌.Biz.cs` | xcode 自动生成 + 手写业务方法 |
| `Stardust.Data/Platform/MCP令牌资源.cs` | xcode 自动生成 |
| `Stardust.Data/Platform/MCP令牌资源.Biz.cs` | xcode 自动生成 + 手写业务方法 |
| `Stardust.Data/Platform/MCP审计日志.cs` | xcode 自动生成 |
| `Stardust.Data/Platform/MCP审计日志.Biz.cs` | xcode 自动生成 + 手写业务方法 |
| `Stardust.Server/Setting.cs`（修改） | 新增 2 个配置字段 |
| `Stardust.Web/Mcp/IMcpAction.cs` | 接口 + 基类 + ResourceRequirement |
| `Stardust.Web/Mcp/McpContext.cs` | 调用上下文 |
| `Stardust.Web/Mcp/IResourceProvider.cs` | 资源 Provider 接口 |
| `Stardust.Web/Mcp/Resources/ProjectResourceProvider.cs` | 项目资源 Provider |
| `Stardust.Web/Mcp/Resources/NodeResourceProvider.cs` | 节点资源 Provider |
| `Stardust.Web/Mcp/Resources/AppResourceProvider.cs` | 应用资源 Provider |
| `Stardust.Web/Mcp/Resources/DeployResourceProvider.cs` | 部署集资源 Provider |
| `Stardust.Web/Mcp/Resources/PipelineResourceProvider.cs` | 流水线资源 Provider |
| `Stardust.Web/Mcp/Resources/ServiceResourceProvider.cs` | 服务资源 Provider |
| `Stardust.Web/Mcp/Actions/Node/NodeListOnlineAction.cs` | 列出在线节点 |
| `Stardust.Web/Mcp/Actions/Node/NodeSendCommandAction.cs` | 下发节点命令 |
| `Stardust.Web/Mcp/Actions/Node/NodeUpgradeAction.cs` | 触发节点升级 |
| `Stardust.Web/Mcp/Actions/Node/NodeSearchAction.cs` | 搜索节点 |
| `Stardust.Web/Mcp/Actions/App/AppListOnlineAction.cs` | 列出在线应用 |
| `Stardust.Web/Mcp/Actions/App/AppSendCommandAction.cs` | 下发应用命令 |
| `Stardust.Web/Mcp/Actions/App/AppResolveServiceAction.cs` | 解析服务地址 |
| `Stardust.Web/Mcp/Actions/App/AppSearchServiceAction.cs` | 搜索已注册服务 |
| `Stardust.Web/Mcp/Actions/App/AppRestartAction.cs` | 重启应用 |
| `Stardust.Web/Mcp/Actions/App/AppStopAction.cs` | 停止应用 |
| `Stardust.Web/Mcp/Actions/App/AppStartAction.cs` | 启动应用 |
| `Stardust.Web/Mcp/Actions/Config/ConfigGetAction.cs` | 获取应用配置 |
| `Stardust.Web/Mcp/Actions/Config/ConfigSetAction.cs` | 设置应用配置 |
| `Stardust.Web/Mcp/Actions/Deploy/DeployListAction.cs` | 列出部署集 |
| `Stardust.Web/Mcp/Actions/Deploy/DeployCompileAction.cs` | 触发编译 |
| `Stardust.Web/Mcp/Actions/Deploy/DeployListVersionsAction.cs` | 列出部署版本 |
| `Stardust.Web/Mcp/Actions/Deploy/DeployListHistoryAction.cs` | 列出部署历史 |
| `Stardust.Web/Mcp/Actions/Deploy/DeployListNodesAction.cs` | 列出部署节点 |
| `Stardust.Web/Mcp/Actions/Deploy/DeployInstallAction.cs` | 触发部署 |
| `Stardust.Web/Mcp/Actions/Deploy/PipelineTriggerAction.cs` | 触发流水线 |
| `Stardust.Web/Mcp/Actions/Deploy/PipelineGetRunAction.cs` | 查询流水线运行 |
| `Stardust.Web/Mcp/Actions/Deploy/PipelineCancelAction.cs` | 取消流水线 |
| `Stardust.Web/Mcp/Actions/Gateway/GatewayListRoutesAction.cs` | 列出网关路由 |
| `Stardust.Web/Mcp/Actions/Gateway/GatewayListClustersAction.cs` | 列出网关集群 |
| `Stardust.Web/Mcp/Actions/Monitor/MonitorTraceSearchAction.cs` | 搜索调用链 |
| `Stardust.Web/Mcp/Actions/Monitor/MonitorAlarmListAction.cs` | 列出告警记录 |
| `Stardust.Web/Mcp/Actions/System/GetAuditLogAction.cs` | 查询审计日志 |
| `Stardust.Web/Services/McpService.cs` | MCP 核心服务 |
| `Stardust.Web/Controllers/McpController.cs` | MCP HTTP 端点 |
| `Stardust.Web/Areas/Platform/Controllers/McpTokenController.cs` | xcode 自动生成 + 手写完善 |
| `Stardust.Web/Areas/Platform/Controllers/McpTokenResourceController.cs` | xcode 自动生成 |
| `Stardust.Web/Areas/Platform/Controllers/McpAuditController.cs` | xcode 自动生成 + 手写完善 |
| `Stardust.Web/Areas/Platform/Views/McpToken/_Form_Body.cshtml` | Token 表单（含资源勾选） |
| `Stardust.Web/Startup.cs`（修改） | 注册 `McpService` |
| `Stardust.Web/Services/PipelineService.cs`（修改） | 新增 `Cancel` 方法 + `RunAsync` 取消检查 |

### 修改的现有文件

| 文件路径 | 修改内容 |
|---|---|
| `Stardust.Data/Platform/Model.xml` | 追加 3 个 Table 节点 |
| `Stardust.Server/Setting.cs` | 新增 `EnableMcp` + `McpActionSet` 字段 |
| `Stardust.Web/Startup.cs` | 注册 `McpService` 为 Singleton |
| `Stardust.Web/Services/PipelineService.cs` | 新增 `Cancel` 方法，`RunAsync` 增加取消检查 |

**不改动**：任何现有 Controller、Service、实体表的对外行为。

---

## 风险与缓解

| 风险 | 影响 | 缓解措施 |
|---|---|---|
| xcode 生成覆盖手写 Biz 代码 | 高 | xcode 仅覆盖 `{中文名}.cs`，不覆盖 `{中文名}.Biz.cs`（partial 类分离） |
| 反射扫描性能 | 低 | 启动时一次性扫描 27 个类，耗时 < 100ms |
| DeployService 命名空间混淆 | 中 | `using WebDeployService = Stardust.Web.Services.DeployService;` 别名区分 |
| Token 恒定时间比较 | 中 | 使用 `SafeEquals` 方法逐字节 XOR |
| 数据库表自动创建 | 低 | XCode 启动时自动建表（`Meta.Factory.Table`） |
| 并发审计写入 | 低 | `Task.Run` 异步写入，不阻塞主调用 |
| `pipeline_cancel` 软中断限制 | 中 | 已下发的节点命令无法撤销，仅阻止后续步骤；文档说明 |

---

## 完成标准

- [ ] 3 张表创建成功，xcode 生成实体类
- [ ] `StarServerSetting.EnableMcp` + `McpActionSet` 配置生效
- [ ] `POST /mcp` 端点响应 JSON-RPC 2.0
- [ ] 5 个 MCP 工具全部可用
- [ ] 27 个 action 全部注册并可调用
- [ ] 框架层资源授权校验正确（直接/间接/多资源）
- [ ] Token 鉴权正确（无/错误/禁用/过期）
- [ ] 审计日志记录完整
- [ ] Web Token 管理页面可用（创建/编辑/禁用/重置/资源勾选）
- [ ] [quickstart.md](./quickstart.md) 11 个场景全部通过
- [ ] 无回归（现有功能不受影响）
