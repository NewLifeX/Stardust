# SRV — App 服务端架构

> **版本**：v3.8 | **日期**：2026-07-26
> **覆盖模块**：AppController + RegistryService（应用接入服务端）
> **关联文档**：[Node服务端架构](SRV-Node服务端架构.md) · [Remoting服务端架构](SRV-Remoting架构.md)
> **定位**：本文档用于梳理 AppClient 应用从接入到下线的服务端全链路串联关系，非 API 参考手册。

---

## 1. 分层架构总览

```
AppController                RegistryService               CMD 层
（HTTP 入口）                （业务逻辑）                   （长连接下发）

/App/Login      ──→  RegistryService.Login       ──→  SessionManager
/App/Ping       ──→  RegistryService.Ping             ├─ AppSessionManager
/App/Logout     ──→  RegistryService.Logout           └─ AppCommandSession
/App/Notify     ──→  RegistryService.SetOnline            (WebSocket 全双工)
/App/SendCmd    ──→  RegistryService.SendCommand
/App/PostEvent  ──→  RegistryService.PostEvents
```

**关键关系**：
- `AppController` 继承 `BaseController`，接入 `BaseController.OnActionExecuting` 管线
- `RegistryService` 继承 `DefaultDeviceService<App, AppOnline>`，但与 NodeService 有显著差异
- `AppSessionManager` 继承 `SessionManager`，Topic=`AppCommands`，话题隔离
- `AppOnlineService` 独立维护应用在线记录的内存缓存（不经过 DefaultDeviceService 缓存层）

---

## 2. 控制器管线（每请求）

与 [Remoting 服务端架构](SRV-Remoting架构.md) 第 2 节一致。每个请求到达时：

```
OnActionExecuting
  ├─ 从对象池取 DeviceContext
  ├─ 提取 Token → DecodeToken → Jwt.Subject = app.Name
  ├─ GetDevice(name) → App.FindByName → ctx.Device = app
  └─ GetOnline(ctx) → AppOnline.FindBySessionID → ctx.Online = online
```

App 特有：`OnAuthorize` 中额外执行 IP 黑白名单校验（`app.MatchIp` + `project.MatchIp`），而非硬件指纹。

---

## 3. Node vs App 关键差异总览

| 维度 | NodeService | RegistryService |
|------|-------------|-----------------|
| **设备实体** | `Node` / `NodeOnline` | `App` / `AppOnline` |
| **SessionId 规则** | `ctx.Code`（节点编码 `359AB559`） | `ctx.ClientId`（应用实例 ID） |
| **GetOnline** | 按 Code 查 | 按 ClientId 查 |
| **鉴权方式** | 硬件指纹 `CheckNode` + 密钥 | IP 黑白名单 + 密钥 |
| **注册方式** | `BuildCode`（UUID散列→编码） | 应用名直接注册 |
| **登录 OnLogin** | 不自行持久化（交基类） | 自行调 `online.Update()`（需关联节点） |
| **心跳** | 更新 Node 表（Frameworks等）+ NodeOnline | 仅更新 AppOnline |
| **注销** | 检查下线告警 | 清理 AppOnlineService 内存缓存 |
| **额外功能** | DDNS、升级推送、dotNet安装 | **服务注册发现、健康检查** |
| **集群角色** | 被管理节点 | 服务提供者/消费者 |

---

## 4. 核心业务流程

### 4.1 登录 → 注册

