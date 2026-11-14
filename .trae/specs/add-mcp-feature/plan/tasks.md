# Tasks — MCP HTTP 服务

**Feature**: MCP HTTP 服务（5 工具 + 27 action）
**Created**: 2026-07-16
**Spec**: [spec.md](../spec.md)
**Plan**: [plan.md](./plan.md)

> ⚠️ **废弃说明（2026-08-03）**：本文及同目录文档中提到的 `McpController`（HTTP 端点 Controller）**已废弃并删除**，改为 `McpMiddleware`（ASP.NET 中间件）承载 `/mcp` 端点。
> **缘由**：`McpMiddleware` 在管线中注册于 `UseCube` 之前，对 `/mcp` 短路处理，可避免 Cube 的 device-id Cookie 解析（`Convert.FromBase64String`）在畸形 Cookie 时抛 `FormatException` 返回错误页，导致 MCP 客户端收到 HTML 而非 JSON-RPC。而 `McpController` 经 MVC 路由位于 `UseCube` 之后，在现有管线中始终不可达（死代码），故删除。详见 `Doc/MCP-3-客户端接入.md`。

## 概述

本文档定义 MCP HTTP 服务的实施任务清单，按阶段组织，每个任务可独立执行和验证。

- **总任务数**：78
- **阶段**：Setup（4）→ Foundational（11）→ US1-9（57）→ Polish（6）
- **MVP 范围**：US1（MCP 协议端点 + Token 鉴权 + 基础工具）

---

## Phase 1: Setup（项目初始化）

> 阶段目标：准备数据模型、配置项、接口抽象。

- [x] T001 [P] 在 `Stardust.Data/Platform/Model.xml` 末尾追加 `McpToken` 表定义（16 字段 + 3 索引）
- [x] T002 [P] 在 `Stardust.Data/Platform/Model.xml` 末尾追加 `McpTokenResource` 表定义（9 字段 + 3 索引）
- [x] T003 [P] 在 `Stardust.Data/Platform/Model.xml` 末尾追加 `McpAudit` 表定义（13 字段 + 5 索引）
- [x] T004 在 `Stardust.Data/` 目录执行 `xcode`（或 `xcodetool.bat`）自动生成 6 个 .cs 文件 + 3 个 Cube Controller 骨架

---

## Phase 2: Foundational（阻塞性基础）

> 阶段目标：实现配置、接口抽象、Token 业务方法、McpService 骨架。**必须先于所有 User Story 完成**。

