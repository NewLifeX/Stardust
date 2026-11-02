# MCP-4 平台 MCP 重新设计

> 版本：v1.3（实现审查后修订；2026-10-31 回源审查发现与用户决策） | 日期：2026-10-31
> 状态：**P0 框架改造完成，P1 只读工具落地，P2 写工具接近完成**。32 个工具已注册并补全 InputSchema；Services 注入已修复（写工具不再 NRE）；权限交集三层已接线（ProjectUser 成员 + Scope + McpTokenResource 白名单）；9 个写工具返回 rollback_hint、5 个列表工具接 pagination；审计写路径已脱敏，BeforeValue/AfterValue 审计快照已接入。遗留：真端口 6680 端到端集成测试。

---

## 1. 背景与目标

### 1.1 星尘的功能资产（MCP 的取材范围）

| 功能域 | 核心能力 | 主要数据/服务 |
|---|---|---|
| 注册中心 | 应用/服务注册发现、消费依赖拓扑、在线实例 | `RegistryService`，App、Service、AppService、AppConsume、AppOnline、AppClientLog |
| 配置中心 | 配置项、版本、发布、回滚 | `ConfigService`，AppConfig、ConfigData、ConfigHistory |
| 发布部署 | 部署集、版本、节点分发、流水线（编译→上传→部署） | `DeployService`，AppDeploy、AppDeployVersion、AppDeployNode、AppDeployHistory、AppPipeline/Run/Step |
| 节点管理 | 节点、在线状态、远程命令通道（bash/cmd）、进程管理 | `NodeService`/`StarFactory`，Node、NodeOnline、NodeCommand、NodeStat |
| 监控告警 | 错误率/性能统计、调用链采样、告警 | `MonitorService`/`AlarmService`，AppMinuteStat/AppDayStat、SampleData/TraceData、AlarmRecord/History |
| 网关 | 动态路由、集群、证书 | `GatewayService`，GatewayRoute/Cluster/Node/Cert |
| 平台 | 项目（星系）、项目成员、审计 | GalaxyProject、ProjectUser、Membership 用户体系 |

### 1.2 目标用户旅程（工具目录的推导依据）

AI Agent（Claude / Cursor / Trae / Codex 等经 MCP 接入）作为**值班运维**：

1. **发布验证**：「刚发的版本成没成？」→ 当前生效版本、各节点版本一致性、流水线步骤、部署历史，一次答完。
2. **告警定位**：「错误率涨了，帮我找原因」→ 应用健康 → 慢/错调用链 → 关联节点 → 给出证据链。
3. **配置变更**：「把某配置改了并发布」→ 读现值 → 改草稿 → 发布（可回滚）。
4. **定点操作**：「灰度发布到两台节点」「重启 3 号节点的应用」→ 版本/节点清单 → 受控下发。
5. **巡检问答**：「示例应用现在健康吗？」「这台节点上跑着什么？」

> 原第 1 条「值班开屏（跨项目值班总览）」已按 Q3 决策移除：平台不做跨项目维度，令牌严格单项目。

**工具按旅程组织，而不是按数据库表组织**——让 AI 用 1~3 次调用回答一个业务问题，而不是把 CRUD 镜像成工具让 AI 自己拼装。

---

## 2. 旧实现核实结论（v1.1 新增）

### 2.1 旧实现形态（已读代码核实）

- **架构**：`McpMiddleware` 在 Cube 前短路 `/mcp` → `McpService`（单例；反射注册 `IMcpAction`×27、`IResourceProvider`×6、间接资源解析器）→ Action 构造注入 `ConfigService`/`DeployService`/`StarFactory` 复用业务层。**`Stardust.Web` 与 `Stardust.WebApi` 各一份完整拷贝**（WebApi 侧文件 namespace 仍是 `Stardust.Web.*`）。
- **传输**：`POST /mcp` JSON-RPC 2.0，无状态 `application/json` 响应；协议版本协商 2024-11-05 ~ 2026-07-28；notifications 回 202；GET/DELETE 405；无 SSE、无 `Mcp-Session-Id`、无 stdio。官方 C# SDK 2.0 `StreamableHttpClientSessionTransport` 实测可连（`SdkMcpClientTests`，握手有 SDK 侧瞬时竞争需重试）。
- **工具面**：`tools/list` 永远只返回 5 个协议原语工具（list_authorized_resources / search_resources / get_resource / list_actions / invoke_action），27 个业务 Action 藏在 `invoke_action` 后，`params` 为无 schema 的自由 JSON。
- **令牌/授权**：`McpToken`（`sdmcp_`+32 位 Base62、恒定时间比较、Enable/ExpireTime/CallCount）→ `McpTokenResource`（Project/Node/App 直接授权 + IsAll 全量通配；Deploy/Pipeline/Service 间接反查 ProjectId）。**无用户绑定、无项目绑定、无操作级别**。
- **审计**：`McpAudit`（TokenName 快照、IP/UA、Arguments 脱敏 password/secret/token/authorization 并截断 2000、Duration、TraceId），`Task.Run` 异步写，**fail-open**（注释明示「审计失败绝不能影响主响应」）。
- **开关**：`StarServerSetting.EnableMcp`（默认 false）+ `McpActionSet` 模块白名单。
- **前端**：旧布局 CRUD 管理页 `Platform/McpToken`（585 行）、`McpTokenResource`；新布局治理页 `McpTab.vue`（173 行）只读展示三张表。
- **测试**：`ServerTests/Mcp`（5 个测试类）+ `McpClientTests`（Raw JSON-RPC 客户端 + 官方 SDK 客户端 + TestServer fixture），基建完备。