```
POST /App/Login (Body: AppModel JSON, [AllowAnonymous])
    │
    ▼
AppController.Login(model)
    ├─ App.FindByName(model.AppId) → ctx.Device = app
    │
    └─ registryService.Login(ctx, request, "Http")
        │
        ├─ Authorize(ctx, request)                  ← RegistryService 重写
        │   ├─ app.MatchIp(ip) — IP 黑白名单
        │   ├─ app.Project.MatchIp(ip) — 项目 IP 过滤
        │   ├─ !app.Enable → 拒绝
        │   └─ 密钥验证（明文 → passwordProvider）
        │
        ├─ Register(ctx, request)                   ← 动态注册
        │   ├─ App.GetOrAdd(name) → 新应用
        │   ├─ 生成 Secret（Rand.NextString(16)）
        │   └─ Enable = AppAutoRegister 配置
        │
        ├─ OnLogin(ctx, request)                    ← RegistryService 重写
        │   ├─ app.DisplayName/Compile/Version 更新
        │   ├─ app.LastLogin/LastIP → app.Update()
        │   ├─ ctx.Online = GetOnline(ctx) ?? CreateOnline(ctx)
        │   ├─ online.NodeId = Node.FindByCode(inf.NodeCode)?.Id
        │   │   └─ 回退：Node.SearchByIP(inf.IP).FirstOrDefault()
        │   ├─ online.Version/Compile 更新
        │   └─ online.Update()                      ← 自行持久化（含节点关联）
        │
        ├─ (device as IEntity)?.Update()            ← 基类 Login 持久化设备
        └─ (ctx.Online as IEntity)?.Update()        ← 基类 Login 持久化在线
```

> **差异**：`RegistryService.OnLogin` 自行调用 `online.Update()`，因为需要关联节点信息（`online.NodeId`）。`NodeService.OnLogin` 不自行 Update，交给基类统一处理。

### 4.2 心跳流程

```
POST /App/Ping (Body: AppInfo JSON)
    │
    ▼
AppController.Ping(inf)
    └─ registryService.Ping(ctx, inf, null)
        └─ base.OnPing(ctx, request)                ← DefaultDeviceService
            └─ online2.Save(request, ctx)
                ├─ Fill(appInfo) + Save
                └─ AppMeter.WriteData（性能数据）
        │
        └─ AcquireCommands(ctx)                     ← 查询待下发命令
```

与 Node 心跳的差异：
- 不更新 App 表（Node 需要更新 Frameworks）
- 额外写 `AppMeter` 性能数据
- `Ping` 请求体中包含服务注册信息（`Service` 数组）

### 4.3 注销流程

```
GET /App/Logout?reason=xxx
    │
    ▼
registryService.Logout(ctx, reason, "Http")
    ├─ base.Logout(ctx, reason, "Http")              ← DefaultDeviceService
    │   └─ SettleOnline + LongLink=false → Update
    │
    └─ _appOnline.RemoveOnline(ctx.ClientId)         ← AppOnlineService 内存缓存清理
```

---

## 5. 服务注册与发现（App 特有核心功能）

### 5.1 服务注册

App 心跳时携带 `Service[]` 数组，每个元素描述一个提供的服务：

```
Ping → RegisterService(app, service, model, online, ip)
    │
    ├─ 查找已有 AppService（按 ServiceId + Client 匹配）
    ├─ Singleton 模式：Client = 本地 IP（去进程 ID）
    │
    ├─ 新建/更新：
    │   ├─ Enable = app.AutoActive
    │   ├─ Scope = AppRule.CheckScope（作用域匹配）
    │   ├─ Tag / Version / Address 更新
    │   ├─ 地址处理：
    │   │   ├─ Extranet → 来源 IP
    │   │   ├─ 其他 → model.IP → localIp → 来源 IP
    │   │   └─ "://*" → 替换为实际 IP
    │   └─ svc.Save()
    │
    ├─ service.Providers = services.Count → Save
    └─ 新增实例 → HealthCheck(svc) 异步健康检测
```

### 5.2 服务发现

```
App Ping → Service[] 中的 Consume 字段
    └─ ResolveService(service, model, scope)
        ├─ AppService.FindAllByService(service.Id)
        ├─ 过滤：Enable + Healthy + Match(minVersion, scope, tags)
        ├─ 每个匹配实例 → ServiceModel { Address, Version, ... }
        └─ 返回 ServiceModel[]
```

### 5.3 服务注销

```
App Logout → UnregisterService(app, service, model, ip)
    ├─ 按 ServiceId + Client 查找 AppService
    ├─ svc.Enable = false → svc.Update()
    └─ service.Providers 更新 → Save
```

### 5.4 健康检查

```
RegisterService 中新实例 → HealthCheck(svc)
    ├─ 从 service.HealthAddress 拼装健康检查 URL
    ├─ HttpClient.GetStringAsync(url)
    ├─ 成功 → svc.Healthy = true, svc.CheckResult = response
    └─ 失败 → svc.Healthy = false, svc.CheckResult = exception
        └─ svc.CheckTimes++; svc.LastCheck = now → Update
```