- [x] T005 [P] 在 `Stardust.Server/Setting.cs` 的 `StarServerSetting` 类中新增 `EnableMcp`（默认 false）和 `McpActionSet`（默认 `*`）字段
- [x] T006 [P] 新建 `Stardust.Web/Mcp/IMcpAction.cs`，定义 `IMcpAction` 接口、`McpActionBase` 抽象基类、`ResourceRequirement` sealed 类
- [x] T007 [P] 新建 `Stardust.Web/Mcp/McpContext.cs`，定义调用上下文（TokenId/TokenName/CallerIp/UserAgent/TraceId）
- [x] T008 [P] 新建 `Stardust.Web/Mcp/IResourceProvider.cs`，定义 `IResourceProvider` 接口（`ResourceType` 属性 + `GetAsync(Int32 id)` 方法）
- [x] T009 手写 `Stardust.Data/Platform/MCP令牌.Biz.cs` 业务方法：`GenerateToken`/`FindByToken`/`Reset`/`IsValid`/`RecordCall`/`SafeEquals`（恒定时间比较）
- [x] T010 手写 `Stardust.Data/Platform/MCP令牌资源.Biz.cs` 业务方法：`FindAllByToken`/`IsAuthorized`/`GetAuthorizedProjectIds`/`SyncByToken`
- [x] T011 手写 `Stardust.Data/Platform/MCP审计日志.Biz.cs` 业务方法：`Search`/`WriteAsync`（异步写入）/`SanitizeArguments`（脱敏）
- [x] T012 新建 `Stardust.Web/Services/McpService.cs`：构造函数注入 `IServiceProvider`/`StarServerSetting`，反射扫描注册 `IMcpAction` 到 `ConcurrentDictionary`，注册间接资源解析器字典（`AppDeploy`/`AppPipeline`/`AppPipelineRun` → ProjectId）
- [x] T013 在 `Stardust.Web/Startup.cs` 的 `ConfigureServices` 中添加 `services.AddSingleton<McpService>()`
- [x] T014 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleAsync(String body, String ip, String ua)` 主入口：JSON-RPC 解析 + Token 校验 + 路由到 `initialize`/`tools.list`/`tools.call`，异常包装为 JSON-RPC error
- [x] T015 在 `Stardust.Web/Services/McpService.cs` 实现框架层资源授权校验方法 `ValidateResourceAccess(Int32 tokenId, ResourceRequirement req, JsonElement params)`：直接校验 + 间接校验（通过 `_indirectResolvers`）

---

## Phase 3: User Story 1 — MCP 协议端点 + 基础工具

> **Story Goal**：LLM 客户端可连接 `/mcp`，完成协议握手、查授权资源、发现动作、调用动作。
> **Independent Test**：`curl POST /mcp` 完成 `initialize` + `list_authorized_resources` + `list_actions` + `invoke_action`（调用 `get_audit_log` 空查）。
> **MVP Scope**：本阶段完成后即可对接 MCP 客户端做基本验证。

- [x] T016 [US1] 新建 `Stardust.Web/Controllers/McpController.cs`：`[ApiController]` + `[Route("/mcp")]` + `[HttpPost]`，检查 `EnableMcp`→读取 body→调用 `mcpService.HandleAsync`→返回 `JsonResult`，不使用 `[ApiFilter]`
- [x] T017 [US1] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleInitialize` 方法：返回 `serverInfo`（name=Stardust, version=1.0.0）+ `capabilities.tools`
- [x] T018 [US1] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleToolsList` 方法：返回 5 个固定工具的 name/description/inputSchema（JSON Schema 定义见 contracts/mcp-protocol.md）
- [x] T019 [US1] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleToolsCall` 方法分发器：根据 `name` 路由到 `HandleListAuthorizedResources`/`HandleSearchResources`/`HandleGetResource`/`HandleListActions`/`HandleInvokeAction`
- [x] T020 [US1] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleListAuthorizedResources` 工具：查 `McpTokenResource.FindAllByToken`，关联 `GalaxyProject`/`Node`/`App` 返回资源标识符和说明，支持 `resource_type?` 过滤
- [x] T021 [US1] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleListActions` 工具：遍历 `_actions` 字典按 `McpActionSet` 过滤，返回 name/description/module/input_schema/required_resource，支持 `module?` 过滤
- [x] T022 [US1] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleInvokeAction` 工具骨架：action 查找（未找到返回 -32601）+ inputSchema 校验（失败返回 -32602）+ `ValidateResourceAccess` + 调用 `InvokeAsync` + 审计日志写入
- [x] T023 [US1] 在 `Stardust.Web/Services/McpService.cs` 实现审计日志写入：每次 `tools.call` 调用 `McpAudit.WriteAsync`，记录 TokenId/TokenName/ToolName/ActionName/CallerIp/Arguments/Success/Duration
- [x] T024 [US1] 新建 `Stardust.Web/Mcp/Actions/System/GetAuditLogAction.cs` 实现 `get_audit_log` action：按当前 `McpContext.TokenId` 查 `McpAudit.Search`，支持 `page?`/`page_size?`/`success?` 参数

---

## Phase 4: User Story 2 — 搜索与获取资源

> **Story Goal**：LLM 可通过 `search_resources` 按关键字查找资源，通过 `get_resource` 按 ID 获取详情。
> **Independent Test**：`search_resources(keyword="测试")` 返回匹配资源 → `get_resource(node, 100)` 返回节点详情 → 未授权资源返回 -32003。

- [x] T025 [US2] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleSearchResources` 工具：跨 6 类资源（project/node/app/deploy/pipeline/service）搜索，按 Token 授权范围过滤返回，支持 `keyword` + `resource_type?`
- [x] T026 [US2] 在 `Stardust.Web/Services/McpService.cs` 实现 `HandleGetResource` 工具：根据 `resource_type` 路由到 `IResourceProvider`，框架层校验资源 ID 在授权范围（直接/间接）
- [x] T027 [P] [US2] 新建 `Stardust.Web/Mcp/Resources/ProjectResourceProvider.cs` 实现 `IResourceProvider`：`GalaxyProject.FindById` 返回项目详情
- [x] T028 [P] [US2] 新建 `Stardust.Web/Mcp/Resources/NodeResourceProvider.cs`：`Node.FindByID` 返回节点详情
- [x] T029 [P] [US2] 新建 `Stardust.Web/Mcp/Resources/AppResourceProvider.cs`：`App.FindById` 返回应用详情
- [x] T030 [P] [US2] 新建 `Stardust.Web/Mcp/Resources/DeployResourceProvider.cs`：`AppDeploy.FindById` 返回部署集详情
- [x] T031 [P] [US2] 新建 `Stardust.Web/Mcp/Resources/PipelineResourceProvider.cs`：`AppPipeline.FindById` 返回流水线详情
- [x] T032 [P] [US2] 新建 `Stardust.Web/Mcp/Resources/ServiceResourceProvider.cs`：`AppService.FindById` 返回服务详情
- [x] T033 [US2] 在 `Stardust.Web/Services/McpService.cs` 注册 6 个 `IResourceProvider` 到 `Dictionary<String, IResourceProvider>`（反射扫描或手动注册）

