# MCP（Model Context Protocol）HTTP 服务 Spec

> ⚠️ **废弃说明（2026-08-03）**：本文及同目录文档中提到的 `McpController`（HTTP 端点 Controller）**已废弃并删除**，改为 `McpMiddleware`（ASP.NET 中间件）承载 `/mcp` 端点。
> **缘由**：`McpMiddleware` 在管线中注册于 `UseCube` 之前，对 `/mcp` 短路处理，可避免 Cube 的 device-id Cookie 解析（`Convert.FromBase64String`）在畸形 Cookie 时抛 `FormatException` 返回错误页，导致 MCP 客户端收到 HTML 而非 JSON-RPC。而 `McpController` 经 MVC 路由位于 `UseCube` 之后，在现有管线中始终不可达（死代码），故删除。详见 `Doc/MCP-3-客户端接入.md`。

## Why

Stardust 已具备完整的"节点管理 + 应用管理 + 配置中心 + 注册中心 + 监控中心 + 远程发布 + 网关管理"能力（M1–M12 共 117 个功能），但所有能力**只能通过 Web 控制台或私有 RPC 协议**调用，**无法被 LLM/智能体直接消费**：

- 用户想问"现在哪些节点离线？" 必须自己打开浏览器查 Web
- 用户想问"把 XXX 应用部署到 YYY 节点" 必须手动点击流水线
- 用户想问"AAA 服务的当前配置是什么" 必须进入配置中心页面

缺少一个**面向 LLM 的标准协议入口**，让 AI 助手能直接查询系统状态、执行运维操作。MCP（Model Context Protocol）是 Anthropic 主导的事实标准，已被主流 IDE/Agent（Trae、Claude Desktop、Cursor）原生支持。通过在 Stardust 服务端实现 MCP HTTP 端点，可让任意 MCP 客户端把 Stardust 当作一个工具服务器调用，**无需为每个 Agent 重新开发适配层**。

现有用户 token 体系走 Cube 框架 Cookie/Session（外部库，源码不在仓库），Server 进程无法直接解码；且该 token 仅校验"是否登录的用户"，未与项目/节点/应用资源做强制授权绑定。因此 MCP 需要一套**独立的资源授权 Token 体系**：每个 Token 显式绑定可访问的项目、节点、应用集合，禁用 Token 即立即失去对相应资源的访问能力。

## Clarifications

### Session 2026-07-16

- Q: 四个 MCP 工具的具体结构？ → A: 4 工具：`list_authorized_resources` + `get_resource` + `list_actions` + `invoke_action`
- Q: 资源与动作的扩展机制？ → A: 纯代码驱动（实现接口自动注册，无 DB 配置表）
- Q: `get_resource` 的资源类型范围？ → A: 6 类核心：project / node / app / deploy / pipeline / service
- Q: `invoke_action` 的授权模型？ → A: 仅框架层统一校验（action 声明资源依赖，框架提取资源 ID 校验，action 内不再校验）
- Q: 首批 Action 清单边界？ → A: 精简到 ~20 个高频 action + 补充 search_resources 和 list_deploy_nodes

### Session 2026-07-16（补充审视）

- Q: 是否有 action 适合抽成 MCP 工具？ → A: `search_resources` 提升为第 5 个 MCP 工具（与 `get_resource` 形成"搜索→获取"查询原语对）
- Q: 是否补充审计查询 action？ → A: 补充 `get_audit_log`（LLM 自省用，查当前 Token 最近的调用记录）
- Q: 是否补充运维 action？ → A: 补充 5 个：`app_restart` / `app_stop` / `app_start`（应用生命周期）+ `deploy_compile`（仅编译不部署）+ `pipeline_cancel`（取消流水线）
- 最终工具数：5 个 MCP 工具；最终 action 数：27 个（原 22 - search_resources 提升 + 6 新增）

## What Changes

在 Stardust.Server 中新增 MCP HTTP 服务端，对外暴露 **5 个 MCP 工具**（`list_authorized_resources` / `search_resources` / `get_resource` / `list_actions` / `invoke_action`），通过这 5 个工具组合访问 Stardust 全部能力；并设计独立的资源授权 Token 体系。

**核心设计理念**：避免暴露过多细粒度工具导致 LLM 选择困难和协议臃肿。MCP 工具聚焦"协议级查询原语"（授权发现/搜索/获取/动作发现/动作调用），业务操作统一走 `invoke_action`。LLM 典型工作流：`list_authorized_resources` → `search_resources(keyword)` → `get_resource(type, id)` → `list_actions` → `invoke_action(action, params)`。单个动作按 snake_case 命名（如 `node_send_command`、`app_restart`），名字+少量参数即可表意。

