# SRV — Node 服务端架构

> **版本**：v3.8 | **日期**：2026-07-26
> **覆盖模块**：NodeController + NodeService（节点接入服务端）
> **关联文档**：[App服务端架构](SRV-App服务端架构.md) · [Remoting服务端架构](SRV-Remoting架构.md)
> **定位**：本文档用于梳理 StarAgent 节点从接入到下线的服务端全链路串联关系，非 API 参考手册。

---

## 1. 分层架构总览

```
NodeController                NodeService                  CMD 层
（HTTP 入口）                 （业务逻辑）                  （长连接下发）

/Node/Login     ──→  NodeService.Login       ──→  SessionManager
/Node/Ping      ──→  NodeService.Ping             ├─ NodeSessionManager
/Node/Logout    ──→  NodeService.Logout           └─ NodeCommandSession
/Node/Notify    ──→  NodeService.SetOnline            (WebSocket 全双工)
/Node/Upgrade   ──→  NodeService.Upgrade
/Node/SendCmd   ──→  NodeService.SendCommand
/Node/PostEvent ──→  NodeService.PostEvents
```

**关键关系**：
- `NodeController` 继承 `BaseController`，接入 `BaseController.OnActionExecuting` 管线（令牌解码、Device/Online 预加载）
- `NodeService` 继承 `DefaultDeviceService<Node, NodeOnline>`，重写 `OnLogin/OnPing/Authorize/Register/SetOnline` 等方法实现节点特有逻辑
- `NodeSessionManager` 继承 `SessionManager`，Topic=`NodeCommands`，话题隔离
- WebSocket 升级请求也走完整 HTTP 管线，`ctx.Device` 和 `ctx.Online` 在 `HandleNotify` 中均已可用

---

## 2. 控制器管线（每请求）

与 [Remoting 服务端架构](SRV-Remoting架构.md) 第 2 节一致。每个请求到达时：

```
OnActionExecuting
  ├─ 从对象池取 DeviceContext
  ├─ 提取 Token → DecodeToken → Jwt.Subject = node.Code (如 "359AB559")
  ├─ GetDevice(code) → Node.FindByCode → ctx.Device = node
  └─ GetOnline(ctx) → NodeOnline.FindBySessionID → ctx.Online = olt
```

Node 特有：`OnAuthorize` 重写中额外执行 `CheckNode`（硬件指纹校验：UUID/Guid/Serial/Mac/Disk）。

---

## 3. 核心业务流程

### 3.1 登录 → 注册 → LoginTime 持久化

```
POST /Node/Login (Body: LoginInfo JSON, [AllowAnonymous])
    │
    ▼
NodeController.Login(data)
    ├─ 反序列化 LoginInfo（兼容旧版 XCoder 请求体）
    ├─ Node.FindByCode(code) → ctx.Device = node
    │
    └─ nodeService.Login(ctx, request, "Http")
        │
        ├─ Authorize(ctx, request)                  ← NodeService 重写
        │   ├─ CheckNode 硬件指纹校验
        │   │   ├─ UUID/Guid/Serial/Mac/Disk 逐个比对
        │   │   └─ 匹配度 < NodeCodeLevel → 拒绝
        │   ├─ 密钥验证（明文 → MD5+盐值）
        │   └─ 失败 → Register(ctx, request)
        │       └─ AutoRegister
        │           ├─ BuildCode（NodeCodeFormula → CRC/MD5 → 8位hex）
        │           ├─ QueryByInfo（硬件指纹匹配旧节点）
        │           ├─ 生成 Secret（Rand.NextString(16)）
        │           └─ node.Save()
        │
        ├─ OnLogin(ctx, request)                    ← NodeService 重写
        │   ├─ node.ProductCode = inf.ProductCode
        │   ├─ node.FixNameByRule()
        │   ├─ node.Login(inf.Node, ip)
        │   │   ├─ node.Fill(di) → OS/Version/UUID/Macs/...
        │   │   ├─ node.Logins++; LastLogin/LastActive = now
        │   │   ├─ node.FixArea()（IP 转地区）
        │   │   └─ node.Save()
        │   ├─ ctx.Online = GetOnline(ctx) ?? CreateOnline(ctx)
        │   ├─ olt.LoginTime = DateTime.Now          ← 内存设置
        │   ├─ olt.Fill(inf.Node)                    ← IP/Gateway/Dns/Memory/...
        │   ├─ WriteHistory("节点鉴权", true)
        │   ├─ CheckOnline(node)                     ← 恢复上线告警
        │   └─ CheckNodeIPChange(node, ip, oldIp)    ← DDNS 检测
        │
        ├─ (device as IEntity)?.Update()             ← 基类 Login 持久化设备
        └─ (ctx.Online as IEntity)?.Update()         ← 基类 Login 持久化 LoginTime
            │
            ▼
        LoginResponse { Token, Expire, Name, ServerTime }
```

