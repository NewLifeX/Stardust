# 星尘监控 Java SDK

> 正式实现：[SDK/Java](../../SDK/Java/README.md) | 接入契约：[APM-17 监控接入 API](../APM-17-监控接入API.md)
>
> 本文是面向 Java 开发者的简要说明。早期设计稿中的 `Nashorn` JSON 解析示例在 Java 15+ 已不可用，已移除；请以 `SDK/Java` 源码与 README 为准。

适用于 **Java 8 及以上**，Maven 构建，**零运行时依赖**（仅 JDK 标准库）。

## 功能特性

- ✅ 应用登录与令牌自动续期
- ✅ 心跳保活，携带应用性能指标（CPU、内存、堆、线程、GC 等）
- ✅ APM 埋点（Span）采集、聚合、GZip 上报
- ✅ 嵌套 Span 自动继承 `ParentId` / `TraceId`；W3C `traceparent` 跨服务透传
- ✅ 服务端采样参数（`Period`、`MaxSamples`、`Excludes` 等）动态生效
- ✅ 自动埋点：Servlet 容器自动注册 Filter、`StardustHttp`、JDBC 动态代理、Redis 通用包装
- 🔧 配置中心：规划中

## Maven 坐标

```xml
<dependency>
  <groupId>com.newlife</groupId>
  <artifactId>stardust</artifactId>
  <version>1.0.0</version>
</dependency>
```

Servlet 项目使用 `StardustFilter` 时额外引入（`provided`）：

```xml
<dependency>
  <groupId>javax.servlet</groupId>
  <artifactId>javax.servlet-api</artifactId>
  <version>3.1.0</version>
  <scope>provided</scope>
</dependency>
```

## 快速开始

```java
import com.newlife.stardust.Span;
import com.newlife.stardust.StardustTracer;

public class Demo {
    public static void main(String[] args) {
        // 一行接入：创建、登录、启动并注册为默认实例
        StardustTracer tracer = StardustTracer.init("http://star.example.com:6600", "MyApp", "MySecret");

        // 需要自定义名称/版本时改用 builder：
        // StardustTracer tracer = new StardustTracer(server, appId, secret)
        //         .setAppName("订单服务").setVersion("1.0.0").start();

        try (Span span = tracer.newSpan("order.create")) {
            span.setTag("orderId=123");
            doBusiness();
        } finally {
            tracer.stop();
        }
    }

    static void doBusiness() {
        // 业务代码
    }
}
```

### 手动埋点

```java
try (Span span = tracer.newSpan("payment.call")) {
    span.setTag("channel=alipay");
    try {
        callPayment();
    } catch (Exception e) {
        span.setError(e);
        throw e;
    }
}
```

`newSpan(name)` 自动使用当前线程栈顶 Span 作为父级；`newSpan(name, parentId)` 用于跨线程传递。`Span` 实现 `AutoCloseable`，推荐 try-with-resources。

### 手动刷新与心跳

```java
tracer.flush();    // 同步上报当前已完成的 Span，不抛异常
tracer.pingNow();  // 立即发送一次心跳（含性能指标）
```

## API 摘要

| 方法 | 说明 |
|------|------|
| `StardustTracer.init(server, appId, secret)` | 一行创建、启动并设为默认实例 |
| `new StardustTracer(server, appId, secret[, appName])` | 创建追踪器 |
| `setAppName` / `setVersion` / `setClientId` | 应用身份 |
| `setPeriod` / `setHeartbeatPeriod` | 上报周期 / 心跳周期（秒） |
| `setMaxSamples` / `setMaxErrors` / `setMaxTagLength` | 本地采样限制 |
| `setEnableMeter` | 上报是否携带 `Info` 性能信息 |
| `setDebug` | 输出调试日志 |
| `start()` / `stop()` | 启动（登录 + 调度）/ 停止（冲刷 + 关闭，幂等） |
| `flush()` / `pingNow()` | 同步上报 / 立即心跳 |
| `newSpan(name[, parentId])` | 创建 Span |
| `addExclude(name)` | 追加排除项，支持 `*` / `?` 通配 |

## 心跳性能指标