- 新增 `McpToken` 表：Token 凭证（名称、Token 字符串、Enable、过期时间、最后调用信息）
- 新增 `McpTokenResource` 表：Token 与资源的授权关系（TokenId + ResourceType[Project/Node/App] + ResourceId），支持"全部资源"授权标记
- 新增 `McpAction` 抽象（`IMcpAction` 接口 + `McpActionBase` 基类）：代码驱动自动注册，每个 action 自描述 name/description/inputSchema/requiredResource
- 新增 5 个 MCP 工具实现：`list_authorized_resources` / `search_resources` / `get_resource` / `list_actions` / `invoke_action`
- 新增 ~27 个首批 action 实现（按 7 模块组织）
- 新增 MCP 配置项：`StarServerSetting.EnableMcp`（默认 `false`）、`StarServerSetting.McpActionSet`（动作集开关，默认全开）
- 新增 `McpController`：实现 MCP 协议三个核心方法（`initialize` / `tools.list` / `tools.call`），基于 JSON-RPC 2.0 over HTTP
- 新增 `McpService`：Token 校验、资源授权检查（框架层统一）、动作注册与路由、审计日志
- 新增审计表 `McpAudit`：记录每次工具调用（工具名/调用方 IP/入参摘要/TokenId/返回状态/耗时）
- 复用现有能力：`AppController.SearchService`、`NodeOnlineService`、`ConfigService.GetAll`、`DeployService`、`PipelineService`、`GatewayService.GetConfig` 等
- **不改动** 任何已有 Controller、Service、实体表的对外行为
- **不破坏** 现有 `[ApiFilter]` / JWT / `BaseController` 鉴权链路
- MCP 端点独立鉴权：`Authorization: Bearer {McpToken}`，**不复用** 设备 token 体系，**不复用** Web 用户登录态

## Impact

- Affected specs：
  - 节点管理（M5，无变动，仅作为资源 + 动作数据源）
  - 应用管理（M4，无变动，仅作为资源 + 动作数据源）
  - 配置中心（M6，无变动，仅作为动作数据源）
  - 远程发布（M10，无变动，仅作为资源 + 动作数据源）
  - 网关管理（M11，无变动，仅作为动作数据源）
  - 监控中心（M8，无变动，仅作为动作数据源）
  - 平台管理（M3，无变动，仅作为资源授权对象）
  - **新增** MCP 工具服务能力 + MCP Token 资源授权体系
- Affected code：
  - `Stardust.Server/Setting.cs` — 新增 `EnableMcp` / `McpActionSet` 字段
  - `Stardust.Server/Controllers/McpController.cs` — 新增（MCP HTTP 端点）
  - `Stardust.Server/Services/McpService.cs` — 新增（Token 校验、资源授权、动作路由、审计）
  - `Stardust.Server/Mcp/IMcpAction.cs` — 新增（动作接口与基类）
  - `Stardust.Server/Mcp/IMcpTool.cs` — 新增（4 个工具接口与基类）
  - `Stardust.Server/Mcp/Actions/*.cs` — 新增（具体动作实现，按模块拆分）
  - `Stardust.Data/Platform/Model.xml` — 新增 `McpToken`、`McpTokenResource`、`McpAudit` 三张表
  - `Stardust.Server/Startup.cs` — 注册 `McpService` 与 MCP 路由
  - `Stardust.Web/Areas/Platform/Controllers/McpTokenController.cs` — 新增（Token 管理：列表/新建/编辑/重置/禁用）
  - `Stardust.Web/Areas/Platform/Controllers/McpTokenResourceController.cs` — 新增（Token 资源授权管理）
  - `Stardust.Web/Areas/Platform/Controllers/McpAuditController.cs` — 新增（审计日志查看）
  - `Stardust.Web/Areas/Platform/Views/McpToken/_Form_Body.cshtml` — 新增（Token 表单，含资源勾选）

## 核心设计

### 1. MCP 协议端点

MCP 端点路径：`POST /mcp`（单一端点，JSON-RPC 2.0）

支持的方法：

| JSON-RPC method | 说明 |
|---|---|
| `initialize` | 协议握手，返回 `serverInfo`（name=`Stardust`、version）和 `capabilities` |
| `tools.list` | 返回 4 个固定工具的清单（`list_authorized_resources` / `get_resource` / `list_actions` / `invoke_action`） |
| `tools.call` | 调用指定工具，传入 `name` 和 `arguments`，返回 `content` 数组 |

> 传输方式：纯 HTTP（单请求单响应），暂不实现 SSE 长连接。

### 2. 五个 MCP 工具

MCP 协议层面只暴露 5 个工具，避免工具列表臃肿。MCP 工具聚焦"协议级查询原语"（授权发现/搜索/获取/动作发现/动作调用），业务操作统一走 `invoke_action`。LLM 通过这 5 个工具的组合完成所有操作。

#### 2.1 `list_authorized_resources`

**描述**：查询当前 Token 授权了哪些资源（项目/节点/应用），返回资源标识符和说明。LLM 首次接入时调用此工具建立上下文。

**入参**：
- `resource_type?`（String）：可选过滤，值为 `project` / `node` / `app`；不传则返回全部三类

**返回**：按类型分组的资源列表，每项含 `id` / `name` / `type` / `description`

**资源声明**：无（查询自身授权，无需资源校验）

#### 2.2 `search_resources`

**描述**：按关键字跨类型搜索资源，返回匹配的资源标识符和说明。与 `get_resource` 形成"搜索→获取"的查询原语对——LLM 不知道资源 ID 时第一步用 `search_resources` 查找，拿到 ID 后用 `get_resource` 获取详情。

**入参**：
- `keyword`（String，必填）：搜索关键字（匹配资源名称/编码/IP 等）
- `resource_type?`（String）：可选过滤，值为 `project` / `node` / `app` / `deploy` / `pipeline` / `service`；不传则全搜

**返回**：按类型分组的匹配资源列表，每项含 `id` / `name` / `type` / `description`

**资源声明**：无（搜索结果按 Token 授权范围过滤返回，不返回未授权资源）

#### 2.3 `get_resource`

**描述**：按资源类型 + ID 获取单个资源详情。适合 LLM 拿到资源 ID 后查询完整信息（如 `get_resource(project, 100)`）。与 `search_resources` 配对使用。