### 2.2 旧设计的问题（核实后确认，重实现要解决的）

| # | 问题 | 事实依据 | 新设计对策 |
|---|---|---|---|
| P1 | **二段式工具发现**：业务能力不进 `tools/list`，AI 看不到工具与参数 schema，需运行时先 `list_actions` 再 `invoke_action`，`params` 无结构化校验 | `McpService.HandleToolsList` 固定 5 工具；`invoke_action.inputSchema.params` 为 `additionalProperties:true` | 工具直接进 `tools/list`（§5），每个工具带完整 JSON Schema，按 scope 裁剪 |
| P2 | **授权与用户体系脱节、读写不分级**：授权节点=全权命令通道，授权项目=可 install/stop；IsAll 全量通配 | `McpToken` 无 UserId/ProjectId/Scopes 字段；`IsAuthorized` 只查类型+Id/IsAll | 令牌绑定用户+项目+scope，权限=交集；风险三档（§4） |
| P3 | **审计 fail-open**：写操作审计丢失无感知，违背 SVR-8「审计重于防御」 | `McpAudit.WriteAsync` fire-and-forget；`HandleToolsCall` finally 块吞异常 | 写操作审计 fail-closed；读操作仍 best-effort（§7.1） |
| P4 | **敏感值明文外泄**：`config_get` 把合并配置全量明文返回，连接串/密钥直进 LLM 上下文与客户端日志 | `ConfigGetAction.InvokeAsync` 无脱敏 | `StarServerSetting.SensitiveKeywords` 关键词匹配即自动脱敏返回 `***`（§5.5/§6.6） |
| P5 | **双宿主代码拷贝**：Web 与 WebApi 各一套 `Mcp/`，修一漏一 | 文件清单两侧对称；WebApi 侧 namespace 为 `Stardust.Web.*` | 收敛为共享类库，两宿主薄挂载（§3.2） |
| P6 | **工具按表/操作镜像，缺旅程聚合**：`node_list_online`/`deploy_list` 是 CRUD 视角；无健康总览、发布验证、告警证据链 | Action 清单 27 个均为单实体操作 | 旅程工具优先（§5），旧单实体操作保留为兜底 |
| P7 | **写操作无后果/回滚说明**：`deploy_install` 只回 `status:"submitted"`；`app_stop` 无确认机制 | `DeployControlActions`/`AppControlActions` 返回体 | 写工具统一返回影响清单 + `rollback_hint`；L2 需 `confirm`（§4.2/§6） |
| P8 | **无 stdio 桥**：仅 stdio 能力的客户端接不进 | `MCP-3` 明言「无需 stdio 桥接」 | 增加极薄 stdio 桥（§3.3） |
| P9 | **无分页与性能预算**：`search_resources` 各类型固定 PageSize 50 无翻页；聚合能力缺失 | `HandleSearchResources` | 分页强制 + 性能预算（§6.3/§6.8） |

### 2.3 旧实现保留的资产（重实现直接继承）

1. **扩展骨架**：`IMcpAction` 反射注册 + 构造注入 + `McpActionBase` 参数提取辅助——升级为 `IMcpTool`（增加风险级别、scope 声明），机制不变。
2. **令牌工程细节**：`sdmcp_` 前缀 + Base62、恒定时间比较、一次性展示、Enable/ExpireTime、调用统计（LastTime/LastIP/CallCount）。
3. **间接资源解析器**：Deploy/Pipeline/Run/Service → ProjectId 反查（`RegisterIndirectResolvers`），项目校验继续复用。
4. **中间件短路挂载**：Cube 前短路 `/mcp`，避开 Web 中间件（device-id Cookie 等）。
5. **无状态 Streamable HTTP 传输**：实测与官方 SDK 2.0 兼容，是省掉 SSE/会话复杂度的正确取舍，保留（§3.3）。
6. **协议版本协商矩阵**（2024-11-05 ~ 2026-07-28）、notifications 202、ping。
7. **审计字段设计与脱敏正则**（扩展脱敏键集合）。
8. **配置开关**：`EnableMcp` 总开关 + `McpActionSet` 模块白名单（语义升级为域白名单）。
9. **测试基建三件套**：`ServerTests/Mcp` 单测、`McpClientTests` 的 Raw/SDK 双客户端、`McpTestServerFixture`。

---

## 3. 设计原则与总体架构

### 3.1 四条设计原则（不可妥协）

1. **旅程优先**：工具回答一个运维问题或完成一个运维动作。读工具一次给足上下文，写工具一次只做一件事并返回后果与回滚方式。禁止「一表一工具」的 CRUD 镜像（兜底性单实体查询除外）。
2. **MCP 是薄壳，Service 是唯一业务入口**：工具复用现有 Service（DeployService、ConfigService、NodeService、AlarmService、RegistryService…），业务语义与 Web 前端完全一致；MCP 层只做参数校验、权限检查、结果裁剪与脱敏、审计。
3. **权限继承，交集生效**：令牌绑定平台用户与项目，实际权限 = 用户在该项目的权限 ∩ 令牌 scope ∩（可选）资源白名单，再按操作风险分级。
4. **审计与人机同屏**：写操作审计 fail-closed；MCP 写操作进入前端「变更」时间线（与 `/changes` 同源）。对齐 SVR-8：客户端可被攻破、平台是安全边界、审计重于防御。

### 3.2 宿主与代码组织（消灭双拷贝）

