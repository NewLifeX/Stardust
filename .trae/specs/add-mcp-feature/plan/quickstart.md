# Quickstart — MCP HTTP 服务端到端验证

**Created**: 2026-07-16
**Spec**: [spec.md](../spec.md)
**Plan**: [plan.md](./plan.md)

## 概述

本文档提供 MCP HTTP 服务的端到端验证场景，每个场景包含前置条件、操作步骤、预期结果。可用于开发完成后的功能验证和回归测试。

详细协议契约见 [contracts/mcp-protocol.md](./contracts/mcp-protocol.md)，数据模型见 [data-model.md](./data-model.md)。

---

## 前置条件

### 环境准备

1. **Stardust 服务端运行**：
   - `Stardust.Web` 项目编译成功
   - 数据库已创建 3 张新表（`McpToken` / `McpTokenResource` / `McpAudit`）
   - `StarServerSetting.EnableMcp` 已设为 `true`（默认 `false`）

2. **测试数据**：
   - 至少 1 个 `GalaxyProject`（如 "测试项目A"）
   - 至少 1 个 `Node`（属于测试项目A）
   - 至少 1 个 `App`（属于测试项目A）

3. **MCP Token**：
   - 在 Web `/Platform/McpToken` 页面创建 Token，名称 "测试Token"
   - 勾选授权"测试项目A"
   - 复制生成的 Token 字符串（格式 `sdmcp_xxx`）

4. **工具**：
   - `curl` 或 PowerShell `Invoke-RestMethod` 用于发送 HTTP 请求
   - 或任意 MCP 客户端（Claude Desktop / Trae / Cursor）

---

## 场景 1：MCP 服务总开关验证

### 验证目标

`EnableMcp=false` 时 `/mcp` 返回 404，`true` 时正常响应。

### 步骤

1. **关闭 MCP**：
   - 在 Web 设置页将 `EnableMcp` 改为 `false`
   - 发送请求：
     ```bash
     curl -X POST http://localhost:6600/mcp \
       -H "Authorization: Bearer sdmcp_xxx" \
       -H "Content-Type: application/json" \
       -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
     ```
   - **预期**：HTTP 404 Not Found

2. **开启 MCP**：
   - 在 Web 设置页将 `EnableMcp` 改为 `true`
   - 重新发送上述请求
   - **预期**：HTTP 200，返回 JSON-RPC `initialize` 响应

---

## 场景 2：Token 鉴权验证

### 验证目标

无 Token / 错误 Token / 禁用 Token / 过期 Token 均返回 `-32001`。

### 步骤

1. **无 Token**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
   ```
   - **预期**：`{"error":{"code":-32001,"message":"Unauthorized: missing token"}}`

2. **错误 Token**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_invalid_token" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
   ```
   - **预期**：`{"error":{"code":-32001,"message":"Unauthorized: token not found"}}`

3. **禁用 Token**：
   - 在 Web 将测试 Token 的 `Enable` 改为 `false`
   - 用正确 Token 发送请求
   - **预期**：`{"error":{"code":-32001,"message":"Unauthorized: token is disabled"}}`
   - 恢复：将 `Enable` 改回 `true`

4. **过期 Token**：
   - 在 Web 将测试 Token 的 `ExpireTime` 改为 `2020-01-01`
   - 发送请求
   - **预期**：`{"error":{"code":-32001,"message":"Unauthorized: token is expired"}}`

---

## 场景 3：LLM 首次接入建立上下文

### 验证目标

LLM 通过 `initialize` + `list_authorized_resources` + `list_actions` 建立完整上下文。

### 步骤

1. **初始化**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}'
   ```
   - **预期**：返回 `serverInfo`（name=Stardust）和 `capabilities.tools`

2. **查授权资源**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_authorized_resources","arguments":{}}}'
   ```
   - **预期**：返回测试项目A 的标识符
   - **验证**：结果中不包含未授权的项目

3. **查可用动作**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_actions","arguments":{}}}'
   ```
   - **预期**：返回 27 个 action 的完整元信息
   - **验证**：每个 action 含 `name`/`description`/`module`/`input_schema`/`required_resource`

---

## 场景 4：搜索并获取资源

### 验证目标

`search_resources` + `get_resource` 形成"搜索→获取"查询原语对。

### 步骤

1. **搜索资源**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"search_resources","arguments":{"keyword":"测试"}}}'
   ```
   - **预期**：返回匹配的资源列表（含测试项目A、其下的节点和应用）
   - **验证**：未授权资源不在结果中

