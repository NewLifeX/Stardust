# DEPLOY-1 节点应用文件日志

> 版本：v1.0 | 日期：2026-11-03  
> 关联：`Doc/NODE-9-指令下发.md`、`Doc/SVR-8-远程命令执行安全.md`  
> 状态：**P1 MVP 已实现**（2026-11-03）

---

## 1. 背景

运维需要在星尘控制台查看**部署在节点上的应用**写在本地的日志文件。现有能力无法替代：

| 能力 | 局限 |
|------|------|
| 应用页签 **AppClientLog** | 需 SDK 上报，非文件 stdout |
| 节点 **抓日志** | StarAgent 自身 XTrace，非业务应用 |
| **bash/cmd** | 适合应急，不宜作为日常看日志入口 |

## 2. 目标

1. **AppDeploy** / **AppDeployNode** 配置日志目录（节点可覆盖部署集默认值）及文件匹配规则。
2. 控制台在「部署节点」行操作 **查看日志**：列文件、读尾部、手动刷新。
3. 经 **Node 指令通道**（WebSocket + 心跳备份）同步等待回包，**禁止**用 bash 拼 `tail`。

## 3. 数据模型

### AppDeploy（部署集默认）

| 字段 | 类型 | 说明 |
|------|------|------|
| LogDirectory | String(500) | 日志目录；绝对路径，或相对 WorkingDirectory |
| LogFilePattern | String(200) | 默认 `*.log` |

### AppDeployNode（部署节点，可覆盖）

同上两字段；合并规则与 `WorkingDirectory` 一致（节点为空 → 用部署集）。

解析（Agent 与平台一致）：