新建共享类库 **`Stardust.Mcp`**（依赖 `Stardust.Server` + `Stardust.Data`），承载全部 MCP 实现：

```
Stardust.Mcp/                  ← 唯一实现（消灭 Web/WebApi 双拷贝）
 ├─ McpEndpoint/               协议端点：JSON-RPC 解析、版本协商、无状态传输
 ├─ IMcpTool.cs / McpToolBase  工具契约（Name/Schema/RiskLevel/Scope/Handler）
 ├─ Tools/{Now,Registry,Trace,Alarm,Config,Deploy,Nodes,AppCtl,Platform}/  按域分目录
 ├─ Security/                  令牌校验、scope 解析、权限交集、脱敏
 └─ Audit/                     审计写入（读 best-effort / 写 fail-closed）

Stardust.Web  ─┐ Startup: services.AddStardustMcp() + app.UseStardustMcp()
Stardust.WebApi ┘（两个宿主只剩 2 行挂载代码，各自短路 /mcp）
```

端点继续由中间件在 Cube 前短路 `/mcp`（继承旧模式），两宿主行为一致、配置共用 `StarServerSetting`。

### 3.3 传输（吸收旧实现已验证的取舍）

- **远程**：保持**无状态 Streamable HTTP**——`POST /mcp`，响应 `application/json`；GET/DELETE 405；不实现 SSE 与 `Mcp-Session-Id`（规范允许，官方 SDK 2.0 实测兼容，见 §2.1）。协议版本协商矩阵、notifications 202、ping 全部继承。
- **本地**：新增极薄 **stdio 桥**（dotnet tool `stardust-mcp`），读环境变量 `STARDUST_URL` / `STARDUST_MCP_TOKEN`，把 stdio JSON-RPC 转发到远端 `/mcp`。桥内零业务逻辑，核心实现唯一。
- **能力裁剪**：`tools/list` 按令牌 scope 过滤（旧实现 5 个原语永远全返，因业务动作在后面无裁剪意义；新工具直出后必须裁剪，客户端看不到无权工具）。

---

## 4. 身份认证与授权

### 4.1 令牌模型（`McpToken` 表演进）

```
McpToken（现有表 + 增量字段）
 ├─ 绑定用户 UserId（Membership）    ← 新增：权限来源之一，令牌持有人可追责
 ├─ 绑定项目 ProjectId               ← 新增：必填，严格单项目（Q3：不做跨项目令牌）
 ├─ Scopes: String                   ← 新增：逗号分隔 域:级别（如 apps:read,config:write）
 ├─ Name / Token(sdmcp_) / Enable / ExpireTime / 调用统计    ← 继承不动
 └─ 速率限制（可选，P3）
```

- **实际权限 = 用户在该项目的权限 ∩ 令牌 scope ∩（可选）资源白名单**，交集不满足时拒绝并点名缺失项（「缺 scope `deploy:write`，找管理员在令牌表单勾选」）。
- 默认新令牌只发只读 scope；写、高危 scope 需显式勾选。**实现修订（2026-10-31）**：默认 scope 串不含 `deploy:write`，仅含各域 `:read`；写权限必须用户手动勾选，安全默认收紧。
- **存量令牌不迁移（Q2 已决）**：UserId/ProjectId/Scopes 留空，等效零 scope，新端点下仅 `stardust_token_whoami` 可用（whoami 不要求 scope），其余全部拒绝；需要时管理员重建令牌，旧令牌可禁用回收。
- **`McpTokenResource` 从授权主数据降级为可选的「资源白名单收窄」**：在 scope 允许的域内限定可用节点/应用（最小授权仍有价值）；`IsAll` 通配在新建时禁止——`McpTokenResourceController.OnInsert` 服务端强制 `IsAll=false`（不依赖前端），存量 IsAll 行只读保留。

### 4.2 操作风险三档（写进工具元数据，端点层强制执行）

| 级别 | 定义 | 代表操作 | 控制措施 |
|---|---|---|---|
| **L0 只读** | 无副作用 | 一切查询 | scope 域 `:read` + 审计（best-effort） |
| **L1 可逆写** | 有副作用但平台内可回滚 | 切换版本、发布/回滚配置、触发流水线、启动/重启、告警处置 | scope 域 `:write` + 审计（fail-closed，含 before/after）+ 返回 `rollback_hint` |
| **L2 高危** | 不可逆 / 影响运行时 / 越出平台 | 停止/卸载节点应用、执行任意命令、删配置、批量操作 | scope 域 `:admin` + 审计（fail-closed）+ **参数必须显式 `confirm:true`**（描述写明后果，如「stop 会将该节点 Enable 置 false」）+ 复用 SVR-8 危险命令告警规则 |
| （企业模式，P3） | — | — | 控制台**确认队列**：L2 调用挂起，人在 Web 界面批准后执行（人机双签） |

### 4.3 项目隔离（硬约束）

- 会话绑定 ProjectId，所有查询走带项目过滤的路径；间接资源校验复用旧解析器（Deploy/Pipeline/Run/Service → ProjectId）。
- 已知缺陷必须在 MCP 层修复、**不得复制**：`AppDeployHistory`、`AppPipelineRun`、`ConfigHistory` 的 Search 不读 projectId（跨项目泄漏，前端停点 20 已核实）。MCP 工具内部按项目内 id 集合过滤后合并，策略与前端 `changes.logic.ts` 一致。

---

## 5. 工具目录（v1：32 个，直出 `tools/list`）