> **约定**：`OnLogin` 仅设置属性不持久化。LoginTime 和 Fill 数据由基类 `Login` 在 return 前统一 `Update`。

### 3.2 心跳流程

#### HTTP Ping（每 60 秒）

```
POST /Node/Ping (Body: PingInfo JSON)
    │
    ▼ BaseController 管线（令牌验证 → ctx.Device/ctx.Online 已加载）
    │
NodeController.Ping(inf)
    └─ nodeService.Ping(ctx, inf, response)
        │
        ├─ OnPing(ctx, request)                      ← NodeService 重写
        │   ├─ node.IP/Gateway 更新
        │   ├─ node.UpdateIP/FixArea/FixNameByRule
        │   ├─ node.Frameworks 更新（取最大版本）
        │   ├─ node.Update()                         ← 每 10 分钟
        │   └─ base.OnPing(ctx, request)             ← DefaultDeviceService
        │       └─ ctx.Online ?? CreateOnline(ctx)   ← Online 已有 → 跳过
        │           └─ online2.Save(request, ctx)
        │               ├─ Fill(ping) → CpuRate/Memory/Disk/Process/...
        │               ├─ CreateData(ping) → NodeData.Insert
        │               └─ Save() → NodeOnline.Update
        │
        ├─ AcquireCommands(ctx)                      ← 检查待下发命令
        └─ 令牌 10 分钟内到期 → IssueToken 续期
```

#### WebSocket 心跳（长连接，每 10 秒）

```
GET /Node/Notify (Upgrade: websocket, Authorization: Bearer xxx)
    │
    ▼ BaseController 管线（WS 升级也是 HTTP 请求 → OnAuthorize → ctx.Device/ctx.Online 已设）
    │
NodeController.Notify()
    └─ HandleNotify(socket)
        ├─ new NodeCommandSession(socket)
        │   ├─ Code = node.Code
        │   ├─ SetOnline = online => nodeService.SetOnline(ctx, online)
        │   └─ sessionManager.Add(session)
        └─ WaitAsync() ← 长期持有
            │
            ├─ 连接建立 → SetOnline(ctx, true)
            │   └─ olt.LongLink = true → olt.Update()
            │
            ├─ 心跳消息 → 复用 ctx.Online → 不触发 CreateOnline
            │
            ├─ 命令响应 → CommandReply(ctx, model)
            │
            └─ 断开 → SetOnline(ctx, false)
                ├─ 检查 session.Active（防旧会话覆盖新会话）
                └─ olt.LongLink = false → olt.Update()
```

### 3.3 注销 → 在线时长结算

```
GET /Node/Logout?reason=模拟运行停止
    │
    ▼
nodeService.Logout(ctx, reason, "Http")
    └─ base.Logout(ctx, reason, source)              ← DefaultDeviceService
        ├─ WriteHistory("Http设备下线", msg)
        │   └─ msg = "模拟运行停止 [X5/359AB559]登录于xxx，最后活跃于xxx"
        │
        ├─ SettleOnline(online, device)
        │   ├─ LoginTime > 2000 → OnSettleOnline(online, device)
        │   │   └─ delta = UpdateTime - LoginTime
        │   │   └─ node.OnlineTime += delta → node.Update()
        │   └─ LoginTime = DateTime.MinValue          ← 防重复结算守卫
        │
        ├─ LongLink = false
        └─ entity.Update()
            │
            ▼
        NodeOnlineService.CheckOffline(node, "注销")
            └─ AlarmOnOffline=true → 发送下线告警
```

### 3.4 升级检查

```
GET /Node/Upgrade?channel=Release
    │
    ▼
nodeService.Upgrade(ctx, channel)
    ├─ ProductRelease.GetValids(channel)
    │   └─ release.MatchPackage(node) → ProductPackage
    │       ├─ 匹配 OS/Kind/Runtime → 返回安装包
    │       └─ node.LastVersion 去重
    │
    └─ 回退：NodeVersion 旧逻辑
        └─ node.Match(version) → 返回版本
```