---

## Phase 5: User Story 3 — 节点管理 Action（4 个）

> **Story Goal**：LLM 可查询在线节点、搜索节点、下发命令、触发升级。
> **Independent Test**：`invoke_action(node_list_online, {})` 返回节点列表 → `invoke_action(node_send_command, {node_id, command:"ping"})` 返回成功。

- [x] T034 [P] [US3] 新建 `Stardust.Web/Mcp/Actions/Nodes/NodeListOnlineAction.cs`：调用 `NodeOnline.Search`，按 Token 授权项目过滤，支持 `project_id?`/`keyword?`/`page?`/`page_size?`
- [x] T035 [P] [US3] 新建 `Stardust.Web/Mcp/Actions/Nodes/NodeSearchAction.cs`：调用 `Node.Search`/`SearchByIP`，按授权项目过滤，支持 `keyword`/`project_id?`
- [x] T036 [P] [US3] 新建 `Stardust.Web/Mcp/Actions/Nodes/NodeSendCommandAction.cs`：调用 `StarFactory.SendNodeCommandAsync`（新版），RequiredResource=`{type:"node",field:"node_id"}`，支持 `node_id`/`command`/`args?`/`expire?`
- [x] T037 [P] [US3] 新建 `Stardust.Web/Mcp/Actions/Nodes/NodeUpgradeAction.cs`：调用 `StarFactory.SendNodeCommandAsync(nodeCode, "node/upgrade", ...)`，RequiredResource=`{type:"node",field:"node_id"}`，支持 `node_id`/`channel?`

---

## Phase 6: User Story 4 — 应用管理 Action（7 个）

> **Story Goal**：LLM 可查询在线应用、下发应用命令、解析服务、控制应用生命周期。
> **Independent Test**：`invoke_action(app_list_online, {})` → `invoke_action(app_restart, {app_id})` 返回成功。