命名 `stardust_<域>_<动作>`；「来源」列标注旧 action 映射（迁移对照 + 业务逻辑复用），标「新造」者为旅程聚合工具。**文案语言（Q4 已决）**：工具 description 与参数 schema 描述用英文（利于模型选工具），返回 summary 与错误提示用中文（利于运维阅读）。

### 5.1 变更时间线 now（1）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_now_changes` | L0 | 项目内跨应用变更时间线（D 发布 / C 配置 / P 流水线），时间倒序分页 | 新造（与 `/changes` 同口径 + §4.3 过滤） |

（**值班总览 `stardust_now_overview` 已按 Q3 决策移除**：不做跨项目维度；单项目内的需关注事项由 `alarm_list` / `app_health` / `node_list` 组合回答。）

### 5.2 应用与注册 registry（5）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_app_list` | L0 | 应用清单（关键字/分类），含在线实例数、版本、启停 | 新造（聚合 `app_list_online` 逻辑） |
| `stardust_app_health` | L0 | **单应用健康体检**：实例、错误率（分钟/日）、最近发布与流水线、活跃告警、最近错误日志 | 新造（旅程 3/6 入口） |
| `stardust_app_topology` | L0 | 提供的服务与消费的依赖（上下游拓扑） | 复用 `app_resolve_service` / `app_search_service` 数据面 |
| `stardust_app_logs` | L0 | 应用日志（级别/时间窗/关键字，分页，服务端按 appId 过滤） | 新造（AppClientLog） |
| `stardust_service_lookup` | L0 | 按服务名找提供者与消费者 | 复用 `app_search_service` |

### 5.3 调用链 trace（2）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_trace_search` | L0 | 慢/错调用列表（应用、时间窗、最小耗时、仅错误，分页） | 复用 `monitor_trace_search` |
| `stardust_trace_detail` | L0 | 单链路瀑布：span 树、耗时、异常、关联应用/节点 | 新造（TraceData 下钻） |

### 5.4 告警 alarm（3）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_alarm_list` | L0 | 告警列表（状态/级别/时间窗，分页） | 复用 `monitor_alarm_list` |
| `stardust_alarm_detail` | L0 | 告警详情 + 证据链（自动附关联应用健康与错误调用链入口） | 新造（组合 5.2/5.3） |
| `stardust_alarm_resolve` | L1 | 处置告警（处理意见必填） | 新造（AlarmService） |

### 5.5 配置 config（4）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_config_list` | L0 | 配置项清单（版本、启用状态；**不回值**） | 改造（AppConfig 列表） |
| `stardust_config_get` | L0 | 读配置值。**敏感项自动脱敏返回 `***`**——敏感识别由 `StarServerSetting.SensitiveKeywords` 驱动（见 §6.6），所有令牌一律不明文返回敏感值 | 改造 `config_get`（去明文直出） |
| `stardust_config_set` | L1 | 写配置草稿（key、value、备注），返回发布提示与回滚提示 | 复用 `config_set`（SetConfigs） |
| ~~`stardust_config_publish`~~ | ~~L1~~ | **已移除（2026-10-31 用户决策）**：发布配置必须人手动在控制台操作，AI 只能写草稿不能发布。AI 调 `config_set` 后提示「请到控制台手动发布」 | — |
| `stardust_config_rollback` | L1 | 回滚到指定版本 | 新造（ConfigHistory.Rollback） |

### 5.6 部署 deploy（8）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_deploy_status` | L0 | **发布验证一步答**：部署集与当前版本、各节点版本一致性（落后点名）、进程存活、最近下发记录 | 新造（旅程 2 核心） |
| `stardust_deploy_versions` | L0 | 版本清单（版本号、启用、大小、提交信息、可用性标记） | 复用 `deploy_list_versions` |
| `stardust_deploy_use_version` | L1 | 切换生效版本。**显式返回 AutoPublish 语义**：未开自动发布时提示「仅切换版本号，未下发节点，需调 deploy_node_operate 或 deploy_release」 | 新造（UseVersion） |
| `stardust_deploy_node_operate` | L1/L2 | 对部署节点 install/start/stop/restart/uninstall；stop/uninstall 为 L2 | 复用 `deploy_install`（Control 通道，act 扩展） |
| `stardust_deploy_release` | L1 | **组合发布**（旅程 5）：应用+目标版本+节点列表，先 `dryRun` 返回计划（哪些节点将变化），确认后逐节点下发，返回逐节点结果 | 新造（编排 UseVersion+Control） |
| `stardust_pipeline_trigger` | L1 | 触发流水线；**区分「已创建 run」与「未创建+原因」**（禁用、分支不匹配、去重窗口等，原因透传不吞） | 复用 `pipeline_trigger`，吸收 `deploy_compile`（编译是流水线的一种触发形态） |
| `stardust_pipeline_run_detail` | L0 | 运行详情（步骤、节点回包、commit；全 Skipped 异常识别——后端把全 Skipped 标 Success 的缺陷，MCP 兜底标注「无节点实际部署」） | 复用 `pipeline_get_run` |
| `stardust_deploy_history` | L0 | 部署历史（时间窗、分页；写明「下发记录≠执行结果」） | 复用 `deploy_list_history` |

（旧 `pipeline_cancel` 并入 `stardust_pipeline_trigger` 的取消语义或独立小工具，实现期定。）

