# MCP Protocol Contract — Stardust MCP HTTP 服务

**Created**: 2026-07-16
**Spec**: [spec.md](../spec.md)
**Protocol Version**: `2024-11-05`
**Transport**: HTTP (单请求单响应，无 SSE)

## 概述

Stardust MCP 服务端点实现 [Model Context Protocol](https://spec.modelcontextprotocol.io/) `2024-11-05` 版本的 HTTP transport，基于 JSON-RPC 2.0 协议。

- **端点**：`POST /mcp`
- **Content-Type**：`application/json`
- **认证**：`Authorization: Bearer sdmcp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
- **响应格式**：`application/json`（JSON-RPC 2.0）

---

## 1. JSON-RPC 请求格式

所有请求均为 JSON-RPC 2.0 格式：

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": { ... }
}
```

- `jsonrpc`：固定字符串 `"2.0"`
- `id`：请求 ID，可为数字或字符串，服务端原样返回
- `method`：JSON-RPC 方法名（见下方方法清单）
- `params`：方法参数（可选）

---

## 2. JSON-RPC 响应格式

### 成功响应

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": { ... }
}
```

### 错误响应

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32001,
    "message": "Token is disabled or expired",
    "data": { ... }
  }
}
```

---

## 3. 错误码定义

| code | message | 触发条件 |
|---|---|---|
| `-32700` | Parse error | JSON 解析失败 |
| `-32600` | Invalid Request | 非 JSON-RPC 2.0 格式（缺 jsonrpc/id/method） |
| `-32601` | Method not found | 未识别的 method 或被 `McpActionSet` 禁用的 action |
| `-32602` | Invalid params | 参数缺失或不符合 `inputSchema` |
| `-32603` | Internal error | 未捕获的异常 |
| `-32001` | Unauthorized | Token 不存在/禁用/过期/未提供 |
| `-32002` | Timeout | 超时（默认 30 秒） |
| `-32003` | Forbidden | 资源未授权（资源 ID 不在 Token 授权范围） |
| `-32000` | Server error | action 业务异常 |

---

## 4. JSON-RPC 方法清单

### 4.1 `initialize`

**描述**：协议握手，返回服务端信息和能力声明。

**请求**：
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "capabilities": {},
    "clientInfo": {
      "name": "claude-desktop",
      "version": "1.0.0"
    }
  }
}
```

**响应**：
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

**鉴权**：需要 Token（若 `EnableMcp=false` 直接返回 HTTP 404）。

---

### 4.2 `tools/list`

**描述**：返回 5 个固定工具的清单。

**请求**：
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list",
  "params": {}
}
```

**响应**：
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "tools": [
      {
        "name": "list_authorized_resources",
        "description": "查询当前Token授权了哪些资源（项目/节点/应用），返回资源标识符和说明。LLM首次接入时调用此工具建立上下文。",
        "inputSchema": {
          "type": "object",
          "properties": {
            "resource_type": {
              "type": "string",
              "enum": ["project", "node", "app"],
              "description": "可选过滤，不传则返回全部三类"
            }
          }
        }
      },
      {
        "name": "search_resources",
        "description": "按关键字跨类型搜索资源，返回匹配的资源标识符和说明。与get_resource形成搜索→获取的查询原语对。",
        "inputSchema": {
          "type": "object",
          "properties": {
            "keyword": {
              "type": "string",
              "description": "搜索关键字（匹配资源名称/编码/IP等）"
            },
            "resource_type": {
              "type": "string",
              "enum": ["project", "node", "app", "deploy", "pipeline", "service"],
              "description": "可选过滤，不传则全搜"
            }
          },
          "required": ["keyword"]
        }
      },
      {
        "name": "get_resource",
        "description": "按资源类型+ID获取单个资源详情。与search_resources配对使用。",
        "inputSchema": {
          "type": "object",
          "properties": {
            "resource_type": {
              "type": "string",
              "enum": ["project", "node", "app", "deploy", "pipeline", "service"],
              "description": "资源类型"
            },
            "resource_id": {
              "type": "integer",
              "description": "资源ID"
            }
          },
          "required": ["resource_type", "resource_id"]
        }
      },
      {
        "name": "list_actions",
        "description": "返回当前可调用的动作清单（含name/description/inputSchema/requiredResource）。LLM在执行操作前调用此工具发现能做什么、需要什么参数。",
        "inputSchema": {
          "type": "object",
          "properties": {
            "module": {
              "type": "string",
              "enum": ["node", "app", "config", "deploy", "gateway", "monitor", "system"],
              "description": "可选过滤，不传则返回全部"
            }
          }
        }
      },
      {
        "name": "invoke_action",
        "description": "调用指定动作。这是LLM执行操作的唯一入口，通过action_name+params路由到具体实现。",
        "inputSchema": {
          "type": "object",
          "properties": {
            "action_name": {
              "type": "string",
              "description": "动作名（snake_case），如node_send_command。可通过list_actions查询可用动作。"
            },
            "params": {
              "type": "object",
              "description": "动作参数，结构由对应action的inputSchema定义",
              "additionalProperties": true
            }
          },
          "required": ["action_name", "params"]
        }
      }
    ]
  }
}
```

---

### 4.3 `tools/call`

**描述**：调用指定工具。

**请求**：
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "invoke_action",
    "arguments": {
      "action_name": "node_send_command",
      "params": {
        "node_id": 100,
        "command": "restart",
        "args": ""
      }
    }
  }
}
```

