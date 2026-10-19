# Stardust Java SDK

星尘（Stardust）APM 监控 Java SDK，适用于 **Java 8 及以上**，**零运行时依赖**（仅使用 JDK 标准库）。

功能：

- 应用登录与令牌自动续期（`Token` 查询参数）
- 心跳保活，携带应用性能指标（CPU、内存、线程、GC 等）
- APM 埋点（Span）采集、聚合与批量上报
- 嵌套 Span 自动继承 `ParentId` 与 `TraceId`
- 大报文自动 GZip 压缩（走 `/Trace/ReportRaw`）
- 服务端采样参数（周期、样本数、排除项等）动态生效
- 可选 Servlet Filter（需 `javax.servlet-api`，`provided` 作用域）

---

## Maven 坐标

```xml
<dependency>
  <groupId>com.newlife</groupId>
  <artifactId>stardust-sdk</artifactId>
  <version>1.0.0</version>
</dependency>
```

Servlet 项目如需使用 `StardustFilter`：

```xml
<dependency>
  <groupId>javax.servlet</groupId>
  <artifactId>javax.servlet-api</artifactId>
  <version>3.1.0</version>
  <scope>provided</scope>
</dependency>
```

---

## 快速开始

```java
import com.newlife.stardust.Span;
import com.newlife.stardust.StardustTracer;

public class Demo {
    public static void main(String[] args) {
        StardustTracer tracer = new StardustTracer("http://star.example.com:6600", "MyApp", "MySecret")
                .setAppName("订单服务")
                .setVersion("1.0.0")
                .setHeartbeatPeriod(60)
                .setPeriod(60)
                .start();

        try {
            // 自动关闭（try-with-resources），嵌套 Span 自动共享 TraceId
            try (Span span = tracer.newSpan("order.create")) {
                span.setTag("orderId=123");
                doBusiness();
            }
        } finally {
            tracer.stop();
        }
    }

    private static void doBusiness() {
        // 业务代码
    }
}
```

### 手动创建与父子 Span

```java
Span root = tracer.newSpan("order.create");
try {
    Span child = tracer.newSpan("db.insert", root.getId());
    try {
        // 业务代码
    } finally {
        child.close();
    }
} finally {
    root.close();
}
```

`newSpan(name)` 会自动使用当前线程栈顶的 Span 作为父级；`newSpan(name, parentId)` 用于跨线程或手工传递父级 ID。

### 异常标记

```java
try (Span span = tracer.newSpan("payment.call")) {
    span.setTag("channel=alipay");
    try {
        callPayment();
    } catch (Exception e) {
        span.setError(e);   // 计入 Errors 与 ErrorSamples
        throw e;
    }
}
```

### 手动刷新与心跳

```java
tracer.flush();    // 同步上报当前已完成的 Span，不抛异常
tracer.pingNow();  // 立即发送一次心跳（含性能指标）
```

---

## Spring / Servlet 集成

### Servlet Filter

```java
StardustTracer tracer = new StardustTracer("http://star.example.com:6600", "MyApp", "MySecret")
        .setAppName("MyApp")
        .setVersion("1.0.0")
        .start();

StardustFilter filter = new StardustFilter(tracer);
```

Filter 会为每个请求创建名称为 `METHOD requestURI` 的 Span，查询字符串作为 Tag；
HTTP 状态码 ≥ 500 或抛出异常时标记为错误。`tracer` 为 `null` 时直接放行。

### Spring Boot（javax.servlet）

```java
@Configuration
public class StardustConfig {

    private StardustTracer tracer;

    @PostConstruct
    public void init() {
        tracer = new StardustTracer("http://star.example.com:6600", "MyApp", "MySecret")
                .setAppName("MyApp")
                .setVersion("1.0.0")
                .start();
    }

    @Bean
    public FilterRegistrationBean<StardustFilter> stardustFilter() {
        FilterRegistrationBean<StardustFilter> bean = new FilterRegistrationBean<StardustFilter>();
        bean.setFilter(new StardustFilter(tracer));
        bean.addUrlPatterns("/*");
        bean.setOrder(Ordered.HIGHEST_PRECEDENCE);
        return bean;
    }

    @PreDestroy
    public void destroy() {
        if (tracer != null) tracer.stop();
    }
}
```