- [x] T038 [P] [US4] 新建 `Stardust.Web/Mcp/Actions/Apps/AppListOnlineAction.cs`：调用 `AppOnline.Search`，按授权项目过滤
- [x] T039 [P] [US4] 新建 `Stardust.Web/Mcp/Actions/Apps/AppSendCommandAction.cs`：调用 `StarFactory.SendAppCommandAsync`，RequiredResource=`{type:"app",field:"app_id"}`
- [x] T040 [P] [US4] 新建 `Stardust.Web/Mcp/Actions/Apps/AppResolveServiceAction.cs`：内联实现服务解析（RegistryService 未链接到 Web 项目，改用 AppService.FindAllByService + Match + ToModel），无资源依赖
- [x] T041 [P] [US4] 新建 `Stardust.Web/Mcp/Actions/Apps/AppSearchServiceAction.cs`：调用 `AppService.Search`，无资源依赖
- [x] T042 [US4] 新建 `Stardust.Web/Mcp/Actions/Apps/AppControlActions.cs`：含 `AppControlActionBase` 基类 + `AppRestartAction`，通过 `DeployService.Control(action="restart")` 走部署节点路径，RequiredResource=`{type:"app",field:"app_id"}`
- [x] T043 [US4] 同文件 `AppControlActions.cs`：`AppStopAction`，action="stop"
- [x] T044 [US4] 同文件 `AppControlActions.cs`：`AppStartAction`，action="start"

---

## Phase 7: User Story 5 — 配置中心 Action（2 个）

> **Story Goal**：LLM 可查询和设置应用配置。
> **Independent Test**：`invoke_action(config_get, {app_id})` 返回配置字典 → `invoke_action(config_set, {app_id, key, value})` 返回成功。

- [x] T045 [P] [US5] 新建 `Stardust.Web/Mcp/Actions/Config/ConfigActions.cs`：`ConfigGetAction` 调用 `ConfigService.GetConfigs`，RequiredResource=`{type:"app",field:"app_id"}`
- [x] T046 [P] [US5] 同文件 `ConfigActions.cs`：`ConfigSetAction` 调用 `ConfigService.SetConfigs` + 可选 `Publish`，RequiredResource=`{type:"app",field:"app_id"}`

---

## Phase 8: User Story 6 — 远程发布 Action（9 个）

> **Story Goal**：LLM 可查询部署集、触发编译、部署、管理流水线。
> **Independent Test**：`invoke_action(deploy_list, {})` → `invoke_action(pipeline_trigger, {pipeline_id})` → `invoke_action(pipeline_get_run, {run_id})` → `invoke_action(pipeline_cancel, {run_id})`。

- [x] T047 [P] [US6] 新建 `Stardust.Web/Mcp/Actions/Deploy/DeployQueryActions.cs`：`DeployListAction` 调用 `AppDeploy.Search`，按授权项目过滤
- [x] T048 [P] [US6] 同文件 `DeployQueryActions.cs`：`DeployListVersionsAction` 调用 `AppDeployVersion.Search`，RequiredResource 间接（deploy_id→AppDeploy.ProjectId）
- [x] T049 [P] [US6] 同文件 `DeployQueryActions.cs`：`DeployListHistoryAction` 调用 `AppDeployHistory.Search`，RequiredResource 间接
- [x] T050 [P] [US6] 同文件 `DeployQueryActions.cs`：`DeployListNodesAction` 调用 `AppDeployNode.Search`，RequiredResource 间接
- [x] T051 [US6] 新建 `Stardust.Web/Mcp/Actions/Deploy/DeployControlActions.cs`：`DeployCompileAction` 调用 `DeployService.Compile`，RequiredResource 间接 + 可选 build_node_id
- [x] T052 [US6] 同文件 `DeployControlActions.cs`：`DeployInstallAction` 调用 `DeployService.Control(action="install")`，RequiredResource 间接（deploy_id→ProjectId）+ deploy_node_id
- [x] T053 [P] [US6] 新建 `Stardust.Web/Mcp/Actions/Deploy/PipelineActions.cs`：`PipelineTriggerAction` 调用 `PipelineService.Trigger`，RequiredResource 间接（pipeline_id→AppPipeline.ProjectId）
- [x] T054 [P] [US6] 同文件 `PipelineActions.cs`：`PipelineGetRunAction` 调用 `AppPipelineRun.FindById` + 查步骤，RequiredResource 间接（run_id→AppPipelineRun.PipelineId→AppPipeline.ProjectId）
- [x] T055 [US6] 在 `Stardust.Web/Services/PipelineService.cs` 新增 `Cancel(Int64 runId, String userHost)` 方法 + `_runningCts` 字典；在 `RunAsync` 编译前/部署前/每个节点前检查 `cancellationToken.IsCancellationRequested`；Trigger/Reprocess/HandleWebhookAsync 创建 CTS 并缓存；finally 清理 CTS
- [x] T056 [US6] 同文件 `PipelineActions.cs`：`PipelineCancelAction` 调用 `PipelineService.Cancel`，RequiredResource 间接（run_id→ProjectId）