### 5.7 节点 nodes（4）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_node_list` | L0 | 节点清单（关键字、在线状态、版本、分类） | 复用 `node_list_online` / `node_search` |
| `stardust_node_detail` | L0 | 节点详情：在线、版本、进程数、资源水位、最近命令历史 | 新造（Node+NodeOnline+NodeStat+NodeCommand） |
| `stardust_node_apps` | L0 | 该节点上运行的应用实例 | 新造（AppOnline 按 nodeId） |
| `stardust_node_command` | L2 | 在节点执行命令（白名单只读命令如 df/ps 可配置降为 L1）；走 StarFactory 命令通道，Operator 记 MCP 令牌身份，命中 SVR-8 危险规则自动告警 | 复用 `node_send_command` / `node_upgrade` |

### 5.8 应用控制 appctl（2）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_app_command` | L2 | 向应用下发命令（服务实例级） | 复用 `app_send_command` |
| `stardust_app_process` | L1/L2 | 应用进程 start/restart/stop（stop 为 L2） | 复用 `app_start` / `app_restart` / `app_stop` |

### 5.9 平台与自检 platform（3）

| 工具 | 级别 | 说明 | 来源 |
|---|---|---|---|
| `stardust_token_whoami` | L0 | 自检：身份、项目、scope、风险档位——AI 接入后第一跳。**无 scope 要求，任何有效令牌可调用**（存量零 scope 旧令牌唯一可用的工具） | 改造 `list_authorized_resources`（语义并入） |
| `stardust_project_list` | L0 | 可见项目清单 | 改造（GalaxyProject 按用户过滤） |
| `stardust_audit_list` | L0 | 查 MCP 审计（令牌/时间窗/工具过滤）——「这个令牌最近干了什么」 | 复用 `get_audit_log`（放开过滤维度） |

**v1 不做**（留 v2）：网关域工具（旧 `gateway_list_routes/clusters` 移入 v2）、Redis/MySql、DNS/防火墙、`search_resources`/`get_resource` 泛型资源原语（旅程工具已覆盖其场景；长尾需求实测后再评估）。

---

## 6. 工具接口规范（实现硬约束）

1. **描述写给 LLM**：description 说明「何时用 / 何时不用 / 与相邻工具的边界」（如 `deploy_status` vs `app_health`），参数用 enum 不用自由文本。
2. **返回统一信封**：
   ```json
   {
     "summary": "MyApp：3/5 实例在线，错误率 2.1%（↑），1 台节点版本落后",
     "data": { },
     "next_hints": ["stardust_deploy_status(app=MyApp) 查看版本一致性", "stardust_trace_search(app=MyApp, errorOnly=true) 定位错误"],
     "pagination": { "page": 1, "pageSize": 20, "total": 137 }
   }
   ```
3. **分页与时间**：列表强制分页（默认 20、上限 100）；时间入参 ISO8601 或相对分钟（`-60m`），返回统一 ISO8601。
4. **写工具必返回三件**：执行结果、受影响对象清单（逐节点/逐项）、`rollback_hint`（如「回滚配置调 stardust_config_rollback(version=v12)」）。
5. **语义透明**：fire-and-forget 操作（`Control`、流水线下发）返回写明「命令已下发，结果看 X 工具」；全 Skipped run 标注；`UseVersion` 未开 AutoPublish 时写明只切版本号。
6. **脱敏**：Token/Secret/密码/连接串出口统一打码。**敏感 key 识别方案（2026-10-31 用户决策）**：在 `StarServerSetting`（文件 `Stardust.Server/Setting.cs`，前端治理页「平台设置」tab 可见可改）加 `SensitiveKeywords` 字段，逗号分隔，默认 12 个：`db,database,redis,mysql,mssql,connectionstring,password,passwd,secret,token,apikey,access_key`。配置项 key 名包含任意关键词（大小写不敏感）→ 值返回 `***`。该字段为通用敏感配置，配置中心、MCP、审计等所有敏感值出口共用，不专属 MCP。改完即生效不用重启。审计写路径另用 8 个关键词正则脱敏。
7. **错误设计**：拒绝返回 `code + 人话 + 建议动作`（如「版本 v2.3 未启用：先调 stardust_deploy_versions，或在控制台启用」）；权限不足点名缺失 scope。错误码继承旧表（-32602 参数、-32001 未认证、-32003 越权、-32002 超时…）。
8. **性能预算**：单工具 p95 < 2s，聚合读（app_health / deploy_status）p95 < 5s；超预算改后端聚合。

---

## 7. 审计与人机协同

### 7.1 审计写入策略（修正旧 P3）

- **读操作**：best-effort 异步写（继承旧行为，失败仅告警）。
- **写操作（L1/L2）**：**fail-closed**——审计记录持久化失败即该次调用失败回滚，绝不允许「操作成功但无审计」。
- 字段沿用 `McpAudit`，增补：RiskLevel、ProjectId、ScopesUsed（快照）、BeforeValue、AfterValue。**实现状态（2026-10-31）**：5 个新字段已加（`MCP审计日志.cs`，XCode 自动迁移）；`McpAuditWriter.Begin` 已接入 RiskLevel/ProjectId/ScopesUsed 赋值；BeforeValue/AfterValue 待工具在调用前后快照（后续补）。

### 7.2 与前端同屏

MCP 的 L1/L2 写操作产生的事件进入前端「变更」时间线（`/changes` 同源数据），行上标注操作者为 MCP 令牌名（`McpToken.Name` 快照）。治理页 `McpTab` 增补 scope/风险档位列；管理页 `Platform/McpToken` 改造表单（用户绑定、项目绑定、scope 勾选、资源白名单可选）。