**入参**：
- `resource_type`（String，必填）：`project` / `node` / `app` / `deploy` / `pipeline` / `service`
- `resource_id`（Int32，必填）：资源 ID

**返回**：资源详情 JSON（含关键字段和关联信息）

**资源声明**：根据 `resource_type` 动态决定
- `project` / `node` / `app`：校验对应类型的 Token 授权
- `deploy` / `pipeline`：按其 `ProjectId` 校验项目授权
- `service`：按其 `AppId` 校验应用授权

#### 2.4 `list_actions`

**描述**：返回当前可调用的动作清单（含 name/description/inputSchema/requiredResource）。LLM 在执行操作前调用此工具发现"能做什么、需要什么参数"。

**入参**：
- `module?`（String）：可选过滤，值为 `node` / `app` / `config` / `deploy` / `gateway` / `monitor` / `system`；不传则返回全部

**返回**：动作元信息数组，每项含：
- `name`（如 `node_send_command`）
- `description`（面向 LLM 的功能说明）
- `module`（所属模块）
- `input_schema`（JSON Schema 参数定义）
- `required_resource`（资源依赖声明，如 `{"type":"node","field":"node_id"}`）

**资源声明**：无（查询动作清单，无需资源校验）

#### 2.5 `invoke_action`

**描述**：调用指定动作。这是 LLM 执行操作的唯一入口，通过 `action_name` + `params` 路由到具体实现。

**入参**：
- `action_name`（String，必填）：动作名（snake_case，如 `node_send_command`）
- `params`（Object，必填）：动作参数，结构由对应 action 的 `input_schema` 定义

**返回**：动作执行结果（JSON 内容块）

**资源声明**：由被调 action 的 `required_resource` 声明决定，框架层统一校验

### 3. 鉴权（资源授权 Token 体系）

#### 3.1 Token 实体（`McpToken` 表）

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Int32 | 主键 |
| Name | String(50) | Token 名称（如"运维助手-小明"），便于管理员识别 |
| Token | String(64) | Token 字符串，64 位随机字符（主索引，唯一） |
| Enable | Boolean | 是否启用。禁用后该 Token 所有调用立即返回 `-32001` |
| ExpireTime | DateTime | 过期时间。null 表示永不过期 |
| LastTime | DateTime | 最后调用时间 |
| LastIP | String(50) | 最后调用方 IP |
| CallCount | Int32 | 累计调用次数 |
| Remark | String(200) | 备注 |
| CreateUserId/Time/IP、UpdateUserId/Time/IP | — | 标准审计字段 |

Token 字符串在创建时自动生成，**创建后不可修改**（如需更换请用"重置"功能，会生成新 Token 并保留旧 Token 的资源授权关系记录到审计）。

#### 3.2 资源授权（`McpTokenResource` 表）

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Int32 | 主键 |
| TokenId | Int32 | 关联 `McpToken.Id`（索引） |
| ResourceType | String(20) | 资源类型：`Project` / `Node` / `App` |
| ResourceId | Int32 | 资源 ID（对应 `GalaxyProject.Id` / `Node.ID` / `App.Id`） |
| IsAll | Boolean | 是否授权该类型的"全部资源"。true 时忽略 ResourceId |
| Enable | Boolean | 是否启用（支持临时禁用某个资源授权而不删除） |
| CreateTime | DateTime | 授权时间 |

唯一索引 `IU_McpTokenResource_TokenId_ResourceType_ResourceId`，普通索引 `IX_McpTokenResource_TokenId`。

#### 3.3 授权语义

- **项目（Project）授权**：Token 可访问该项目下的所有节点、应用、部署集、流水线、配置等（通过 `Node.ProjectId`/`App.ProjectId` 等外键关联过滤）
- **节点（Node）授权**：Token 可对该节点执行查询、下发命令、升级等操作（独立于项目授权，允许跨项目授权单个节点）
- **应用（App）授权**：Token 可对该应用执行查询、下发命令、查配置等操作（独立于项目授权，允许跨项目授权单个应用）
- **`IsAll=true` 授权**：该类型下的所有资源（含未来新增的）均允许访问，适合管理员 Token
- **三类资源授权是 OR 关系**：如 Token 授权了"项目 A"且授权了"节点 X（属于项目 B）"，则 Token 既能访问项目 A 的所有资源，也能访问节点 X
- **未授权的资源**：`invoke_action` 和 `get_resource` 入参中引用的资源 ID 必须在 Token 的授权范围内，否则返回 JSON-RPC `-32003`（无权限）

#### 3.4 调用流程

```
MCP 客户端
  │ POST /mcp  Authorization: Bearer {token}
  ▼
McpController
  │ 1. 从 Header 取 Token
  │ 2. 查 McpToken 表：Token 是否存在、Enable=true、未过期
  │    ↓ 失败 → 返回 -32001
  │ 3. 更新 McpToken.LastTime/LastIP/CallCount
  │ 4. 解析 JSON-RPC method
  │    ├─ initialize → 返回 serverInfo
  │    ├─ tools.list → 返回 5 个固定工具
  │    └─ tools.call {name, arguments}
  │         ├─ list_authorized_resources → 直接返回 Token 授权资源
  │         ├─ search_resources {keyword, resource_type?} → 按 Token 授权范围过滤返回搜索结果
  │         ├─ list_actions → 返回 McpActionSet 过滤后的动作清单
  │         ├─ get_resource {resource_type, resource_id}
  │         │    │ 5. 框架层资源授权校验
  │         │    │ 6. 路由到对应资源 provider 返回详情
  │         │    └─ 返回 content
  │         └─ invoke_action {action_name, params}
  │              │ 7. 查找 action，未找到 → -32601
  │              │ 8. 校验入参 input_schema，失败 → -32602
  │              │ 9. **框架层资源授权校验**（仅此处，action 内不再校验）：
  │              │    - 根据 action 的 required_resource 声明
  │              │    - 从 params 提取资源 ID
  │              │    - 查 McpTokenResource 判断是否在授权范围
  │              │    - 未授权 → -32003
  │              │ 10. 调用 action 的 InvokeAsync
  │              │ 11. 写 McpAudit 审计日志
  │              └─ 返回 content
  ▼
MCP 客户端
```