- `LogDirectory` 为空 → 不可查看，API 返回明确提示。
- 非根路径且非 `X:\` / `/` 开头 → 与 **EffectiveWorkingDirectory** 组合。

## 4. Agent 指令

注册于 `ServiceManager.Attach`（StarAgent 进程）：

### `deploy/log/list`

请求 JSON：

```json
{
  "logDirectory": "D:/app/logs",
  "workingDirectory": "D:/app",
  "pattern": "*.log"
}
```

响应 JSON（写入 `CommandReplyModel.Data`）：

```json
{
  "files": [
    { "name": "app.log", "size": 12345, "lastWriteUtc": "2026-11-03T02:00:00Z" }
  ]
}
```

限制：最多 100 个文件；路径必须在解析后的日志根目录内。

### `deploy/log/tail`

请求 JSON：

```json
{
  "logDirectory": "D:/app/logs",
  "workingDirectory": "D:/app",
  "file": "app.log",
  "offset": 0,
  "maxBytes": 262144
}
```

响应 JSON：

```json
{
  "content": "...",
  "offset": 0,
  "fileSize": 999999,
  "truncated": false
}
```

安全：`file` 仅允许文件名（无 `..`、无目录分隔符）；读文件用 `FileShare.ReadWrite`；单次 maxBytes 上限 512KB（Agent 与 API 双端限制）。

## 5. HTTP API（WebApi / Web）

| 方法 | 路径 | 权限 |
|------|------|------|
| GET | `/Deployment/AppDeployNode/LogFiles?id={deployNodeId}` | Detail |
| GET | `/Deployment/AppDeployNode/LogTail?id=&file=&offset=&maxBytes=` | Detail |

流程：加载 `AppDeployNode` → 合并日志配置 → `StarFactory.SendNodeCommandAsync(node.Code, command, args, timeout: 25)` → 解析回包 JSON。

## 6. 前端（StardustLayout2 / Console）

完整交互、接口、验收见 **[DEPLOY-3-前端应用日志需求.md](DEPLOY-3-前端应用日志需求.md)**。

- **NodeFormDrawer**：日志目录、文件匹配（部署节点 kind=deploy）。
- **DeployNodes**：「查看日志」→ **DeployLogDrawer**（文件选择、等宽展示、刷新 loading）。
- **ShipPanel**（应用工作台部署页）：接线 `DeployLogDrawer`。
- 逻辑：**deployLog.logic.ts** + Vitest。

与 **AppClientLog** 页签区分：本能力为 **节点文件日志**。

## 7. 分期

| 阶段 | 内容 |
|------|------|
| **P1 MVP** | 字段 + list/tail + API + 抽屉 + 手动刷新 |
| **P2（2026-11-04 已交付）** | **MCP 查询工具** `stardust_deploy_log_files` / `stardust_deploy_log_tail`（L0/deploy，见 §11）；抽屉**自动轮询开关 + 1~5 秒间隔**；**向上翻页**（加载更早，256KB/页，累计 2MB 上限，Agent offset=0 语义为读尾部故头部窗口用 offset=1）；**下载当前窗口**（.log 文件）；文件大小友好格式 |
| **P3（2026-11-04 已交付）** | 准实时 tail：轮询增量追新（offset=上次 fileSize，内容封顶 1MB）；SSE/WebSocket 留作后续可选增强 |
| 遗留 | 更细离线/Agent 版本提示；P4 K8s：`kubectl logs`（待工作区在途 k8s 改动落库后再做） |

## 8. 本地联调

见 **[DEPLOY-2-本地应用日志联调环境.md](DEPLOY-2-本地应用日志联调环境.md)**（`Doc/scripts/deploy-log/setup_local.py`、分层验证 L1/L2/L3）。

## 9. 自动化测试（P1 关键路径）

| 层级 | 工程 / 文件 | 覆盖 |
|------|-------------|------|
| Agent 指令 | `ClientTest/DeployLogCommandsTests.cs` | 路径解析、list/tail  happy path、目录不存在、非法文件名 |
| 配置合并 | `Stardust.WebApiTests/Areas/DeploymentDeployLogApiTests.cs` | `GetEffectiveLogDirectory` / Pattern 节点覆盖与回落 |
| HTTP API | 同上 | 未配置目录、缺 file、无效 id；已配置时进入下发（无 Agent 则非「未配置」错误） |
| 前端 logic | `deployLog.logic.test.ts` | parse / fetch 契约与失败透传；`applyTailSnapshot` 增量合并（追加/替换/回卷/1MB 封顶，14 用例） |
| 部署节点表单 | `deploy.logic.test.ts` | logDirectory / logFilePattern 映射与 update body |
| MCP 工具 | `Stardust.McpApiTests/DeployLogToolsTests.cs` | 未配置目录优雅空数据+note、节点不存在/缺 file/路径分隔符 -32602、非成员 -32003（6 用例） |
| MCP 覆盖 | `Stardust.McpApiTests/McpToolCoverageTests.cs` | tools/list 计数 34；逐工具正路径（含两个新工具） |
| 本机 E2E | `.temp/smoke_deploy_log.py` | 可选 L3，非 CI 门禁 |

全链路 **WebApi → StarServer → StarAgent → 磁盘** 仍依赖 DEPLOY-2 环境；未纳入 xUnit（成本高，用 L3 冒烟补）。

## 10. 主要代码

| 层 | 路径 |
|----|------|
| 实体 | `Stardust.Data/Deployment/Model.xml`、`应用节点.Biz.cs` |
| Agent | `Stardust/Managers/DeployLogCommands.cs`、`ServiceManager.cs` |
| API | `AppDeployNodeController.cs`、`DeployService`（LogFiles/LogTail） |
| MCP | `Stardust.Mcp/Tools/Deploy/DeployLogTools.cs`、`Stardust.Mcp/IMcpOperations.cs`（两宿主 `Services/McpOperations.cs`） |
| 前端 | `deployLog.logic.ts`、`DeployLogDrawer.vue`、`ShipPanel.vue` |

## 11. MCP 工具（P2，2026-11-04）

供 MCP 客户端/Agent 助手查询节点文件日志，与 HTTP API 同底座（`DeployService.ListLogFilesAsync/TailLogFileAsync`）：

| 工具 | 风险/域 | 参数 | 返回 |
|------|---------|------|------|
| `stardust_deploy_log_files` | L0 / deploy | `deploy_node_id` | `data.files[]{name,size,last_write_utc}`；未配置目录 → 空列表 + `note` 说明（不下发节点指令） |
| `stardust_deploy_log_tail` | L0 / deploy | `deploy_node_id`、`file`、`offset`（默认 0）、`max_bytes`（默认 256KB，上限 512KB） | `data.{content,offset,file_size,truncated}`；未配置 → 空 content + `note` |

- 节点不存在 / 缺 `file` / 文件名含路径分隔符或 `..` → `-32602`；项目归属不符 → `-32003`。
- 增量轮询建议（工具返回的 `rollback_hint`）：下一次调用把 `offset` 设为本次返回的 `file_size`。
- 宿主操作接口 `IMcpOperations.ReadDeployLogFilesAsync/ReadDeployLogTailAsync` 在 **Web 与 WebApi 两个宿主**各有实现，接口变更需同步两侧。