### 5.5 服务变更通知

```
RegisterService/UnregisterService → NotifyConsumers(service, command)
    ├─ AppConsume.FindAllByService → 找出所有订阅者
    ├─ 对每个订阅应用 → SendCommand(app, "registry/register", model)
    └─ 消费者收到命令 → 重新 ResolveService → 刷新服务地址列表
```

---

## 6. 指令下发管线

```
外部平台 → POST /App/SendCommand (CommandInModel)
    │
    ▼ [AllowAnonymous] 但需应用令牌
AppController.SendCommand(model)
    └─ registryService.SendCommand(ctx, model)
        ├─ App.FindByName(model.Code) → 验证应用
        ├─ DecodeToken → Jwt → App.FindByName → 验证权限
        ├─ AllowControlNodes 权限检查
        └─ 回退到 SendCommand(app, model, user) 内部方法
            ├─ AppCommand 实体（就绪状态）→ Insert
            └─ sessionManager.PublishAsync(code, cmd, timeout)
                │
                ▼ 事件总线广播
            AppCommandSession 收到
                └─ WebSocket.SendAsync(commandJson)
                    │
                    ▼ 应用执行
            POST /App/CommandReply (HTTP)
                └─ registryService.CommandReply(ctx, model)
                    ├─ AppCommand.Status/Result 更新
                    └─ PublishResponseAsync → 广播回发起方
```

---

## 7. 会话与令牌

### 令牌颁发

App 登录成功后由 Controller 自行颁发令牌（不同于 Node 由基类颁发）：

```
AppController.Login
    ├─ registryService.Login(...) → 鉴权成功
    ├─ app.Enable → TokenService.IssueToken(app.Name, clientId)
    └─ LoginResponse { Token, Expire, Code?, Secret? }
```

### WebSocket 通知

与 Node 同构，`AppController.Notify()` → `HandleNotify(socket)`：

```
AppCommandSession
    ├─ Code = $"{app.Name}@{ctx.ClientId}"
    ├─ SetOnline = online => registryService.SetOnline(ctx, online)
    └─ WaitAsync() ← 长期持有
```

WebSocket 升级请求同样走完整 HTTP 管线（`OnAuthorize` → `ctx.Online` 已设置）。

---

## 8. RegistryService 重写清单

| 方法 | 用途 | 核心逻辑 |
|------|------|---------|
| `Authorize` | 鉴权 | IP 黑白名单 `app.MatchIp` + 密钥验证 |
| `Register` | 自动注册 | `App.GetOrAdd(name)` + 生成 Secret |
| `OnLogin` | 登录处理 | `app.Update`、关联 NodeId、`online.Update` 自行持久化 |
| `Logout` | 注销 | 基类结算 + `appOnline.RemoveOnline` 清理缓存 |
| `QueryDevice` | 查设备 | `App.FindByName` |
| `QueryOnline` | 查在线 | `AppOnline.FindBySessionId` |
| `GetSessionId` | 会话标识 | 返回 `ctx.ClientId`（应用实例 ID） |
| `AcquireCommands` | 查询命令 | 缓存 1000 条 + AppId 匹配过滤 |
| `CommandReply` | 命令响应 | AppCommand.Status/Result 更新 |
| `CreateEvent` | 事件实体 | 创建 AppHistory 并关联 NodeId/Client |
| `WriteHistory` | 写历史 | 创建 AppHistory + NodeId/Client 关联 |

### 业务方法（非虚方法重写）

| 方法 | 用途 |
|------|------|
| `RegisterService` | 服务注册 → AppService 新建/更新 |
| `UnregisterService` | 服务注销 → svc.Enable=false |
| `ResolveService` | 服务发现 → 过滤返回可用实例 |
| `HealthCheck` | 健康检查 → HTTP GET health URL |
| `NotifyConsumers` | 服务变更通知 → SendCommand 给所有订阅者 |
| `SendCommand(App, ...)` | 内部命令下发 → NodeCommand.Insert + PublishAsync |