2. **获取资源详情**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_resource","arguments":{"resource_type":"node","resource_id":100}}}'
   ```
   - **预期**：返回节点详情（含 name/code/ip/project_id 等）
   - **验证**：框架层校验 node 100 在授权范围（属于测试项目A）

3. **获取未授权资源**：
   - 假设 node 200 属于未授权的"项目B"
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"get_resource","arguments":{"resource_type":"node","resource_id":200}}}'
   ```
   - **预期**：`{"error":{"code":-32003,"message":"Forbidden: resource node_id=200 is not authorized"}}`

---

## 场景 5：节点命令下发（写操作）

### 验证目标

`invoke_action(node_send_command)` 资源校验 + 命令下发。

### 步骤

1. **下发命令到授权节点**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"node_send_command","params":{"node_id":100,"command":"ping","args":""}}}}'
   ```
   - **预期**：返回 `{"success":true,"message":"命令已下发",...}`

2. **下发命令到未授权节点**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"node_send_command","params":{"node_id":200,"command":"ping"}}}}'
   ```
   - **预期**：`{"error":{"code":-32003,"message":"Forbidden: resource node_id=200 is not authorized"}}`

3. **调用不存在的 action**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"nonexistent_action","params":{}}}}'
   ```
   - **预期**：`{"error":{"code":-32601,"message":"Method not found: action nonexistent_action"}}`

4. **参数缺失**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"node_send_command","params":{"node_id":100}}}}'
   ```
   - **预期**：`{"error":{"code":-32602,"message":"Invalid params: missing required field command"}}`

---

## 场景 6：间接资源校验（部署操作）

### 验证目标

`deploy_install` 通过 `deploy_id` 间接反查 `ProjectId` 进行授权校验。

### 前置

- 测试项目A 下有 1 个 `AppDeploy`（如 id=10）
- 未授权的"项目B"下有 1 个 `AppDeploy`（如 id=20）
- 测试项目A 下有 1 个授权节点（如 id=100）

### 步骤

1. **部署到授权项目+授权节点**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"deploy_install","params":{"deploy_id":10,"node_id":100}}}}'
   ```
   - **预期**：框架层校验 deploy_id=10 → AppDeploy.ProjectId=测试项目A → 授权 ✓，node_id=100 → 授权 ✓，返回成功

2. **部署到未授权项目**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"deploy_install","params":{"deploy_id":20,"node_id":100}}}}'
   ```
   - **预期**：`{"error":{"code":-32003,"message":"Forbidden: resource deploy_id=20 (project) is not authorized"}}`

3. **部署到未授权节点**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":13,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"deploy_install","params":{"deploy_id":10,"node_id":200}}}}'
   ```
   - **预期**：`{"error":{"code":-32003,"message":"Forbidden: resource node_id=200 is not authorized"}}`

---

## 场景 7：动作集过滤

### 验证目标

`McpActionSet` 配置限制可用动作模块。

### 步骤

1. **配置仅启用 node+app 模块**：
   - 在 Web 设置页将 `McpActionSet` 改为 `node,app`
   - 调用 `list_actions`：
     ```bash
     curl -X POST http://localhost:6600/mcp \
       -H "Authorization: Bearer sdmcp_xxx" \
       -H "Content-Type: application/json" \
       -d '{"jsonrpc":"2.0","id":14,"method":"tools/call","params":{"name":"list_actions","arguments":{}}}'
     ```
   - **预期**：仅返回 node 和 app 模块的 action（4+7=11 个）

2. **调用被禁用的 deploy action**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":15,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"deploy_list","params":{}}}}'
   ```
   - **预期**：`{"error":{"code":-32601,"message":"Method not found: action deploy_list is disabled"}}`

3. **恢复全部启用**：
   - 将 `McpActionSet` 改回 `*`
   - `list_actions` 返回全部 27 个 action

---

## 场景 8：审计日志查询

### 验证目标

`get_audit_log` 返回当前 Token 的调用记录。

### 步骤

1. **执行若干操作**（场景 3-7 已产生多条审计记录）