心跳 `POST /App/Ping?Token=...` 的请求体为 `AppInfo`：

| 字段 | 说明 |
|------|------|
| `Id` / `Name` / `Version` | 进程号、进程名、应用版本 |
| `CommandLine` / `UserName` / `MachineName` | 启动命令、系统用户、机器名 |
| `IP` | 本机非回环 IPv4 |
| `StartTime` | 进程启动时间（ISO-8601） |
| `ProcessorTime` / `CpuUsage` | 累计 CPU 时间（ms）与最近区间使用率（0~1） |
| `WorkingSet` / `HeapSize` | 已提交虚拟内存 / 堆使用量（字节） |
| `Threads` / `Handles` | 线程数 / 文件描述符数 |
| `GCCount` | 周期内 GC 次数 |
| `Time` | 采集时间（Unix 毫秒） |

`WorkerThreads`、`IOThreads`、`AvailableThreads`、`PendingItems`、`CompletedItems`、`Connections`、`Listens` 为协议兼容字段，Java SDK 固定为 0/空。

## 认证与令牌

- 所有接口均为 HTTP POST，成功响应 `{ "code": 0, "data": {...} }`，失败响应 `{ "code": ..., "message": "..." }`（HTTP 状态码仍为 200）。
- 令牌通过 `?Token=...` 传递。
- 无令牌或 5 分钟内过期时自动重新登录；上报/心跳返回 `401` 时重新登录并重试一次。
- 心跳响应返回新 `Token` 时自动替换。
- `Version` 必须随登录与上报发送，服务端才会下发 `Excludes` / `EnableMeter`。

## 自动埋点

自动埋点遵循星尘统一语义约定，所有采集代码都捕获自身异常，不影响业务：

| 类型 | 接入方式 | Span 名称 |
|------|----------|-----------|
| Web 入口 | Servlet 3.0+ 容器自动注册 `StardustFilter` | `METHOD requestURI` |
| 出站 HTTP | `StardustHttp.open/execute`（JDK 无全局 Hook） | `METHOD host/path` |
| 数据库 | `StardustJdbc.wrap(dataSource)` 一行接入 | `db:{system}:{action}:{tables}` |
| Redis | `StardustRedis.call(...)` 通用包装 | `redis:{COMMAND}` |

启用自动埋点的 `StardustTracer` 在构造时自动成为默认实例，`StardustFilter` 会解析上游 `traceparent`；通过 `setAutoInstrument(false)` 整体关闭自动埋点（手工 `newSpan` 不受影响）。

## Servlet / Spring 集成

```java
StardustTracer tracer = new StardustTracer("http://star.example.com:6600", "MyApp", "MySecret")
        .setVersion("1.0.0")
        .start();

// 容器可通过 META-INF/services 自动注册；也可显式注册：
StardustFilter filter = new StardustFilter(tracer);
```

Filter 为每个请求创建 `METHOD requestURI` Span，状态码 ≥ 500 或异常时标记错误；`tracer` 为空时直接放行。

Spring Boot（`javax.servlet`）可通过 `FilterRegistrationBean<StardustFilter>` 注册，并在 `@PreDestroy` 中调用 `tracer.stop()`。

## Java 8 兼容性

- 不使用 `ProcessHandle`，通过 `RuntimeMXBean.getName()` 获取进程号。
- 使用 `HttpURLConnection`，不使用 Java 11 的 `java.net.http`。
- 不依赖 Nashorn/Gson/Jackson，内置轻量 JSON 解析器。
- 编译目标 `source/target = 8`。

## 运行测试

```bash
cd SDK/Java
mvn -B test
```

测试使用 JDK 自带 `com.sun.net.httpserver.HttpServer` 模拟服务端，覆盖登录、心跳性能、上报、GZip、401 重登录、动态采样、停止，以及 `traceparent`、Servlet 自动注册、JDBC 代理与 Redis 包装。

## 相关资源

- [SDK/Java/README.md](../../SDK/Java/README.md)
- [APM-17 监控接入 API](../APM-17-监控接入API.md)
- [SDK 能力矩阵](../../SDK/README.md)