# MCP 客户端接入指南

> 版本：v1.1 | 日期：2026-08-03
> 对应模块：MCP 工具服务能力（客户端侧配置与使用）
> 相关文档：[MCP-MCP架构](MCP-MCP架构.md) | [MCP-1-token管理](MCP-1-token管理.md) | [MCP-2-action扩展](MCP-2-action扩展.md)

---

## 0. 本文档解决什么

前面三份文档讲了服务端**架构、Token 管理、Action 扩展**，但没有讲**客户端怎么连**。本文档专门说明：

- 客户端（Cursor / Trae / Claude Desktop / VS Code 等）的 `mcp.json` 怎么写
- MCP 端点的真实地址与端口
- 连接前必须做的前置配置
- 连通性验证与常见错误排查

> 如果你还没建 Token，先读 [MCP-1-token管理](MCP-1-token管理.md)；想新增 Action 读 [MCP-2-action扩展](MCP-2-action扩展.md)。

---

## 1. 前置条件（少一步都连不上）

### 1.1 打开全局开关 `EnableMcp`

`/mcp` 端点由 `McpMiddleware`（在 Cube Web 中间件之前短路）统一处理，开关关闭时返回 404。开关是 DB 配置实体 `StarServerSetting.EnableMcp`（`Stardust.Server/Setting.cs`，默认 `false`）。

- 打开方式：**Stardust Web 控制台 → 系统设置（StarServerSetting）→ 启用 MCP 服务** 设为 `true`。
- 同页还有 `McpActionSet`（默认 `*` 表示全部动作启用；可填逗号分隔的模块名 `node,app,config,deploy,gateway,monitor,system` 做白名单）。

### 1.2 Token 已创建且可用

按 [MCP-1-token管理](MCP-1-token管理.md) 建好 Token 后，确认：

- `Enable = true`（未禁用）
- `ExpireTime` 未过期（或留空永不过期）
- **已绑定资源授权**（项目/节点/应用，或勾「全部」）。不绑资源时只能调 `list_authorized_resources` / `search_resources`，调 `invoke_action` 会返回 `-32003` 未授权。

Token 字符串形如 `sdmcp_` + 32 位随机字符。**只在创建/重置时显示一次**，请妥善保存。

---

## 2. 端点与端口（重点澄清）

| 项 | 值 | 说明 |
|---|---|---|
| 路径 | `POST /mcp` | `McpMiddleware` 短路处理，路由定义见 `McpMiddleware` 的 `InvokeAsync` |
| 协议 | JSON-RPC 2.0 over HTTP | 单请求单响应 |
| 鉴权 | `Authorization: Bearer <token>` | 从 HTTP Header 取，恒定时间比较 |
| 宿主端口 | **`6680`** | `Stardust.Web/appsettings.json` 的 `Urls: http://*:6680` |
| 传输类型 | 纯 HTTP，**无 SSE、无 `Mcp-Session-Id`** | 见 §5 兼容性说明 |

> ⚠️ **端口易错**：架构文档示例里写的 `:6600` 是 `StarServer` 控制面（数据面 API）的端口。**MCP 端点由 Web 项目承载，实际端口是 `6680`**。客户端务必指向 6680。

完整端点地址示例：`http://<服务器可达地址>:6680/mcp`

- 本机 IDE 直连：`http://localhost:6680/mcp`
- 远程/容器部署：填宿主机 IP 或可解析域名，确保 6680 端口可达（防火墙/反向代理放行）。

---

## 3. mcp.json 写法

MCP 客户端的主流配置是「Streamable HTTP」传输，用 `type: "http"` + `url` + `headers`：

```json
{
  "mcpServers": {
    "stardust": {
      "type": "http",
      "url": "http://<服务器可达地址>:6680/mcp",
      "headers": {
        "Authorization": "Bearer sdmcp_你的token"
      }
    }
  }
}
```

字段说明：

- `url`：固定为 `<host>:6680/mcp`，**不要**漏掉 `/mcp` 路径后缀。
- `headers.Authorization`：把你的 Token 原样填入，`Bearer ` 后面跟 `sdmcp_xxx`。
- **不要**在 `headers` 里手动加 `Accept`，客户端 SDK 会自动带 `application/json, text/event-stream`。
- 各客户端配置文件位置（自行对应）：
  - Cursor：`~/.cursor/mcp.json` 或项目 `.cursor/mcp.json`
  - Trae：IDE 设置 → MCP → 配置文件
  - Claude Desktop：`claude_desktop_config.json`
    - VS Code：`settings.json` 的 `mcp.servers` 或 `.vscode/mcp.json`