### 4. 开关配置

在 `StarServerSetting` 中新增：

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `EnableMcp` | Boolean | `false` | MCP 服务总开关。关闭时 `/mcp` 返回 404 |
| `McpActionSet` | String | `*` | 启用的动作集，逗号分隔；`*` 表示全部启用；如 `node,app,config` 表示只启用这三类模块的动作 |

> Token 本身有 `Enable` 字段可单独禁用；`EnableMcp` 是全局开关，关闭后所有 Token 都无法调用。

### 5. 动作接口设计（代码驱动自动注册）

```csharp
public interface IMcpAction
{
    String Name { get; }                          // 动作名（snake_case，如 node_send_command）
    String Description { get; }                   // 动作描述（面向 LLM）
    String Module { get; }                        // 所属模块（node/app/config/deploy/gateway/monitor/system）
    Object InputSchema { get; }                   // JSON Schema 参数定义
    ResourceRequirement RequiredResource { get; } // 资源依赖声明（null 表示无资源依赖）
    Task<Object> InvokeAsync(JsonElement params, CancellationToken ct);
}

public abstract class McpActionBase : IMcpAction { /* 公共实现 */ }

public sealed class ResourceRequirement
{
    public String ResourceType { get; init; }     // project / node / app
    public String IdField { get; init; }          // params 中资源 ID 的字段名（如 "node_id"）
    public Boolean Indirect { get; init; }        // true=资源 ID 需通过中间实体查 ProjectId/AppId（如 deploy_id → AppDeploy.ProjectId）
    public String IndirectEntity { get; init; }   // 间接查找的实体（如 "AppDeploy"）
}
```

**注册机制**：`McpService` 启动时通过反射扫描所有实现 `IMcpAction` 的类，自动注册到内存动作表。新增功能时只需添加一个实现类即可暴露为新动作，无需修改 `McpService` 或任何配置文件。

`McpService` 在调用 action 前根据 `RequiredResource` 声明做框架层统一资源授权校验，action 实现内**不再重复校验**授权（仅做业务合法性校验如"deploy 是否存在"）。

### 6. 首批动作清单（~27 个）

按模块组织，动作名采用 `{module}_{action}` 蛇形命名。LLM 通过 `list_actions` 自发现，通过 `invoke_action` 调用。

#### 6.1 节点管理（node）— 4 个

| 动作名 | 描述 | 参数 | 资源依赖 |
|---|---|---|---|
| `node_list_online` | 列出在线节点 | `project_id?`、`keyword?`、`page?`、`page_size?` | 无（按 Token 授权项目过滤返回） |
| `node_send_command` | 向节点下发命令 | `node_id`、`command`、`args?`、`expire?` | node / `node_id` |
| `node_upgrade` | 触发节点升级检查 | `node_id`、`channel?` | node / `node_id` |
| `node_search` | 按名称/IP/编码搜索节点 | `keyword`、`project_id?` | 无（按 Token 授权项目过滤返回） |

> 节点详情通过 `get_resource(node, id)` 获取，无需重复 action。通用跨类型搜索通过 `search_resources` 工具完成，`node_search` 保留用于节点专属搜索（支持 IP/编码等节点特有字段）。

#### 6.2 应用管理（app）— 7 个

| 动作名 | 描述 | 参数 | 资源依赖 |
|---|---|---|---|
| `app_list_online` | 列出在线应用 | `project_id?`、`keyword?`、`page?`、`page_size?` | 无（按 Token 授权项目过滤返回） |
| `app_send_command` | 向应用下发命令 | `app_id`、`command`、`args?` | app / `app_id` |
| `app_resolve_service` | 解析服务地址 | `service_name`、`key?` | 无（公开服务发现） |
| `app_search_service` | 搜索已注册服务 | `service_name?`、`key?` | 无（公开服务发现） |
| `app_restart` | 重启应用 | `app_id` | app / `app_id` |
| `app_stop` | 停止应用 | `app_id` | app / `app_id` |
| `app_start` | 启动应用 | `app_id` | app / `app_id` |

> 应用详情通过 `get_resource(app, id)` 获取。`app_restart`/`app_stop`/`app_start` 通过 `app_send_command` 下发 `restart`/`stop`/`start` 命令实现，但作为独立 action 暴露便于 LLM 直接调用且语义清晰。

#### 6.3 配置中心（config）— 2 个

| 动作名 | 描述 | 参数 | 资源依赖 |
|---|---|---|---|
| `config_get` | 获取应用配置 | `app_id`、`scope?` | app / `app_id` |
| `config_set` | 设置应用配置项 | `app_id`、`key`、`value`、`scope?` | app / `app_id` |