### 7.3 口径共享（与前端组的协同建议）

「变更时间线」前端现为客户端聚合（停点 20 结论），MCP 若另做一份会口径漂移。建议 P1 期把该口径提升为 WebApi 聚合接口（`ChangeFeedService`），前端与 MCP 共用；前端迁移可后排期，但新接口从一开始就是唯一口径源。（值班总览已按 Q3 移除，`OverviewService` 不再需要。）

---

## 8. Resources 与 Prompts（P2 起）

- **Resources**（`stardust://` URI）：`apps`（应用清单快照）、`nodes`（节点清单）、`topology`（服务拓扑）——供客户端预取大上下文。
- **Prompts**：固化运维 SOP——「告警定位五步法」「发布验证清单」「配置变更与回滚演练」。

---

## 9. 分期实施

| 阶段 | 范围 | 验收要点 | 实现状态 |
|---|---|---|---|
| **P0 框架改造** | `Stardust.Mcp` 类库 + 双宿主挂载；`IMcpTool` 注册体系；`McpToken` 加 UserId/ProjectId/Scopes；审计双策略；scope 拦截与 tools/list 裁剪；种子工具 whoami；旧双拷贝删除 | `dotnet build -c Release`；测试全绿；Raw/SDK 双客户端跑通 | ✅ 完成（2026-10-31）：类库 + 薄挂载、32 工具反射注册、Services 注入修复、权限交集三层接线、审计写路径脱敏、InputSchema 补全 |
| **P1 只读值班** | §5.1–5.4、config_list/get、deploy_status/versions/history、pipeline_run_detail、node_list/detail/apps、platform 剩余 2 个（约 17 个 L0） | 权限矩阵测试全绿；旅程端到端；审计 100% 覆盖 | ✅ 基本完成：17 个 L0 工具已注册并补全 schema；TraceDetail 项目隔离已修；遗留真端口端到端验证 |
| **P2 可逆写** | config_set/rollback、use_version、deploy_release、node_operate(install/start/restart)、pipeline_trigger、alarm_resolve、app_process(start/restart) | 每个写工具有 rollback_hint 与审计断言；dryRun 计划正确；`/changes` 可见 MCP 操作 | 🔄 接近完成：Services 注入已修；config_publish 已移除（用户决策）；9 个写工具 rollback_hint 已填、5 个列表工具 pagination 已接；风险粒度已修正。遗留真端口端到端验证 |
| **P3 高危与企业模式** | node_command、app_command、stop/uninstall 类 L2；confirm 强制；SVR-8 危险规则联动；控制台确认队列；限流 | confirm 缺失即拒绝；确认队列端到端；危险命令告警联动 | ⏳ 未开始 |
| **P4 生态** | stdio 桥（dotnet tool）、Resources/Prompts、网关域工具 | Claude Desktop/Cursor 实测接入 | ⏳ 未开始 |

每期完成后做一次「LLM 实测」：用真实 MCP 客户端跑六条旅程，记录平均调用次数（目标：单旅程 ≤ 3 次工具调用）。

---

## 10. 迁移对照（已核实，替换 v1.0 的推断）

### 10.1 数据表处置

| 表/类型 | 处置 | 说明 |
|---|---|---|
| `McpToken` | **保留 + 增量字段** | 加 UserId、ProjectId、Scopes；**存量令牌不迁移（Q2 已决）**：新字段留空等效零 scope，新端点下仅 whoami 可用，其余拒绝；需要时管理员重建令牌 |
| `McpTokenResource` | **降级为可选资源白名单** | 不再是授权主数据；新建禁用 IsAll；存量 IsAll 行迁移时转为「scope 内不收窄」 |
| `McpAudit` | **保留 + 增量字段** | 加 RiskLevel、ProjectId、ScopesUsed、BeforeValue、AfterValue |
| `McpResourceType` / `McpModuleType` | **收缩** | 保留枚举供资源白名单/域映射复用；协议面不再暴露资源原语 |

### 10.2 代码处置

- 删除 `Stardust.Web/Mcp/`、`Stardust.Web/McpMiddleware.cs`、`Stardust.Web/Services/McpService.cs` 及 WebApi 侧对应拷贝；Action 的业务编排逻辑按 §5「来源」列迁入 `Stardust.Mcp/Tools/`。
- `StarServerSetting.EnableMcp` / `McpActionSet` 语义保留（McpActionSet 改为按域配置）；前端 `McpTab` 与 `Platform/McpToken` 管理页按 §7.2 改造。
- 测试迁移：`ServerTests/Mcp` 五个测试类随实现迁移；`McpClientTests` 双客户端保留，断言从 5 工具改为新目录。

### 10.3 旧工具 → 新工具映射（给 LLM 用户的迁移说明）

`list_authorized_resources` → `stardust_token_whoami`；`search_resources`/`get_resource` → 各域 list/detail 工具；`invoke_action(node_send_command)` → `stardust_node_command`；`deploy_install` → `stardust_deploy_node_operate(act=install)`；`app_stop/app_restart/app_start` → `stardust_app_process`；`config_get/config_set` → 同名新工具（get 脱敏）；其余 action 一一对应 §5 各行「来源」列。

---

## 11. 测试与验证门禁