---

## 4. 连通性验证

连上或用客户端前，先用 curl 验证端点与 Token 是否生效（端口改 6680）：

```bash
# 1) 握手 + 列出 5 个工具
curl -X POST http://localhost:6680/mcp \
  -H "Authorization: Bearer sdmcp_你的token" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

期望：返回含 `serverInfo`（name=Stardust）和 5 个工具的 `tools` 数组。

```bash
# 2) 查看当前 Token 授权了哪些资源
curl -X POST http://localhost:6680/mcp \
  -H "Authorization: Bearer sdmcp_你的token" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_authorized_resources","arguments":{}}}'

# 3) 列出可调用动作
curl -X POST http://localhost:6680/mcp \
  -H "Authorization: Bearer sdmcp_你的token" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_actions","arguments":{}}}'
```

调用动作示例（`invoke_action`）：

```bash
curl -X POST http://localhost:6680/mcp \
  -H "Authorization: Bearer sdmcp_你的token" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"node_send_command","params":{"node_id":100,"command":"status"}}}}'
```

---

## 5. 兼容性说明（重要）

`/mcp` 端点是**极简 JSON-RPC 2.0 over HTTP（Streamable HTTP）** 实现：

- `POST` 单请求单响应，`Content-Type: application/json`
- **无状态**：不实现 SSE 长连接，不返回 `Mcp-Session-Id`，`GET`/`DELETE /mcp` 一律返回 `405`
- **协议版本协商**：支持 `2024-11-05`、`2025-03-26`、`2025-06-18`、`2026-07-28`；客户端 `initialize` 携带的版本号会被协商为服务端支持的最高兼容版本，低于 `2024-11-05` 返回 `-32602`

现代 MCP 客户端（Cursor / Trae / Claude Desktop / VS Code，基于 2025-03-26 及之后 SDK）的 `type: "http"`（Streamable HTTP）对「服务器不回 Session-Id、只回纯 JSON」是**兼容的**（`Mcp-Session-Id` 在规范里可选）。所以上面 §3 的写法通常能直接连上。

> 本功能尚未正式上线，**仅面向支持 Streamable HTTP 的现代客户端**，不兼容仅支持旧版 SSE 会话握手的客户端，无需 stdio 桥接。

---

## 6. 工具与动作速查

### 6.1 五个 MCP 工具

| 工具名 | 作用 | 资源校验 |
|---|---|---|
| `list_authorized_resources` | 当前 Token 授权了哪些资源 | 无 |
| `search_resources` | 按关键字跨类型搜资源 | 按授权过滤结果 |
| `get_resource` | 取单个资源详情（需 `resource_type` + `resource_id`） | 动态校验 |
| `list_actions` | 列出可调用动作 | 无 |
| `invoke_action` | 调具体动作（需 `action_name` + `params`） | 由 action 的 `RequiredResource` 声明 |

### 6.2 常用动作（完整 27 个见 [MCP-MCP架构](MCP-MCP架构.md) §5）

- 节点：`node_list_online` / `node_send_command` / `node_upgrade` / `node_search`
- 应用：`app_list_online` / `app_restart` / `app_stop` / `app_start` / `app_send_command`
- 配置：`config_get` / `config_set`
- 发布：`deploy_list` / `deploy_install` / `pipeline_trigger` / `pipeline_get_run`
- 网关：`gateway_list_routes` / `gateway_list_clusters`
- 监控：`monitor_trace_search` / `monitor_alarm_list`

---

## 7. 错误码排查

| 错误码 | 含义 | 处理 |
|---|---|---|
| `404` | `EnableMcp=false` 或路径错 | 开开关；确认 `/mcp` 与端口 6680 |
| `-32001` | Token 缺失/禁用/过期/不匹配 | 检查 Bearer 头；确认 Token 启用了且未过期 |
| `-32003` | 资源未授权 | 在 Token 表单绑定对应项目/节点/应用 |
| `-32601` | action 被 `McpActionSet` 禁用 | 调整 `McpActionSet` |
| `-32602` | 参数校验失败 | 检查 `params` 必填字段 |
| `-32000` | 服务端业务异常 | 看返回的 `error.message` |
| `-32002` | 动作超时（默认 30s） | 拆分任务或排查后端 |

---

## 8. 一句话总结

端点 `http://<host>:6680/mcp`，Bearer Token 鉴权；mcp.json 用 `type:"http"` + `url` + `headers.Authorization`；先开 `EnableMcp` 开关、再绑资源授权，否则连不上或调不动。