#### 6.4 远程发布（deploy）— 9 个

| 动作名 | 描述 | 参数 | 资源依赖 |
|---|---|---|---|
| `deploy_list` | 列出应用部署集 | `project_id?`、`keyword?`、`page?`、`page_size?` | 无（按 Token 授权项目过滤返回） |
| `deploy_compile` | 触发编译（不部署） | `deploy_id`、`node_id?`（编译节点） | project（间接：deploy_id → AppDeploy.ProjectId）+ node / `node_id`（若传） |
| `deploy_list_versions` | 列出部署版本 | `deploy_id`、`page?`、`page_size?` | project（间接：deploy_id → AppDeploy.ProjectId） |
| `deploy_list_history` | 列出部署历史 | `deploy_id`、`page?`、`page_size?` | project（间接：deploy_id → AppDeploy.ProjectId） |
| `deploy_list_nodes` | 列出部署目标节点 | `deploy_id` | project（间接：deploy_id → AppDeploy.ProjectId） |
| `deploy_install` | 触发部署到指定节点 | `deploy_id`、`node_id` | project（间接：deploy_id → AppDeploy.ProjectId）+ node / `node_id` |
| `pipeline_trigger` | 手动触发流水线 | `pipeline_id`、`branch?` | project（间接：pipeline_id → AppPipeline.ProjectId） |
| `pipeline_get_run` | 查询流水线运行状态 | `run_id` | project（间接：run_id → AppPipelineRun.ProjectId） |
| `pipeline_cancel` | 取消正在运行的流水线 | `run_id` | project（间接：run_id → AppPipelineRun.ProjectId） |

> 部署集详情通过 `get_resource(deploy, id)` 获取；流水线详情通过 `get_resource(pipeline, id)` 获取。`deploy_compile` 对应 `DeployService.Compile`，仅编译产物不执行 install。

#### 6.5 网关管理（gateway）— 2 个

| 动作名 | 描述 | 参数 | 资源依赖 |
|---|---|---|---|
| `gateway_list_routes` | 列出网关路由 | `keyword?`、`page?`、`page_size?` | 无 |
| `gateway_list_clusters` | 列出网关集群 | 无 | 无 |

> 网关全局配置为只读，通过 `get_resource` 暂不暴露（无强授权需求），如需可后续扩展 `gateway_get_config` action。

#### 6.6 监控中心（monitor）— 2 个

| 动作名 | 描述 | 参数 | 资源依赖 |
|---|---|---|---|
| `monitor_trace_search` | 搜索调用链 | `app_id?`、`keyword?`、`page?`、`page_size?` | app / `app_id`（若传 app_id） |
| `monitor_alarm_list` | 列出告警记录 | `app_id?`、`page?`、`page_size?` | app / `app_id`（若传 app_id） |

> 应用每日统计通过 `get_resource` 暂不暴露（可后续扩展 `monitor_app_stat` action）。

#### 6.7 系统（system）— 1 个

| 动作名 | 描述 | 参数 | 资源依赖 |
|---|---|---|---|
| `get_audit_log` | 查询当前 Token 最近的调用记录（用于 LLM 自省和多轮对话回顾上下文） | `page?`、`page_size?`、`success?`（过滤成功/失败） | 无（仅返回当前 Token 的审计记录） |

> 原 `system_info` / `system_list_apis` / `system_list_redis_nodes` / `system_list_mysql_nodes` 等枚举类工具去掉，信息可由 `list_actions` 或 `search_resources` 间接获得。`search_resources` 已提升为 MCP 工具，不再作为 action。

**总计 27 个 action**（4+7+2+9+2+2+1），加上 5 个 MCP 工具，LLM 可完成节点/应用/配置/部署/网关/监控的全部运维操作。

### 7. 扩展机制（代码驱动）

新增功能时暴露为 MCP 动作的步骤：

1. 在 `Stardust.Server/Mcp/Actions/` 下新建类，实现 `IMcpAction`（或继承 `McpActionBase`）
2. 填写 `Name` / `Description` / `Module` / `InputSchema` / `RequiredResource`
3. 实现 `InvokeAsync` 方法（仅业务逻辑，无需授权校验）
4. 重启 Stardust.Server，`McpService` 自动反射注册，`list_actions` 即可见

**无需**：修改 `McpController`、修改 `McpService` 注册代码、修改数据库配置、修改前端代码。

### 8. 审计日志

新增 `McpAudit` 表：

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Int64 | 主键 |
| TokenId | Int32 | 关联 `McpToken.Id`（索引） |
| TokenName | String(50) | Token 名称快照（便于 Token 删除后审计） |
| ToolName | String(50) | 工具名（`list_authorized_resources` / `get_resource` / `list_actions` / `invoke_action`） |
| ActionName | String(50) | 动作名（仅 `invoke_action` 时填写，如 `node_send_command`） |
| CallerIp | String(50) | 调用方 IP |
| CallerUserAgent | String(200) | 客户端 UA |
| Arguments | String(2000) | 入参 JSON（截断 2000 字符，敏感字段脱敏） |
| Success | Boolean | 是否成功 |
| ErrorMessage | String(500) | 失败原因 |
| Duration | Int32 | 耗时（ms） |
| CreateTime | DateTime | 调用时间 |

## User Scenarios & Testing

### 场景 1：LLM 首次接入建立上下文

**前置**：管理员创建了 Token "运维助手-小明"，勾选授权"项目 A"和"项目 B"