---

## Phase 9: User Story 7 — 网关管理 Action（2 个）

> **Story Goal**：LLM 可查询网关路由和集群。
> **Independent Test**：`invoke_action(gateway_list_routes, {})` → `invoke_action(gateway_list_clusters, {})`。

- [x] T057 [P] [US7] 新建 `Stardust.Web/Mcp/Actions/Gateway/GatewayActions.cs`：`GatewayListRoutesAction` 调用 `GatewayRoute.Search`，无资源依赖
- [x] T058 [P] [US7] 同文件 `GatewayActions.cs`：`GatewayListClustersAction` 调用 `GatewayCluster.Search`，无资源依赖

---

## Phase 10: User Story 8 — 监控中心 Action（2 个）

> **Story Goal**：LLM 可搜索调用链和告警记录。
> **Independent Test**：`invoke_action(monitor_trace_search, {app_id})` → `invoke_action(monitor_alarm_list, {app_id})`。

- [x] T059 [P] [US8] 新建 `Stardust.Web/Mcp/Actions/Monitor/MonitorActions.cs`：`MonitorTraceSearchAction` 调用 `TraceData.Search`，RequiredResource=`{type:"app",field:"app_id",optional:true}`
- [x] T060 [P] [US8] 同文件 `MonitorActions.cs`：`MonitorAlarmListAction` 调用 `AlarmRecord.Search`（通过应用名匹配Action字段），RequiredResource=`{type:"app",field:"app_id",optional:true}`

---

## Phase 11: User Story 9 — Web 管理 Token 与资源授权

> **Story Goal**：管理员可在 Web 页面创建/编辑/禁用/重置 Token，勾选授权资源。
> **Independent Test**：Web `/Platform/McpToken` 新建 Token 勾选资源 → 用该 Token 调用 `/mcp` 鉴权通过 → 禁用后调用返回 -32001。

- [x] T061 [US9] 完善 `Stardust.Web/Areas/Platform/Controllers/McpTokenController.cs`：静态构造 `ListFields.RemoveField("Token",...)` 隐藏敏感字段；重写 `OnInsert` 自动生成 Token 字符串；`LogOnChange=true`
- [x] T062 [US9] 重写 `McpTokenController.OnInsert`/`OnUpdate`：同步 `McpTokenResource.SyncByToken`（删除旧授权+插入新授权）
- [x] T063 [US9] 在 `McpTokenController` 新增 `Reset(Int32 id)` Action：生成新 Token 字符串 + 旧 Token 立即失效 + 写历史记录
- [x] T064 [US9] 新建 `Stardust.Web/Areas/Platform/Views/McpToken/_Form_Body.cshtml`：遍历 `GalaxyProject.FindAllWithCache()`/`Node.FindAllWithCache()`/`App.FindAllWithCache()` 渲染三组勾选框，每组支持"全部资源"勾选（IsAll=true）
- [x] T065 [US9] 完善 `Stardust.Web/Areas/Platform/Controllers/McpTokenResourceController.cs`：静态构造定制列表字段，通常仅查看
- [x] T066 [US9] 完善 `Stardust.Web/Areas/Platform/Controllers/McpAuditController.cs`：重写 `Search` 支持按 TokenId/ToolName/ActionName/Success/时间筛选

---

## Phase 12: Polish（集成验证与文档）

> **阶段目标**：端到端验证、回归测试、GitNexus 风险检查。