**响应（成功）**：
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"success\":true,\"message\":\"命令已下发\",\"command_id\":\"abc123\"}"
      }
    ]
  }
}
```

**响应（错误 - 资源未授权）**：
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "error": {
    "code": -32003,
    "message": "Forbidden: resource node_id=100 is not authorized for this token"
  }
}
```

---

## 5. 5 个 MCP 工具的 inputSchema 完整定义

### 5.1 `list_authorized_resources`

```json
{
  "type": "object",
  "properties": {
    "resource_type": {
      "type": "string",
      "enum": ["project", "node", "app"],
      "description": "可选过滤，不传则返回全部三类"
    }
  }
}
```

**返回 content 示例**：
```json
{
  "projects": [
    {"id": 1, "name": "订单系统", "description": "核心订单业务"}
  ],
  "nodes": [
    {"id": 100, "name": "web-node-1", "description": "Web服务器节点", "ip": "10.0.0.1"}
  ],
  "apps": [
    {"id": 200, "name": "order-service", "description": "订单服务应用"}
  ]
}
```

---

### 5.2 `search_resources`

```json
{
  "type": "object",
  "properties": {
    "keyword": {
      "type": "string",
      "description": "搜索关键字（匹配资源名称/编码/IP等）"
    },
    "resource_type": {
      "type": "string",
      "enum": ["project", "node", "app", "deploy", "pipeline", "service"],
      "description": "可选过滤，不传则全搜"
    }
  },
  "required": ["keyword"]
}
```

**返回 content 示例**：
```json
{
  "results": [
    {"type": "project", "id": 1, "name": "订单系统", "description": "核心订单业务"},
    {"type": "app", "id": 200, "name": "order-service", "description": "订单服务应用"},
    {"type": "node", "id": 100, "name": "web-node-1", "description": "Web服务器节点"}
  ]
}
```

---

### 5.3 `get_resource`

```json
{
  "type": "object",
  "properties": {
    "resource_type": {
      "type": "string",
      "enum": ["project", "node", "app", "deploy", "pipeline", "service"],
      "description": "资源类型"
    },
    "resource_id": {
      "type": "integer",
      "description": "资源ID"
    }
  },
  "required": ["resource_type", "resource_id"]
}
```

**返回 content（以 `node` 为例）**：
```json
{
  "id": 100,
  "name": "web-node-1",
  "code": "WN001",
  "ip": "10.0.0.1",
  "project_id": 1,
  "project_name": "订单系统",
  "enable": true,
  "version": "2.1.0",
  "last_active": "2026-07-16T15:30:00",
  "category": "Web服务器",
  "os": "Linux",
  "cpu": "4核",
  "memory": "8GB"
}
```

---

### 5.4 `list_actions`

```json
{
  "type": "object",
  "properties": {
    "module": {
      "type": "string",
      "enum": ["node", "app", "config", "deploy", "gateway", "monitor", "system"],
      "description": "可选过滤，不传则返回全部"
    }
  }
}
```

**返回 content 示例**：
```json
{
  "actions": [
    {
      "name": "node_send_command",
      "description": "向节点下发命令",
      "module": "node",
      "input_schema": {
        "type": "object",
        "properties": {
          "node_id": {"type": "integer", "description": "节点ID"},
          "command": {"type": "string", "description": "命令名，如restart/stop/start"},
          "args": {"type": "string", "description": "命令参数（可选）"},
          "expire": {"type": "integer", "description": "过期时间秒（可选，默认3600）"}
        },
        "required": ["node_id", "command"]
      },
      "required_resource": {
        "type": "node",
        "field": "node_id"
      }
    }
  ]
}
```

---

### 5.5 `invoke_action`

```json
{
  "type": "object",
  "properties": {
    "action_name": {
      "type": "string",
      "description": "动作名（snake_case），如node_send_command。可通过list_actions查询可用动作。"
    },
    "params": {
      "type": "object",
      "description": "动作参数，结构由对应action的inputSchema定义",
      "additionalProperties": true
    }
  },
  "required": ["action_name", "params"]
}
```