1. LLM 调用 `initialize`，服务端返回 `serverInfo`
2. LLM 调用 `list_authorized_resources`，返回项目 A、B 的标识符和说明
3. LLM 调用 `list_actions`，返回 27 个可用动作及参数 schema
4. LLM 建立完整上下文：知道能访问什么资源、能做什么操作

**验证**：
- `list_authorized_resources` 不返回项目 C（即使数据库中有）
- `list_actions` 返回所有 27 个动作的完整元信息

### 场景 2：LLM 查询并操作节点

1. LLM 调用 `invoke_action(node_list_online, {})`，框架层无资源依赖，action 内按 Token 授权项目过滤返回节点列表
2. LLM 拿到节点 ID 后调用 `get_resource(node, 100)`，框架层校验 node 100 在授权范围，返回节点详情
3. LLM 调用 `invoke_action(node_send_command, {node_id:100, command:"restart"})`
4. 框架层校验 node_id=100 在 Token 授权范围 ✓，调用 action 执行

**验证**：
- 节点列表只返回项目 A、B 下的节点
- 未授权节点 `get_resource` 返回 `-32003`
- 未授权节点 `invoke_action(node_send_command)` 返回 `-32003`

### 场景 3：LLM 触发部署（写操作，间接资源校验）

**前置**：Token 授权了"项目 A"和"节点 X"（节点 X 属于项目 B）

1. LLM 调用 `invoke_action(deploy_install, {deploy_id:100, node_id:X})`
2. 框架层校验：
   - `deploy_id=100` → 查 `AppDeploy.ProjectId=A` → 在授权范围 ✓
   - `node_id=X` → 在授权范围 ✓
3. 调用 `StarFactory.SendNodeCommand(install)`

**验证**：
- 若 `deploy_id` 属于未授权项目 C → 返回 `-32003`
- 若 `node_id` 未授权 → 返回 `-32003`
- 审计日志记录入参摘要和结果

### 场景 4：LLM 跨类型搜索资源

1. LLM 调用 `search_resources({keyword:"订单"})` 工具
2. 工具跨 project/node/app/deploy/pipeline/service 六类资源搜索
3. 按 Token 授权范围过滤返回匹配结果

**验证**：
- 返回结果只包含 Token 授权范围内的资源
- 未授权资源不出现在结果中

### 场景 5：管理员创建 Token 并勾选资源

1. 管理员在 Web `/Platform/McpToken` 页面点"新建"
2. 填写名称"运维助手-测试"，勾选资源：
   - 项目：勾选"项目 A"、"项目 B"
   - 节点：勾选"节点 X"（属于项目 C）
   - 应用：勾选"全部应用"（IsAll=true）
3. 保存后系统生成 64 位 Token 字符串并显示一次
4. 管理员复制 Token 配置给 LLM 客户端

**验证**：
- Token 字符串创建后不可修改
- 资源授权关系写入 `McpTokenResource` 表
- `McpTokenResource` 中应用类型有一条 `IsAll=true` 记录

### 场景 6：管理员禁用 Token

1. 管理员在 Web 将 Token "运维助手-测试" 的 `Enable` 改为 `false`
2. LLM 客户端使用该 Token 调用 `/mcp`
3. 服务端返回 `-32001`（未授权）

**验证**：
- 禁用立即生效，无需重启
- 审计日志记录禁用后的失败调用

### 场景 7：管理员禁用某类动作

1. 管理员在 Web 设置页将 `McpActionSet` 改为 `node,app,config`
2. 保存后，`list_actions` 只返回这三类模块的动作
3. `deploy_*` / `gateway_*` 等动作不可见且不可调用

**验证**：
- `list_actions` 结果按配置过滤
- 被禁用的动作调用返回 `-32601`（method not found）

### 场景 8：Token 过期

1. 管理员创建 Token 时设置 `ExpireTime=2026-12-31`
2. 2027-01-01 时 LLM 调用 `/mcp`
3. 服务端返回 `-32001`（Token 已过期）

**验证**：
- 过期检查精确到秒
- 审计日志记录"Token 已过期"

### 场景 9：扩展新动作

1. 开发者在 `Stardust.Server/Mcp/Actions/` 新建 `RedisClearAction.cs`，实现 `IMcpAction`
2. 重启 Stardust.Server
3. LLM 调用 `list_actions`，返回结果中包含新动作 `redis_clear`
4. LLM 调用 `invoke_action(redis_clear, {...})` 执行

**验证**：
- 新动作自动注册，无需修改 `McpService`
- `list_actions` 准确反映新动作
- 框架层资源授权校验对新动作生效

## 功能需求

### FR1：MCP 协议端点

- FR1.1：Stardust.Server 提供 `POST /mcp` 端点，接受 JSON-RPC 2.0 请求
- FR1.2：实现 `initialize` 方法，返回 `serverInfo`（name=`Stardust`）和 `capabilities`
- FR1.3：实现 `tools.list` 方法，返回 5 个固定工具（`list_authorized_resources` / `search_resources` / `get_resource` / `list_actions` / `invoke_action`）
- FR1.4：实现 `tools.call` 方法，根据 `name` 路由到对应工具并返回结果
- FR1.5：未识别的 method 返回 JSON-RPC `-32601`
- FR1.6：JSON 解析失败返回 JSON-RPC `-32700`

### FR2：Token 凭证管理