- [x] T067 新建 `Stardust.ServerTests/Mcp/McpTokenTests.cs`：测试 Token 生成/校验/恒定时间比较/过期判断
- [x] T068 新建 `Stardust.ServerTests/Mcp/McpTokenResourceTests.cs`：测试 `IsAuthorized`/`GetAuthorizedProjectIds`/`IsAll` 语义/`SyncByToken`
- [x] T069 新建 `Stardust.ServerTests/Mcp/McpServiceTests.cs`：测试 JSON-RPC 解析/方法路由/错误码（-32700/-32600/-32601/-32602/-32001/-32003）
- [x] T070 [P] 新建 `Stardust.ServerTests/Mcp/McpActionSchemaTests.cs`：测试 27 个 action 的 InputSchema 完整性和 RequiredResource 声明正确性
- [x] T071 执行 quickstart.md 场景 1-2（MCP 开关 + Token 鉴权）验证
- [x] T072 执行 quickstart.md 场景 3-4（LLM 上下文 + 搜索获取）验证
- [x] T073 执行 quickstart.md 场景 5-6（写操作 + 间接资源校验）验证
- [x] T074 执行 quickstart.md 场景 7-8（动作过滤 + 审计日志）验证
- [x] T075 执行 quickstart.md 场景 9（Web Token 管理）验证
- [x] T076 执行 quickstart.md 场景 10（扩展新动作）验证：临时新建 `GetSystemInfoAction` 重启后 `list_actions` 可见
- [x] T077 执行 quickstart.md 场景 11（客户端集成）：配置 Claude Desktop 或 Trae 连接并执行 LLM 工作流
- [x] T078 运行 GitNexus `detect_changes({scope:"compare",base_ref:"master"})` 验证变更范围，确认无意外影响现有功能

---

## Dependencies（User Story 完成顺序）

```
Phase 1: Setup (T001-T004)
    ↓
Phase 2: Foundational (T005-T015)
    ↓
Phase 3: US1 — MCP 协议端点 + 基础工具 (T016-T024)  [MVP]
    ↓
    ├─→ Phase 4: US2 — 搜索与获取资源 (T025-T033)
    ├─→ Phase 5: US3 — 节点管理 (T034-T037)
    ├─→ Phase 6: US4 — 应用管理 (T038-T044)
    ├─→ Phase 7: US5 — 配置中心 (T045-T046)
    ├─→ Phase 8: US6 — 远程发布 (T047-T056)
    ├─→ Phase 9: US7 — 网关管理 (T057-T058)
    └─→ Phase 10: US8 — 监控中心 (T059-T060)
         ↓
         Phase 11: US9 — Web Token 管理 (T061-T066)
              ↓
              Phase 12: Polish (T067-T078)
```

**关键依赖**：
- Phase 4-10 依赖 Phase 3（US1）完成，因为 action 调用需要 `HandleInvokeAction` 骨架
- Phase 11（Web Token 管理）依赖 Phase 3-10 完成（需要完整功能验证）
- Phase 12（Polish）依赖所有功能阶段完成

---

## Parallel Execution Examples（并行执行示例）

### 示例 1：Setup 阶段并行

T001、T002、T003 可并行（不同表的 Model.xml 定义，独立编辑）。

### 示例 2：Foundational 阶段并行

T005（Setting.cs）、T006（IMcpAction.cs）、T007（McpContext.cs）、T008（IResourceProvider.cs）可并行（不同文件，无依赖）。

T009、T010、T011 可并行（不同 Biz.cs 文件，无依赖）。

### 示例 3：资源 Provider 并行

T027-T032（6 个 ResourceProvider）可并行（不同文件，仅依赖 T008 接口定义）。

### 示例 4：Action 模块并行

Phase 5-10（US3-US8）的 action 实现可在各自模块内并行：
- T034-T037（4 个 node action）并行
- T038-T041（4 个查询类 app action）并行
- T045-T046（2 个 config action）并行
- T047-T050（4 个查询类 deploy action）并行
- T057-T058（2 个 gateway action）并行
- T059-T060（2 个 monitor action）并行

### 示例 5：测试并行