> 不使用 Filter 时，也可以只在 Service/DAO 等关键位置手工埋点。

---

## 心跳性能指标

心跳（`POST /App/Ping`）请求体为服务端 `AppInfo` 结构，主要字段如下：

| 字段 | 说明 | 采集方式 |
|------|------|----------|
| `Id` | 进程 ID | `RuntimeMXBean.getName()` 中 `@` 前部分 |
| `Name` | 进程名 | `sun.java.command` 首个词，缺省为应用名 |
| `Version` | 应用版本 | `setVersion()`，默认 `1.0.0` |
| `CommandLine` | 启动命令 | `sun.java.command` |
| `UserName` | 操作系统用户 | `user.name` |
| `MachineName` | 机器名 | `InetAddress.getLocalHost().getHostName()`，失败取 `COMPUTERNAME` |
| `IP` | 本机 IPv4 | 首个非回环 IPv4，失败取 `127.0.0.1` |
| `StartTime` | 进程启动时间 | ISO-8601（`yyyy-MM-dd'T'HH:mm:ss.SSSXXX`） |
| `ProcessorTime` | 进程 CPU 时间（ms） | `getProcessCpuTime() / 1_000_000` |
| `CpuUsage` | CPU 使用率（0..1） | 两次心跳的 CPU 时间差 / 墙钟时间差，失败回退 `getProcessCpuLoad()` |
| `WorkingSet` | 工作集（字节） | 已提交虚拟内存，失败取堆 + 非堆 |
| `HeapSize` | 堆使用量（字节） | `MemoryMXBean` 堆已用 |
| `Threads` | 线程数 | `ThreadMXBean.getThreadCount()` |
| `Handles` | 句柄/文件描述符数 | Unix 平台取 `getOpenFileDescriptorCount()`，否则 0 |
| `GCCount` | 周期内 GC 次数 | `GarbageCollectorMXBean` 计数增量 |
| `Connections` | 连接数 | 固定 0（JVM 无跨平台采集） |
| `Listens` | 监听端口 | 固定空字符串 |
| `Time` | 采集时间（Unix ms） | `System.currentTimeMillis()` |

`WorkerThreads`、`IOThreads`、`AvailableThreads`、`PendingItems`、`CompletedItems` 为服务端协议兼容字段，Java SDK 固定为 0。

---

## 认证与令牌

所有接口均为 HTTP POST，成功响应：

```json
{ "code": 0, "data": { ... } }
```

失败响应（HTTP 状态码仍为 200）：

```json
{ "code": 400, "message": "..." }
```

令牌通过 URL 查询参数传递：`?Token=...`。

SDK 的令牌生命周期：

1. 无令牌或令牌将在 **5 分钟内过期**时自动重新登录；
2. 上报/心跳返回 `401`（或 HTTP 401）时，重新登录并**重试一次**；
3. 心跳响应若返回新 `Token`，自动替换；若能解析 JWT `exp`，同步更新过期时间。

登录请求体：

```json
{
  "AppId": "MyApp",
  "Secret": "MySecret",
  "ClientId": "192.168.1.10@12345",
  "AppName": "订单服务",
  "Version": "1.0.0",
  "IP": "192.168.1.10"
}
```

`Version` 必须随登录与上报一起发送，服务端才会返回 `Excludes`、`EnableMeter` 等参数。

---

## 配置项