- FR2.1：新增 `McpToken` 表，字段见"核心设计 3.1"
- FR2.2：Web 端提供 Token 列表/新建/编辑/删除/重置页面（`/Platform/McpToken`）
- FR2.3：Token 字符串在创建时自动生成 64 位随机字符，创建后不可修改
- FR2.4：Token 支持 `Enable` 开关，禁用后所有调用立即返回 `-32001`
- FR2.5：Token 支持 `ExpireTime`，过期后所有调用返回 `-32001`
- FR2.6：Token 支持"重置"操作：生成新 Token 字符串，旧 Token 立即失效
- FR2.7：Token 删除时级联删除 `McpTokenResource` 关联记录（`McpAudit` 保留）

### FR3：资源授权管理

- FR3.1：新增 `McpTokenResource` 表，字段见"核心设计 3.2"
- FR3.2：Web 端 Token 表单支持勾选授权资源，分三类：项目、节点、应用
- FR3.3：每类资源支持"全部资源"勾选（`IsAll=true`），勾选后该类型所有资源均授权（含未来新增）
- FR3.4：资源授权支持 `Enable` 开关，可临时禁用某条授权而不删除
- FR3.5：项目授权语义：授权项目 → 该项目下所有节点/应用/部署集/流水线/配置均可访问
- FR3.6：节点授权语义：授权节点 → 该节点可单独操作（独立于项目授权）
- FR3.7：应用授权语义：授权应用 → 该应用可单独操作（独立于项目授权）

### FR4：鉴权与授权校验（框架层统一）

- FR4.1：所有 `/mcp` 请求必须携带 `Authorization: Bearer {token}` Header
- FR4.2：Token 不存在 / `Enable=false` / 已过期 → 返回 JSON-RPC `-32001`
- FR4.3：`get_resource` 时框架层根据 `resource_type` 校验资源 ID 是否在 Token 授权范围
- FR4.4：`invoke_action` 时框架层根据 action 的 `RequiredResource` 声明，从 params 提取资源 ID 校验
- FR4.5：资源 ID 不在 Token 授权范围 → 返回 JSON-RPC `-32003`（无权限）
- FR4.6：间接资源校验：如 `deploy_id` 需通过 `AppDeploy.ProjectId` 反查项目授权
- FR4.7：列表类 action（如 `node_list_online`）按 Token 授权范围过滤返回数据，不返回未授权资源
- FR4.8：`deploy_install` 同时校验 project（间接）+ node 两类资源，任一未授权即返回 `-32003`
- FR4.9：action 实现内部**不再重复**资源授权校验，仅做业务合法性校验

### FR5：开关配置

- FR5.1：`StarServerSetting.EnableMcp` 默认 `false`，关闭时 `/mcp` 返回 404
- FR5.2：`StarServerSetting.McpActionSet` 默认 `*`（全部启用），支持逗号分隔的模块名
- FR5.3：配置变更立即生效（无需重启）

### FR6：MCP 工具实现（5 个）

- FR6.1：实现 `list_authorized_resources` 工具，按 `resource_type?` 过滤返回 Token 授权资源
- FR6.2：实现 `search_resources` 工具，按 `keyword` + `resource_type?` 跨类型搜索，结果按 Token 授权范围过滤
- FR6.3：实现 `get_resource` 工具，支持 6 类资源（project/node/app/deploy/pipeline/service）
- FR6.4：实现 `list_actions` 工具，按 `module?` 过滤返回动作清单（含 input_schema 和 required_resource）
- FR6.5：实现 `invoke_action` 工具，根据 `action_name` 路由到 `IMcpAction` 实现

### FR7：动作接口与注册（代码驱动）

- FR7.1：定义 `IMcpAction` 接口，含 Name/Description/Module/InputSchema/RequiredResource/InvokeAsync
- FR7.2：`McpService` 启动时通过反射扫描所有 `IMcpAction` 实现类，自动注册到内存动作表
- FR7.3：`list_actions` 只返回 `McpActionSet` 允许的动作
- FR7.4：被禁用的动作调用返回 `-32601`
- FR7.5：动作入参按 `InputSchema` 校验，校验失败返回 `-32602`（invalid params）
- FR7.6：新增动作只需添加实现类，无需修改 `McpService` 或任何配置文件

### FR8：动作实现（首批 27 个）

- FR8.1：实现节点管理类动作 4 个（`node_list_online` / `node_send_command` / `node_upgrade` / `node_search`）
- FR8.2：实现应用管理类动作 7 个（`app_list_online` / `app_send_command` / `app_resolve_service` / `app_search_service` / `app_restart` / `app_stop` / `app_start`）
- FR8.3：实现配置中心类动作 2 个（`config_get` / `config_set`）
- FR8.4：实现远程发布类动作 9 个（`deploy_list` / `deploy_compile` / `deploy_list_versions` / `deploy_list_history` / `deploy_list_nodes` / `deploy_install` / `pipeline_trigger` / `pipeline_get_run` / `pipeline_cancel`）
- FR8.5：实现网关管理类动作 2 个（`gateway_list_routes` / `gateway_list_clusters`）
- FR8.6：实现监控中心类动作 2 个（`monitor_trace_search` / `monitor_alarm_list`）
- FR8.7：实现系统类动作 1 个（`get_audit_log`）
- FR8.8：列表类动作必须按 Token 授权范围过滤返回数据，不返回未授权资源
- FR8.9：`app_restart`/`app_stop`/`app_start` 通过 `app_send_command` 下发对应命令实现，资源依赖为 app / `app_id`

### FR9：审计日志