1. **Schema 单测**：每工具参数校验矩阵（类型/enum/必填/分页边界）——继承 `McpActionSchemaTests` 思路。
2. **契约测试**：工具 → Service 调用与结果映射（mock Service），含脱敏断言（config_get 无 secret scope 时敏感值为掩码）。
3. **权限矩阵测试**：scope × 工具 × 项目 × 资源白名单四维，拒绝路径点名缺失项。
4. **审计断言**：读 best-effort / 写 fail-closed（写审计失败→操作失败）；字段齐全；脱敏生效。
5. **端到端 MCP 会话测试**：五条旅程各一条脚本（initialize → tools/list → 调用链），跑在 WebApi TestServer 上（复用 `McpTestServerFixture`），Raw + 官方 SDK 双客户端。
6. **安全用例**：跨项目越权（§4.3 缺陷接口直查必须被过滤）、confirm 缺失、敏感值脱敏、令牌过期/禁用/用户被移除后令牌失效。
7. 门禁：`dotnet build -c Release` 零新增警告 + 上述测试全绿；前端管理页改动跑前端门禁。

---

## 12. 决策记录

| # | 决策 | 理由 |
|---|---|---|
| D1 | 工具按旅程组织直出 tools/list，废弃二段式 invoke_action | 修复 P1/P6；AI 1~3 次调用答完业务问题 |
| D2 | 共享类库 `Stardust.Mcp`，双宿主薄挂载 | 修复 P5 双拷贝 |
| D3 | 无状态 Streamable HTTP 保留（不引入 SSE/会话） | 旧实现已实测与官方 SDK 2.0 兼容，避免过度工程 |
| D4 | 权限 = 用户项目权限 ∩ scope ∩ 可选资源白名单；IsAll 禁止新建 | 修复 P2；保留最小授权能力 |
| D5 | 风险三档 + L2 显式 confirm + 写审计 fail-closed | 修复 P3/P7；对齐 SVR-8 |
| D6 | ~~配置值分级脱敏 + config:secret scope~~（已被 D16 取代：改为 `StarServerSetting.SensitiveKeywords` 自动脱敏，不使用 config:secret） | 修复 P4 |
| D7 | 写操作进 /changes 时间线，人机同屏 | AI 操作可追责 |
| D8 | stdio 桥只转发不做业务 | 修复 P8；核心实现唯一 |
| D9 | 变更时间线口径后端化，前端与 MCP 共用 | 防口径漂移（值班总览已按 Q3 移除，不再需要 OverviewService） |
| D10 | v1 砍网关/Redis/MySql/DNS 域与泛型资源原语 | 先做深值班旅程；长尾需求实测后再评估 |
| D11 | 旧 5 工具 + 27 action 在 P0 直接下线（不做兼容期） | 功能未正式上线（MCP-3 注明），无兼容包袱 |
| D12 | 文案语言：英文 description + 中文 summary/错误提示 | Q4 已决；发现层利于模型选工具，结论与报错利于运维阅读 |
| D13 | 存量令牌不迁移，零 scope 旧令牌仅 whoami 可用 | Q2 已决；无需迁移脚本，安全默认收紧 |
| D14 | 令牌严格单项目，去掉值班总览 | Q3 已决；平台不做跨项目维度 |
| D15 | config_publish 工具移除（`ConfigTools.cs` 中无该工具类），发布必须人手动来 | 2026-10-31 用户决策：AI 只能写草稿（config_set），发布操作必须人在控制台手动完成 |
| D16 | 敏感配置识别改为 `StarServerSetting.SensitiveKeywords` 关键词列表，不改 ConfigData 表 | 2026-10-31 用户决策：字段为通用敏感配置（不专属 MCP），key 名匹配即自动脱敏返回 ***；不加表字段，不做加密存储 |
| D17 | 默认新令牌 scope 不含 deploy:write（`McpTokenController.OnInsert`） | 2026-10-31 用户决策：安全默认收紧，写权限必须手动勾选 |
| D18 | IsAll 新建服务端强制 false（`McpTokenResourceController.OnInsert`） | 2026-10-31 用户决策：前端挡不够，服务端创建时硬编码 IsAll=false |

---

## 13. 用户决策记录（2026-10-30 已全部决议）

| # | 问题 | 决议（用户原话） | 设计落点 |
|---|---|---|---|
| Q1 | 开工顺序（前端停点 11 未完 vs MCP P0） | 「并行也不冲突」——两线同时推进 | §9 P0 |
| Q2 | 存量 McpToken 迁移 | 「不需要迁移，旧的不用管」——无迁移脚本；新字段留空=零 scope，旧令牌新端点下仅 whoami 可用 | §4.1、§10.1 |
| Q3 | 项目绑定粒度 | 「去掉值班总览，不需要跨项目维度的」——删除 now_overview 工具与跨项目维度；令牌严格单项目必填 | §1.2、§4.1、§5.1 |
| Q4 | 工具文案语言 | 「c」——英文 description + 中文 summary/错误提示 | §5、§6.7 |

---

## 14. 实现审查记录（2026-10-31）

实现完成后做了一轮设计对照审查（报告 `.temp/mcp-design-review.md`），发现 3 个严重偏差并已修复：

| 偏差 | 现象 | 修复 |
|---|---|---|
| D-1（P0） | `McpContext.Services` 从未注入，所有写工具运行时必 NRE | `McpEngine.HandleAsync` 加 services 参数透传，`McpMiddleware` 传入 `RequestServices` |
| D-2（P1） | 权限交集只落地 1/3：UserId 未校验项目成员关系，McpTokenResource 白名单运行时未接线 | `McpGuard.NeedProject` 加 ProjectUser 成员校验；新增 `CheckResource` 方法接 McpTokenResource 白名单 |
| D-3（P1） | 32 个工具 inputSchema 全为空对象 `{type:"object"}` | 9 个 Tools 文件全部补上完整 JSON Schema（properties/required/enum） |

