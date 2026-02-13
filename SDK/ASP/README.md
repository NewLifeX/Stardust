# 星尘监控 ASP (Classic ASP/VBScript) SDK

适用于经典 ASP (VBScript) 环境，提供星尘 APM 监控的接入能力。

## 功能特性

- ✅ **登录认证**：应用标识 + 密钥登录，获取 JWT 令牌
- ✅ **心跳保活**：定期 Ping，保持在线状态，自动刷新令牌
- ✅ **链路追踪**：页面级别的 APM 埋点
- ✅ **数据上报**：采集调用链数据并上报至星尘服务端
- ✅ **零依赖**：仅使用 Windows 内置的 MSXML2.ServerXMLHTTP 组件

## 环境要求

- IIS + Classic ASP
- MSXML2.ServerXMLHTTP 组件（Windows 默认可用）

## 快速开始

### 最简心跳接入

只需登录和心跳两个接口，即可接入星尘平台：

```asp
<!--#include file="StardustTracer.asp"-->
<%
Dim tracer
Set tracer = New StardustTracer
tracer.Init "http://star.example.com:6600", "MyASPApp", "MySecret"

' 登录
tracer.Login

' 心跳
tracer.Ping

Set tracer = Nothing
%>
```

### APM 监控

```asp
<!--#include file="StardustTracer.asp"-->
<%
Dim tracer
Set tracer = New StardustTracer
tracer.Init "http://star.example.com:6600", "MyASPApp", "MySecret"
tracer.Login

' 创建追踪片段
Dim span
Set span = tracer.NewSpan("GET /index.asp")
span.Tag = Request.ServerVariables("URL")

' 业务逻辑
DoSomething

span.Finish

' 请求结束时上报
tracer.Flush

Set tracer = Nothing
%>
```

## 文件说明

```
SDK/ASP/
├── src/
│   └── StardustTracer.asp      # SDK 核心文件（Include 引用）
├── examples/
│   ├── basic_ping.asp          # 基础心跳示例
│   ├── apm_basic.asp           # APM 监控示例
│   └── global.asa              # 应用程序级初始化示例
└── README.md                   # 本文件
```

## 核心 API

### StardustTracer 类

| 方法 | 说明 |
|------|------|
| `Init(server, appId, secret)` | 初始化追踪器 |
| `Login()` | 登录获取令牌，返回 Boolean |
| `Ping()` | 心跳保活，刷新令牌 |
| `NewSpan(name)` | 创建追踪片段 |
| `Flush()` | 上报数据到监控中心 |

### StardustSpan 类

| 属性/方法 | 说明 |
|-----------|------|
| `Tag` | 标签信息（如 URL、参数等） |
| `Error` | 错误信息 |
| `SetError(msg)` | 设置错误信息 |
| `Finish()` | 完成追踪片段 |

## 最佳实践

### Token 缓存

将 Token 缓存到 `Application` 对象中，避免每个请求都重新登录：

```asp
<!--#include file="StardustTracer.asp"-->
<%
Dim tracer
Set tracer = New StardustTracer
tracer.Init "http://star.example.com:6600", "MyASPApp", "MySecret"

Dim cachedToken
cachedToken = Application("StardustToken")
If Len(cachedToken) = 0 Then
    tracer.Login
    Application.Lock
    Application("StardustToken") = tracer.Token
    Application.UnLock
End If
%>
```

### 错误处理

```asp
<%
Dim span
Set span = tracer.NewSpan("数据库操作")

On Error Resume Next
' ... 可能出错的操作 ...
If Err.Number <> 0 Then
    span.SetError Err.Description
    Err.Clear
End If
On Error GoTo 0

span.Finish
%>
```

## 注意事项

1. Classic ASP 为同步请求-响应模式，每次请求结束时必须调用 `Flush()` 上报数据
2. 建议将 Token 缓存到 `Application` 对象中以减少登录调用
3. `MSXML2.ServerXMLHTTP` 的超时设置已内置（连接 5 秒，数据 10 秒）

## 相关链接

- [星尘项目主页](https://github.com/NewLifeX/Stardust)
- [接入 API 文档](/Doc/星尘监控接入Api文档.md)
- [ASP SDK 详细文档](/Doc/SDK/stardust-sdk-asp.md)

## 许可证

MIT License