### 3.5 dotNet 运行时推送（节点特有）

```
登录或心跳中触发：
CheckDotNet(node, baseUri, ip)
    ├─ DotNetPackage.Match(node)                     ← 匹配 OS/Kind/Runtime
    ├─ OS 兼容性检查（Ubuntu18 不支持 .NET10）
    ├─ GLIBC 版本检查（.NET10 要求 glibc 2.27+）
    └─ 推送 "framework/install" 命令 → SendCommand
```

### 3.6 DDNS 动态域名解析（节点特有）

```
登录/心跳中检查 IP 变化：
CheckNodeIPChange(node, ip, oldIp)
    ├─ 查找该节点的域名记录（DomainProvider + DomainRecord）
    ├─ IP 变化 → 调用阿里云/腾讯云/UCloud DNS API 更新 A 记录
    └─ 写历史 "更新DNS"
```

---

## 4. 指令下发管线

```
外部平台 → POST /Node/SendCommand (CommandInModel)
    │
    ▼ [AllowAnonymous] 但需应用令牌
NodeController.SendCommand(model)
    └─ nodeService.SendCommand(ctx, model)
        ├─ Node.FindByCode(model.Code) → 验证节点
        ├─ App.FindByName(Jwt.Subject) → 验证应用权限
        ├─ NodeCommand 实体（就绪状态）→ Insert
        └─ sessionManager.PublishAsync(code, cmd, timeout)
            │
            ▼ 事件总线广播（跨实例）
        NodeCommandSession 收到
            └─ WebSocket.SendAsync(commandJson)
                │
                ▼ 设备执行
        POST /Node/CommandReply (HTTP)
            └─ nodeService.CommandReply(ctx, model)
                ├─ NodeCommand.Status/Result 更新
                └─ PublishResponseAsync → 广播回发起方
```

---

## 5. 在线会话生命周期

与 [Remoting 服务端架构](SRV-Remoting架构.md) 第 6 节一致，Node 特有的扩展：

| 阶段 | Node 特有处理 |
|------|-------------|
| 创建 | `CheckNode` 硬件指纹、`FixArea` IP 转地区、`FixNameByRule` 命名规则 |
| 心跳更新 | 更新 `node.Frameworks`（运行时版本）、`NodeData.Insert`（性能历史） |
| 结算 | 累加 `node.OnlineTime`，用于统计节点总在线时长 |
| 销毁 | 基类 `RemoveOnline` 直接 `SettleOnline + Delete`，无需额外缓存清理 |

---

## 6. 超时清理

```
NodeOnlineService.CheckNodeOnline（每 30 秒定时器）
    ├─ NodeOnline.ClearExpire(sessionTimeout)
    ├─ 找到过期记录 → WriteHistory("超时下线")
    ├─ nodeService.RemoveOnline(new DeviceContext { Device=node, Online=online })
    │   └─ DefaultDeviceService.RemoveOnline
    │       ├─ SettleOnline → OnSettleOnline → node.OnlineTime += delta
    │       └─ entity.Delete()
    └─ CheckOffline(node) → 下线告警
```

---

## 7. NodeService 重写清单

| 方法 | 用途 | 核心逻辑 |
|------|------|---------|
| `Authorize` | 鉴权 | 硬件指纹 `CheckNode` + 密钥验证 |
| `Register` | 自动注册 | `BuildCode` 生成编码 + `QueryByInfo` 匹配旧节点 |
| `OnLogin` | 登录处理 | `node.Login`、设 `LoginTime`、`olt.Fill`、D DNS |
| `OnPing` | 心跳处理 | 更新 `node.Frameworks`、`node.Update`、DDNS |
| `OnSettleOnline` | 在线结算 | 累加 `node.OnlineTime` |
| `SetOnline` | WS 上下线 | 检查 `session.Active` 防旧会话覆盖 |
| `CreateOnline` | 创建在线 | 补充 `Token` 字段 |
| `AcquireCommands` | 查询命令 | 缓存 1000 条 + 节点匹配过滤 |
| `Upgrade` | 升级检查 | ProductRelease + NodeVersion 双路径 |
| `QueryDevice` | 查设备 | `Node.FindByCode` |
| `QueryOnline` | 查在线 | `NodeOnline.FindBySessionID` |
| `GetSessionId` | 会话标识 | 返回 `ctx.Code`（节点编码） |