- FR9.1：新增 `McpAudit` 表，字段见"核心设计 8"
- FR9.2：每次 `tools.call` 在 `McpAudit` 记录一条（含 TokenId/TokenName/ToolName/ActionName/CallerIp/Arguments/Success/ErrorMessage/Duration）
- FR9.3：`Arguments` 截断 2000 字符，敏感字段（`password`/`secret`/`token`）脱敏
- FR9.4：Web 端提供 `McpAudit` 列表页面（`/Platform/McpAudit`），支持按 TokenId/工具名/动作名/时间/成功与否筛选
- FR9.5：审计日志记录失败时不影响主调用返回

### FR10：错误处理

- FR10.1：所有动作异常被 `McpService` 捕获，转换为 JSON-RPC `-32000`（server error）+ 错误信息
- FR10.2：超时（默认 30 秒）返回 `-32002`
- FR10.3：参数缺失返回 `-32602`
- FR10.4：资源未授权返回 `-32003`
- FR10.5：动作未找到返回 `-32601`

## 非功能需求

- 性能：单次工具调用 P95 < 500ms（不含下游 RPC 等待）
- 并发：支持 50 并发 MCP 调用
- 安全：Token 字符串不在日志中明文打印；审计日志入参脱敏；Token 校验使用恒定时间比较防时序攻击
- 兼容：MCP 协议版本 `2024-11-05`，兼容主流客户端（Claude Desktop / Trae / Cursor）
- 扩展性：新增动作仅需添加一个 `IMcpAction` 实现类，零配置零侵入

## Success Criteria

1. **可调用**：LLM 客户端通过 5 个 MCP 工具 + 27 个 action 可完成节点/应用/配置/部署/网关/监控的全部运维操作
2. **可治理**：管理员可在 Web 设置页 1 步关闭 MCP 服务（`EnableMcp=false`），关闭后所有 `/mcp` 请求返回 404
3. **可管控**：管理员可按模块启用/禁用动作子集（如只开 `node,app` 类），`list_actions` 准确反映配置
4. **可授权**：管理员创建 Token 时可勾选授权的项目/节点/应用，未授权资源 100% 不可访问（查询不返回、操作被拒绝）
5. **可禁用**：Token 禁用后立即失效，所有调用返回 `-32001`；Token 过期后同样立即失效
6. **可追溯**：每次工具调用在 `McpAudit` 表有完整记录（含 TokenId/ActionName），可通过 Web 页面筛选查看
7. **安全**：无 Token / Token 错误 / Token 禁用 / Token 过期 / 资源未授权的调用 100% 被拒绝
8. **可扩展**：新增动作仅需添加一个 `IMcpAction` 实现类并重启，无需修改框架代码或配置文件
9. **零回归**：现有 6600 端口的 RPC 接口、Web 控制台、设备登录/心跳链路无任何行为变化

## Key Entities

- **McpToken**（新增）：MCP 调用凭证，含 Enable / ExpireTime / 调用统计
- **McpTokenResource**（新增）：Token 与资源（项目/节点/应用）的授权关系
- **McpAudit**（新增）：MCP 工具调用审计日志（含 ActionName 字段）
- **StarServerSetting**（扩展）：新增 `EnableMcp`（默认 `false`）/ `McpActionSet` 配置字段
- **IMcpAction**（新增）：MCP 动作抽象接口（代码驱动自动注册）
- **ResourceRequirement**（新增）：动作的资源依赖声明
- **McpService**（新增）：MCP 工具路由、Token 校验、框架层资源授权、动作反射注册

## Assumptions

- 假设 MCP 客户端均支持 JSON-RPC 2.0 over HTTP 的基础传输模式
- 假设 4 个 MCP 工具 + 22 个 action 已能覆盖 80% 的 LLM 运维场景，后续按需代码扩展
- 假设 Stardust 服务端与 LLM 客户端在同一可信网络或通过 HTTPS 反向代理对外，Token 通过 HTTPS Header 传输保证机密性
- 假设 `McpAudit` 表数据量可控（日均 < 10 万条），无需单独分表
- 假设现有 `[ApiFilter]` 鉴权链路不被 MCP 端点复用，MCP 端点使用独立的资源授权 Token 体系
- 假设资源授权校验在 `McpService` 框架层统一完成，action 实现内仅做业务合法性校验（如"实体是否存在"），不再重复资源授权校验
- 假设 `McpToken` 与 `McpTokenResource` 与现有 `GalaxyProject`/`Node`/`App` 实体通过 `ResourceId` 弱关联（不建数据库外键）
- 假设间接资源校验（如 deploy_id → AppDeploy.ProjectId）通过框架层查询实体表实现，action 无需感知
- 假设动作注册采用反射扫描程序集方式，启动时一次性加载（不做动态热加载，避免复杂度）

## Out of Scope

- SSE 长连接与流式响应（`notifications/progress`）：后续按需扩展
- MCP `resources` / `prompts` 协议方法：本期只实现 `tools`
- MCP 客户端 SDK：本期只实现服务端
- 工具调用的速率限制：本期不实现
- 现有接口的鉴权改造（如给 `NodeController.SendCommand` 补鉴权）：不在本期范围
- 复用 Web 用户登录态（Cube Cookie/Session）：不复用，MCP Token 是独立体系
- Token 调用方 IP 白名单：本期不实现
- 动态热加载动作：本期仅支持重启后生效
- 动作的细粒度 RBAC（基于调用方身份限制可调动作）：本期通过 `McpActionSet` 全局配置 + 资源授权控制