**返回 content 示例**：
```json
{
  "success": true,
  "message": "命令已下发",
  "command_id": "abc123",
  "node_code": "WN001"
}
```

---

## 6. 鉴权流程

### 6.1 Token 提取

```
Authorization: Bearer sdmcp_abc123def456...
```

服务端从 `Authorization` Header 提取 `Bearer ` 后的字符串，作为 Token 查询 `McpToken` 表。

### 6.2 Token 校验

```
1. 提取 Token 字符串（若无 → -32001 Unauthorized）
2. 查 McpToken.FindByToken(token)（恒定时间比较）
3. 若 Token 不存在 → -32001 Unauthorized
4. 若 Enable=false → -32001 Unauthorized "Token is disabled"
5. 若 ExpireTime < DateTime.Now → -32001 Unauthorized "Token is expired"
6. 更新 McpToken.LastTime/LastIP/CallCount
7. 继续 JSON-RPC 方法处理
```

### 6.3 资源授权校验

仅 `get_resource` 和 `invoke_action` 工具需要资源授权校验：

```
1. 根据 resource_type 或 action 的 RequiredResource 声明
2. 从 params 提取资源 ID（如 node_id）
3. 若 Indirect=true，通过预注册解析器反查 ProjectId/AppId
4. 查 McpTokenResource.IsAuthorized(tokenId, resourceType, resourceId)
5. 若未授权 → -32003 Forbidden
```

---

## 7. 完整调用示例（LLM 工作流）

### 步骤 1：初始化

```http
POST /mcp HTTP/1.1
Authorization: Bearer sdmcp_abc123...
Content-Type: application/json

{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}
```

### 步骤 2：建立上下文（查授权资源）

```http
POST /mcp HTTP/1.1
Authorization: Bearer sdmcp_abc123...
Content-Type: application/json

{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_authorized_resources","arguments":{}}}
```

### 步骤 3：搜索资源

```http
POST /mcp HTTP/1.1
Authorization: Bearer sdmcp_abc123...
Content-Type: application/json

{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"search_resources","arguments":{"keyword":"订单"}}}
```

### 步骤 4：获取资源详情

```http
POST /mcp HTTP/1.1
Authorization: Bearer sdmcp_abc123...
Content-Type: application/json

{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_resource","arguments":{"resource_type":"node","resource_id":100}}}
```

### 步骤 5：发现可用动作

```http
POST /mcp HTTP/1.1
Authorization: Bearer sdmcp_abc123...
Content-Type: application/json

{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"list_actions","arguments":{"module":"node"}}}
```

### 步骤 6：执行操作

```http
POST /mcp HTTP/1.1
Authorization: Bearer sdmcp_abc123...
Content-Type: application/json

{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"invoke_action","arguments":{"action_name":"node_send_command","params":{"node_id":100,"command":"restart"}}}}
```

---

## 8. 客户端配置示例

### Claude Desktop

`claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "stardust": {
      "url": "http://stardust.example.com:6600/mcp",
      "headers": {
        "Authorization": "Bearer sdmcp_abc123def456..."
      }
    }
  }
}
```

### Trae IDE

在 Trae 的 MCP 配置中添加 HTTP transport server，URL 填 `http://localhost:6600/mcp`，Header 填 `Authorization: Bearer sdmcp_xxx`。

### Cursor

`.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "stardust": {
      "transport": {
        "type": "http",
        "url": "http://localhost:6600/mcp",
        "headers": {
          "Authorization": "Bearer sdmcp_abc123def456..."
        }
      }
    }
  }
}
```

---

## 9. 协议限制与不支持的方法

以下 MCP 协议方法**不支持**（返回 `-32601 Method not found`）：

- `resources/list` / `resources/read` / `resources/subscribe` / `resources/unsubscribe`
- `prompts/list` / `prompts/get`
- `notifications/initialized` / `notifications/progress` / `notifications/cancelled`
- `ping`（暂不支持，后续可扩展）
- `logging/setLevel`

不支持 SSE 长连接，所有响应均为单次 HTTP 响应。

---

## 10. 审计日志记录

每次 `tools/call` 调用都会在 `McpAudit` 表记录一条：

| 字段 | 示例值 |
|---|---|
| TokenId | 1 |
| TokenName | 运维助手-小明 |
| ToolName | invoke_action |
| ActionName | node_send_command |
| CallerIp | 192.168.1.100 |
| CallerUserAgent | claude-desktop/1.0.0 |
| Arguments | `{"action_name":"node_send_command","params":{"node_id":100,"command":"restart"}}` |
| Success | true |
| ErrorMessage | null |
| Duration | 152 |
| CreateTime | 2026-07-16 15:30:00 |

`list_authorized_resources`、`search_resources`、`get_resource`、`list_actions` 四个工具也会记录审计日志，但 `ActionName` 字段为 null。