| 方法 | 默认值 | 说明 |
|------|--------|------|
| `setAppName(String)` | 等于 `appId` | 应用显示名 |
| `setVersion(String)` | `1.0.0` | 应用版本，建议显式设置 |
| `setEnableMeter(boolean)` | `true` | 上报报文是否携带 `Info` 性能信息 |
| `setPeriod(int)` | `60` | 上报周期（秒），服务端响应可覆盖 |
| `setHeartbeatPeriod(int)` | `30` | 心跳周期（秒） |
| `setMaxSamples(int)` | `1` | 每个埋点每周期正常样本上限 |
| `setMaxErrors(int)` | `10` | 每个埋点每周期异常样本上限 |
| `setMaxTagLength(int)` | `1024` | Tag 最大长度，超出截断 |
| `setClientId(String)` | `ip@pid` | 客户端实例标识 |
| `setDebug(boolean)` | `false` | 输出监控内部错误日志到标准错误 |

默认排除的埋点名：`/Trace/Report`、`/Trace/ReportRaw`、`/App/Login`、`/App/Ping`。
可使用 `addExclude(String)` 追加，支持 `*`、`?` 通配符。

---

## 上报格式与服务端响应

上报（`POST /Trace/Report`）：

```json
{
  "AppId": "MyApp",
  "AppName": "订单服务",
  "ClientId": "192.168.1.10@12345",
  "Version": "1.0.0",
  "Info": { "Id": 12345, "Version": "1.0.0", "...": "..." },
  "Builders": [
    {
      "Name": "order.create",
      "StartTime": 1784092800000,
      "EndTime": 1784092801000,
      "Total": 100,
      "Errors": 2,
      "Cost": 5000,
      "MaxCost": 120,
      "MinCost": 8,
      "Samples": [
        { "Id": "a1", "ParentId": null, "TraceId": "t1", "StartTime": 1784092800000, "EndTime": 1784092800030, "Tag": "orderId=123", "Error": "" }
      ],
      "ErrorSamples": []
    }
  ]
}
```

当 JSON 报文（UTF-8 字节数）大于 **1024 字节**时，自动改为
`POST /Trace/ReportRaw`，请求体为 GZip 压缩，`Content-Type: application/x-gzip`。
测试表明包含 `Info` 的常规报文通常会超过该阈值，因此实际线上多数走压缩接口。

服务端响应 `data`：

| 字段 | 说明 |
|------|------|
| `Period` | 新的上报周期（秒），SDK 会动态调整定时任务 |
| `MaxSamples` | 正常样本上限 |
| `MaxErrors` | 异常样本上限 |
| `Timeout` | 慢请求阈值（毫秒） |
| `MaxTagLength` | Tag 最大长度，SDK 立即生效 |
| `RequestTagLength` | 请求标签最大长度 |
| `EnableMeter` | 是否上报性能信息 |
| `Excludes` | 需要排除的埋点名，SDK 合并到排除列表 |

---

## 排查建议

1. **平台无数据**
   - 开启 `setDebug(true)`，观察标准错误输出；
   - 检查登录是否成功、`Version` 是否为空、服务端时间与客户端时间是否偏差过大；
   - 检查应用是否在星尘后台启用。
2. **一直 401**
   - 确认 `AppId`/`Secret` 正确且应用已启用；
   - 确认令牌主体与上报 `AppId` 一致。
3. **心跳指标为 0**
   - 非 HotSpot JVM 可能不支持 `com.sun.management`，此时 CPU/句柄等字段为 0；
   - 线程池相关字段在 Java SDK 中固定为 0。
4. **大报文 400**
   - 检查 `StartTime`/`EndTime` 是否为 Unix 毫秒数值；
   - 确认 `Content-Type` 为 `application/x-gzip` 时请求体确实是 GZip。
5. **进程无法退出**
   - SDK 定时线程为守护线程，但仍建议在应用关闭时调用 `tracer.stop()`；
   - `stop()` 会执行一次同步 flush 并关闭调度器，可重复调用。

---

## 构建与测试

环境：JDK 8+、Maven 3.6+。

```bash
cd SDK/Java
mvn -B -q test
```

仅编译：

```bash
mvn -B -q package -DskipTests
```

测试使用 JDK 自带的 `com.sun.net.httpserver.HttpServer` 模拟星尘服务端，覆盖登录、心跳指标、
普通/压缩上报、服务端采样参数下发、401 重登录重试与 `stop()` 幂等性。