**其他已修复**：写路径审计脱敏（8 关键词正则）、审计实体加 5 个新字段（RiskLevel/ProjectId/ScopesUsed/BeforeValue/AfterValue）并接入前 3 个字段赋值、TraceDetail 跨项目泄漏修复、风险粒度错配修正（install/start/restart 不再误标 L2）、9 个写工具返回 rollback_hint（app_process 复用 app_command）、5 个列表工具用 Paged() 包装分页、`StarServerSetting.SensitiveKeywords` 通用敏感字段已加。

**保持现状（用户决策）**：`project_list` 只返回令牌绑定的那一个项目（不按用户过滤）；`audit_list` 只查当前令牌（不放开多维过滤）。

**后续待做**：①~~真端口 6680 端到端集成测试~~（✅ 2026-11-01 完成——`Stardust.McpApiTests` 32 例连真实 6681 `/mcp` 全绿，见 §15；6680 Web 宿主仍未测）；②~~TDD 测试用例补全~~（✅ ServerTests MCP 单测 136 例 + McpApiTests 32 例双层门禁，见 §15）。（已修 2026-10-31：Before/After 审计快照已接入，引擎在 L1/L2 写操作调用前后取值写入。）

---

## 15. 测试落地与发布记录（2026-11-01）

### 15.1 测试资产与结果

| 层 | 工程 | 规模 | 结果 |
|---|---|---|---|
| 单元/组件 | `Stardust.ServerTests`（`--filter ServerTest.Mcp`） | 136 例（守卫/契约/脱敏/审计/令牌默认值/G1 守卫） | 全绿 |
| API 集成 | `Stardust.McpApiTests`（官方 MCP SDK 2.0.0 连真实 6681 `/mcp`） | 32 例：协议 6 + 覆盖 3 + 边界 13 + 越权 10 | 32/32 全绿（约 6~16s） |
| 真实节点 | 覆盖-3 真实节点模式（本机 StarServer 6600 + StarAgent 在线） | 4 个 Agent 依赖工具真实执行 | 全部真实成功 |

真实节点环境：StarAgent（`Bin\Agent\net10.0`，`Config\StarAgent.config` 预置 Project=「MCP 测试」）注册为项目 4 节点 DGCWIAX10（Code=F4683130）；测试进程内嵌 AppClient（mcpapi-app）登录 6600 成为真实应用实例。实证留痕：StardustData.db `NodeCommand`/`AppCommand` 状态与 Result（cmd→`mcp-api-test`、freeMemory→`OK`、app/start→`start-ok`、deploy/start→错误「无法找到服务」为预期业务回包）。

### 15.2 测试发现并修复的缺陷

- **TraceTools 分片路由缺陷**（产品缺陷，唯一）：`trace_search`/`trace_detail` 在 SQLite 分库分表下查主表必失败（`no such table`）→ 修复为包 `Meta.CreateShard`（`TraceSearchTool` 包 `TraceData.Meta.CreateShard(since)`、`TraceDetailTool` 包 `SampleData.Meta.CreateShard(DateTime.Today)`）。
- 其余为测试侧改造：SDK 远程错误断言用 `McpProtocolException.ErrorCode` 数值（消息不含码）；McpResult 的 records/pagination 在 `data` 下；越权靶资源显式造跨项目节点；`config_rollback` 参数用 `ConfigHistoryId`；**防假绿三件套**——①服务端等不到回包的兜底包是 `{status=就绪,data=null}`，回包断言必须要求 data 非空；②探测真实节点须排除初始化器造的假在线记录（mcpapi-* 前缀）；③内嵌 AppClient 须注册 `app/start` 等处理器，否则靠「命令不存在」错误包假绿。

### 15.3 回归门禁（固定两条命令）

```bash
dotnet test --filter ServerTest.Mcp      # 136 单测：引擎/守卫/契约/脱敏（无需起服务）
dotnet test Stardust.McpApiTests         # 32 集成：需 6681 起着；真实执行需 6600+Agent 在线（离线自动降级网关受理断言）
```

回归保护映射：TraceTools 修复→覆盖-3 爆；G1/ParseResources/indexer→`McpTokenUpdateGuardTests` 等单测爆；契约/守卫/脱敏/越权→双层爆。**唯一缺口：6680 Web 宿主 MCP 链无自动化测试**（N1/P2 因此漏网，Web 版守卫无测试）。

### 15.4 发布记录（2026-11-01 23:33）

- 命令：`dotnet publish Stardust.WebApi/Stardust.WebApi.csproj -c Release --no-restore`（无参数，默认目录 `Bin\WebApi\publish`）。
- **教训：publish 前必须停 6681**——csproj `OutputPath=..\Bin\WebApi` 与运行位同目录，运行中进程锁 DLL（MSB3027）；失败复制可能留残，重发布自愈。
- 校验：publish\Stardust.Mcp.dll SHA256 与顶层一致（含 TraceTools 修复）；重启后 `/cube/info` 200 + 启动日志 `MCP engine initialized with 32 tool(s)`；冒烟 32/32 全绿。
- 状态：6681（API 宿主）**已发布可上线**；6680（Web 宿主）**未随发**——N1（Web 版默认 scope 含 `deploy:write`，与 D17 不一致）、P2（Web 版资源授权缺 IsAll=false 强制，与 D18 不一致）未修，修复并补 Web 版单测后再发。