2. **查询审计日志**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":16,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"get_audit_log","params":{"page":1,"page_size":10}}}}'
   ```
   - **预期**：返回最近 10 条调用记录
   - **验证**：每条含 `tool_name`/`action_name`/`success`/`duration`/`create_time`
   - **验证**：`arguments` 字段中敏感信息已脱敏

3. **筛选失败记录**：
   ```bash
   curl -X POST http://localhost:6600/mcp \
     -H "Authorization: Bearer sdmcp_xxx" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":17,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"get_audit_log","params":{"success":false}}}}'
   ```
   - **预期**：仅返回失败的调用记录（如场景 5/6 中的 -32003 错误）

---

## 场景 9：Web 管理 Token 与资源授权

### 验证目标

管理员在 Web 页面创建 Token、勾选资源、禁用 Token。

### 步骤

1. **创建 Token**：
   - 访问 `/Platform/McpToken`
   - 点击"新建"
   - 填写名称"运维助手-测试"
   - 勾选资源：
     - 项目：勾选"测试项目A"
     - 节点：勾选"全部节点"（IsAll=true）
     - 应用：不勾选
   - 点击"保存"
   - **预期**：
     - Token 字符串自动生成（`sdmcp_` 前缀）
     - 页面显示生成的 Token 字符串
     - `McpTokenResource` 表有 2 条记录（1 条 Project，1 条 Node IsAll=true）

2. **验证 Token 授权**：
   - 用新 Token 调用 `list_authorized_resources`
   - **预期**：返回测试项目A + 全部节点
   - 调用 `invoke_action(app_send_command, {app_id:200,...})`
   - **预期**：返回 -32003（应用未授权）

3. **禁用 Token**：
   - 在 Web 将"运维助手-测试"的 `Enable` 改为 `false`
   - 用该 Token 调用 `/mcp`
   - **预期**：返回 -32001

4. **重置 Token**：
   - 在 Web 点击"重置"
   - **预期**：
     - 旧 Token 立即失效
     - 新 Token 字符串生成
     - 资源授权关系保留

---

## 场景 10：扩展新动作

### 验证目标

开发者添加新 `IMcpAction` 实现类后，重启即可被 `list_actions` 发现。

### 步骤

1. **新建 action 类**：
   - 在 `Stardust.Web/Mcp/Actions/System/` 下新建 `GetSystemInfoAction.cs`
   ```csharp
   public class GetSystemInfoAction : McpActionBase
   {
       public override String Name => "get_system_info";
       public override String Description => "获取系统信息";
       public override String Module => "system";
       public override JObject InputSchema => new();
       public override ResourceRequirement? RequiredResource => null;
       
       public override Task<Object> InvokeAsync(JsonElement params, CancellationToken ct)
       {
           return Task.FromResult<Object>(new {
               version = "1.0.0",
               server_time = DateTime.Now,
               node_count = Node.Meta.Count,
               app_count = App.Meta.Count
           });
       }
   }
   ```

2. **重启 Stardust.Web**

3. **验证**：
   - 调用 `list_actions`
   - **预期**：返回结果中包含 `get_system_info`
   - 调用 `invoke_action(get_system_info, {})`
   - **预期**：返回系统信息 JSON

---

## 场景 11：客户端集成（Claude Desktop）

### 验证目标

主流 MCP 客户端可连接 Stardust MCP 服务。

### 步骤

1. **配置 Claude Desktop**：
   - 编辑 `claude_desktop_config.json`：
     ```json
     {
       "mcpServers": {
         "stardust": {
           "url": "http://localhost:6600/mcp",
           "headers": {
             "Authorization": "Bearer sdmcp_xxx"
           }
         }
       }
     }
     ```
   - 重启 Claude Desktop

2. **测试对话**：
   - 用户："帮我查一下现在有哪些节点在线？"
   - **预期**：Claude 调用 `list_authorized_resources` → `list_actions` → `invoke_action(node_list_online, {})`，返回在线节点列表

   - 用户："把 web-node-1 节点重启一下"
   - **预期**：Claude 调用 `search_resources(keyword=web-node-1)` → `invoke_action(node_send_command, {node_id:..., command:restart})`

---

## 验证清单总结

| 场景 | 验证目标 | 关键断言 |
|---|---|---|
| 1 | MCP 总开关 | EnableMcp=false 返回 404 |
| 2 | Token 鉴权 | 4 种无效 Token 均返回 -32001 |
| 3 | LLM 上下文建立 | 27 action 完整返回，授权资源过滤正确 |
| 4 | 搜索+获取 | search→get 工作流，未授权资源 -32003 |
| 5 | 写操作 + 资源校验 | 命令下发成功，未授权 -32003，参数缺失 -32602 |
| 6 | 间接资源校验 | deploy_id→ProjectId 反查正确，多资源同时校验 |
| 7 | 动作集过滤 | McpActionSet 生效，禁用的 action 返回 -32601 |
| 8 | 审计日志 | get_audit_log 返回记录，脱敏正确 |
| 9 | Web Token 管理 | 创建/禁用/重置全流程 |
| 10 | 扩展新动作 | 添加类即可自动注册 |
| 11 | 客户端集成 | Claude Desktop 可连接并执行操作 |

所有场景通过后，MCP HTTP 服务功能完整可用。