T067-T070（4 个测试文件）可并行（不同测试类，无依赖）。

---

## Implementation Strategy（实施策略）

### MVP 优先

**MVP 范围 = Phase 1 + Phase 2 + Phase 3（US1）**

完成 MVP 后即可：
- 启动 Stardust.Web，配置 `EnableMcp=true`
- 用 `curl` 或 MCP 客户端连接 `/mcp`
- 完成 `initialize` → `list_authorized_resources` → `list_actions` → `invoke_action(get_audit_log)` 工作流
- 验证 Token 鉴权和基础协议

### 增量交付

1. **MVP**（Phase 1-3）：协议端点 + Token 鉴权 + `get_audit_log`（最小可验证）
2. **查询能力**（Phase 4）：搜索 + 获取资源（LLM 可建立完整资源上下文）
3. **运维能力**（Phase 5-10）：27 个 action 逐模块实现（可按业务优先级调整顺序）
4. **管理界面**（Phase 11）：Web Token 管理（管理员可创建/禁用 Token）
5. **质量保障**（Phase 12）：测试 + 端到端验证 + 回归检查

### 风险缓解

- **xcode 生成冲突**：T004 后立即检查生成结果，Biz.cs 文件不会被覆盖
- **DeployService 命名空间**：T051/T052 中使用 `using WebDeployService = Stardust.Web.Services.DeployService;` 别名
- **间接资源校验**：T015 实现后单独测试 `AppPipelineRun`→`AppPipeline`→`ProjectId` 二级查找
- **pipeline_cancel**：T055 需修改现有 `PipelineService.cs`，按 AGENTS.md 要求运行 GitNexus `impact` 分析

---

## Task Count Summary

| Phase | Task Count | Description |
|---|---|---|
| Phase 1: Setup | 4 | 数据模型 + xcode 生成 |
| Phase 2: Foundational | 11 | 配置 + 接口 + 业务方法 + McpService 骨架 |
| Phase 3: US1 | 9 | MCP 协议端点 + 基础工具（MVP） |
| Phase 4: US2 | 9 | 搜索 + 获取资源 |
| Phase 5: US3 | 4 | 节点管理 action |
| Phase 6: US4 | 7 | 应用管理 action |
| Phase 7: US5 | 2 | 配置中心 action |
| Phase 8: US6 | 10 | 远程发布 action（含 PipelineService.Cancel） |
| Phase 9: US7 | 2 | 网关管理 action |
| Phase 10: US8 | 2 | 监控中心 action |
| Phase 11: US9 | 6 | Web Token 管理 |
| Phase 12: Polish | 12 | 测试 + 端到端验证 |
| **总计** | **78** | |

### 并行任务统计

- 标记 `[P]` 的任务：27 个（可并行执行）
- 主要并行机会：Setup 阶段（3 个表定义）、Foundational 阶段（4 个接口文件 + 3 个 Biz 文件）、资源 Provider（6 个）、Action 模块内（多个 action 类）

### 独立测试标准

| User Story | 独立测试 |
|---|---|
| US1 | `curl POST /mcp` 完成 initialize + 3 个基础工具调用 |
| US2 | search_resources + get_resource 工作流，未授权返回 -32003 |
| US3 | node_list_online + node_send_command 工作流 |
| US4 | app_list_online + app_restart 工作流 |
| US5 | config_get + config_set 工作流 |
| US6 | deploy_list + pipeline_trigger + pipeline_cancel 工作流 |
| US7 | gateway_list_routes + gateway_list_clusters |
| US8 | monitor_trace_search + monitor_alarm_list |
| US9 | Web 创建 Token + 勾选资源 + 禁用 Token |

---

## Format Validation

- ✅ 所有任务使用 `- [ ]` 复选框格式
- ✅ 所有任务有唯一 Task ID（T001-T078）
- ✅ User Story 阶段任务有 `[USx]` 标签
- ✅ 可并行任务标记 `[P]`
- ✅ 所有任务有明确文件路径
- ✅ 依赖关系清晰（Phase 顺序 + Dependencies 图）
