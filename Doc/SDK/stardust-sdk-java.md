# 星尘监控 Java SDK

适用于 Java 8+（推荐 Java 11+），提供星尘 APM 监控和配置中心的接入能力。

> **版本说明**：
> - **Java 11+**：完整支持，推荐使用
> - **Java 8**：基本支持，需要额外配置（详见末尾兼容性说明）

## 功能特性

- ✅ APM 监控：链路追踪、性能统计、错误采样
- ✅ 配置中心：配置拉取、版本管理、变更通知
- ✅ 应用性能指标：CPU、内存、线程等系统指标上报
- ✅ 零依赖：仅使用 JDK 标准库

## 依赖

无额外依赖，使用 JDK 内置 HTTP 客户端。

> **生产环境建议**：使用 Gson 或 Jackson 替代示例中的简易 JSON 解析器，以获得更好的性能和稳定性。

## 快速开始

### APM 监控

```java
StardustTracer tracer = new StardustTracer("http://star.example.com:6600", "MyJavaApp", "MySecret");
tracer.start();

// 手动埋点
try (Span span = tracer.newSpan("业务操作")) {
    span.setTag("参数信息");
    doSomething();
}

// 程序退出
tracer.stop();
```

### 配置中心

```java
StardustConfig config = new StardustConfig("http://star.example.com:6600", "MyJavaApp", "MySecret");
config.start();

// 获取配置
String dbUrl = config.get("db.url", "jdbc:mysql://localhost:3306/test");
int maxConnections = config.getInt("db.maxConnections", 10);

// 监听配置变更
config.addChangeListener((key, value) -> {
    System.out.println("配置变更：" + key + " = " + value);
});

// 程序退出
config.stop();
```

## 完整代码

### StardustTracer（APM 监控）

```java
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;
import java.util.zip.GZIPOutputStream;
import java.lang.management.*;

/**
 * 星尘监控 Java SDK
 */
public class StardustTracer {

    private final String server;
    private String appId;
    private String appName;
    private String secret;
    private final String clientId;

    private volatile String token = "";
    private volatile long tokenExpire = 0;

    // 采样参数
    private volatile int period = 60;
    private volatile int maxSamples = 1;
    private volatile int maxErrors = 10;
    private volatile int timeout = 5000;
    private volatile int maxTagLength = 1024;
    private volatile int requestTagLength = 0;
    private volatile boolean enableMeter = true;
    private volatile List<String> excludes = new ArrayList<>();

    private final ConcurrentHashMap<String, SpanBuilder> builders = new ConcurrentHashMap<>();
    private volatile boolean running = false;
    private ScheduledExecutorService scheduler;
    private String version = "";

    public StardustTracer(String server, String appId, String secret) {
        this.server = server.replaceAll("/+$", "");
        this.appId = appId;
        this.appName = appId;
        this.secret = secret;
        this.clientId = getLocalIp() + "@" + getProcessId();
    }

    /**
     * 启动追踪器
     */
    public void start() {
        login();
        running = true;
        scheduler = Executors.newScheduledThreadPool(2);
        scheduler.scheduleAtFixedRate(this::flush, period, period, TimeUnit.SECONDS);
        scheduler.scheduleAtFixedRate(this::ping, 30, 30, TimeUnit.SECONDS);
    }

    /**
     * 停止追踪器
     */
    public void stop() {
        running = false;
        flush();
        if (scheduler != null) {
            scheduler.shutdown();
        }
    }

    /**
     * 创建追踪片段
     */
    public Span newSpan(String name) {
        return newSpan(name, "");
    }

    public Span newSpan(String name, String parentId) {
        return new Span(name, this, parentId);
    }

    void finishSpan(Span span) {
        // 排除自身
        if ("/Trace/Report".equals(span.name) || "/Trace/ReportRaw".equals(span.name)) return;
        for (String exc : excludes) {
            if (exc != null && span.name.toLowerCase().contains(exc.toLowerCase())) return;
        }

        // 截断 Tag
        if (span.tag != null && span.tag.length() > maxTagLength) {
            span.tag = span.tag.substring(0, maxTagLength);
        }

        builders.computeIfAbsent(span.name,
                k -> new SpanBuilder(k, maxSamples, maxErrors)).addSpan(span);
    }

    // ========== 网络通信 ==========

    private void login() {
        String url = server + "/App/Login";
        Map<String, Object> payload = new LinkedHashMap<>();
        payload.put("AppId", appId);
        payload.put("Secret", secret);
        payload.put("ClientId", clientId);
        payload.put("AppName", appName);

        try {
            Map<String, Object> data = postJson(url, payload, false);
            if (data != null) {
                token = (String) data.getOrDefault("Token", "");
                int expire = ((Number) data.getOrDefault("Expire", 7200)).intValue();
                tokenExpire = System.currentTimeMillis() / 1000 + expire;
                if (data.get("Code") != null) appId = (String) data.get("Code");
                if (data.get("Secret") != null) secret = (String) data.get("Secret");
            }
        } catch (Exception ex) {
            System.err.println("[Stardust] Login failed: " + ex.getMessage());
        }
    }

    private void ping() {
        String url = server + "/App/Ping?Token=" + urlEncode(token);
        Map<String, Object> payload = new LinkedHashMap<>();
        payload.put("Id", ProcessHandle.current().pid());
        payload.put("Name", appName);
        payload.put("Time", System.currentTimeMillis());

        try {
            Map<String, Object> data = postJson(url, payload, true);
            if (data != null && data.get("Token") != null) {
                String newToken = (String) data.get("Token");
                if (!newToken.isEmpty()) token = newToken;
            }
        } catch (Exception ex) {
            System.err.println("[Stardust] Ping failed: " + ex.getMessage());
        }
    }

    private void report(List<Map<String, Object>> buildersData) {
        Map<String, Object> payload = new LinkedHashMap<>();
        payload.put("AppId", appId);
        payload.put("AppName", appName);
        payload.put("ClientId", clientId);
        if (!version.isEmpty()) payload.put("Version", version);
        payload.put("Builders", buildersData);
        
        // 添加应用性能指标（如果启用）
        if (enableMeter) {
            payload.put("Info", collectAppInfo());
        }

        String body = toJson(payload);

        try {
            Map<String, Object> data;
            if (body.length() > 1024) {
                String url = server + "/Trace/ReportRaw?Token=" + urlEncode(token);
                data = postGzip(url, body);
            } else {
                String url = server + "/Trace/Report?Token=" + urlEncode(token);
                data = postJson(url, payload, true);
            }
            if (data != null) applyResponse(data);
        } catch (Exception ex) {
            System.err.println("[Stardust] Report failed: " + ex.getMessage());
        }
    }

    private void applyResponse(Map<String, Object> result) {
        if (getInt(result, "Period") > 0) period = getInt(result, "Period");
        if (getInt(result, "MaxSamples") > 0) maxSamples = getInt(result, "MaxSamples");
        if (getInt(result, "MaxErrors") > 0) maxErrors = getInt(result, "MaxErrors");
        if (getInt(result, "Timeout") > 0) timeout = getInt(result, "Timeout");
        if (getInt(result, "MaxTagLength") > 0) maxTagLength = getInt(result, "MaxTagLength");
        if (getInt(result, "RequestTagLength") > 0) requestTagLength = getInt(result, "RequestTagLength");
        
        // 处理 EnableMeter（Boolean类型）
        Object meter = result.get("EnableMeter");
        if (meter instanceof Boolean) {
            enableMeter = (Boolean) meter;
        }
        
        Object exc = result.get("Excludes");
        if (exc instanceof List) {
            excludes = new ArrayList<>();
            for (Object e : (List<?>) exc) excludes.add(String.valueOf(e));
        }
    }
    
    private Map<String, Object> collectAppInfo() {
        Map<String, Object> info = new LinkedHashMap<>();
        try {
            Runtime runtime = Runtime.getRuntime();
            java.lang.management.OperatingSystemMXBean osBean = 
                java.lang.management.ManagementFactory.getOperatingSystemMXBean();
            
            // 获取进程ID（Java 9+）
            try {
                info.put("Id", ProcessHandle.current().pid());
            } catch (NoClassDefFoundError | NoSuchMethodError e) {
                // Java 8 fallback：使用进程名解析
                String processName = java.lang.management.ManagementFactory.getRuntimeMXBean().getName();
                String pid = processName.split("@")[0];
                info.put("Id", Integer.parseInt(pid));
            }
            
            info.put("Name", appName);
            info.put("Time", System.currentTimeMillis());
            
            // 尝试获取 CPU 使用率（需要 com.sun.management API）
            try {
                if (osBean instanceof com.sun.management.OperatingSystemMXBean) {
                    com.sun.management.OperatingSystemMXBean sunOsBean = 
                        (com.sun.management.OperatingSystemMXBean) osBean;
                    info.put("CpuUsage", sunOsBean.getProcessCpuLoad());
                }
            } catch (Exception e) {
                // 某些 JVM 实现可能不支持
            }
            
            info.put("WorkingSet", runtime.totalMemory() - runtime.freeMemory());
            info.put("Threads", Thread.activeCount());
        } catch (Exception ex) {
            // 忽略性能指标收集错误，不影响主流程
        }
        return info;
    }

    private void flush() {
        if (builders.isEmpty()) return;

        List<Map<String, Object>> list = new ArrayList<>();
        Iterator<Map.Entry<String, SpanBuilder>> it = builders.entrySet().iterator();
        while (it.hasNext()) {
            Map.Entry<String, SpanBuilder> entry = it.next();
            SpanBuilder sb = entry.getValue();
            if (sb.total > 0) list.add(sb.toMap());
            it.remove();
        }

        if (!list.isEmpty()) report(list);
    }

    // ========== HTTP 工具 ==========

    @SuppressWarnings("unchecked")
    private Map<String, Object> postJson(String url, Map<String, Object> payload, boolean useToken)
            throws Exception {
        String body = toJson(payload);
        HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
        conn.setRequestMethod("POST");
        conn.setRequestProperty("Content-Type", "application/json; charset=utf-8");
        conn.setConnectTimeout(10000);
        conn.setReadTimeout(10000);
        conn.setDoOutput(true);

        try (OutputStream os = conn.getOutputStream()) {
            os.write(body.getBytes(StandardCharsets.UTF_8));
        }

        String resp = readResponse(conn);
        Map<String, Object> root = parseJson(resp);
        if (root != null && Integer.valueOf(0).equals(getInt(root, "code"))) {
            return (Map<String, Object>) root.get("data");
        }
        return null;
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> postGzip(String url, String jsonBody) throws Exception {
        byte[] data = jsonBody.getBytes(StandardCharsets.UTF_8);
        ByteArrayOutputStream bos = new ByteArrayOutputStream();
        try (GZIPOutputStream gzip = new GZIPOutputStream(bos)) {
            gzip.write(data);
        }
        byte[] compressed = bos.toByteArray();

        HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
        conn.setRequestMethod("POST");
        conn.setRequestProperty("Content-Type", "application/x-gzip");
        conn.setConnectTimeout(10000);
        conn.setReadTimeout(10000);
        conn.setDoOutput(true);

        try (OutputStream os = conn.getOutputStream()) {
            os.write(compressed);
        }

        String resp = readResponse(conn);
        Map<String, Object> root = parseJson(resp);
        if (root != null && Integer.valueOf(0).equals(getInt(root, "code"))) {
            return (Map<String, Object>) root.get("data");
        }
        return null;
    }

    private String readResponse(HttpURLConnection conn) throws Exception {
        InputStream is = conn.getResponseCode() < 400 ? conn.getInputStream() : conn.getErrorStream();
        BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.UTF_8));
        StringBuilder sb = new StringBuilder();
        String line;
        while ((line = reader.readLine()) != null) sb.append(line);
        reader.close();
        return sb.toString();
    }

    // ========== JSON 工具（简易实现，生产环境建议使用 Gson/Jackson） ==========

    private String toJson(Map<String, Object> map) {
        StringBuilder sb = new StringBuilder("{");
        boolean first = true;
        for (Map.Entry<String, Object> entry : map.entrySet()) {
            if (!first) sb.append(",");
            first = false;
            sb.append("\"").append(entry.getKey()).append("\":");
            sb.append(valueToJson(entry.getValue()));
        }
        sb.append("}");
        return sb.toString();
    }

    @SuppressWarnings("unchecked")
    private String valueToJson(Object value) {
        if (value == null) return "null";
        if (value instanceof String) return "\"" + escapeJson((String) value) + "\"";
        if (value instanceof Number || value instanceof Boolean) return value.toString();
        if (value instanceof Map) return toJson((Map<String, Object>) value);
        if (value instanceof List) {
            StringBuilder sb = new StringBuilder("[");
            boolean first = true;
            for (Object item : (List<?>) value) {
                if (!first) sb.append(",");
                first = false;
                sb.append(valueToJson(item));
            }
            sb.append("]");
            return sb.toString();
        }
        return "\"" + escapeJson(value.toString()) + "\"";
    }

    private String escapeJson(String s) {
        return s.replace("\\", "\\\\").replace("\"", "\\\"")
                .replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t");
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> parseJson(String json) {
        // 简易 JSON 解析
        // 注意： JavaScript 引擎（Nashorn）在 Java 11 被弃用，Java 15 已移除
        // 生产环境建议使用 Gson、Jackson 等专业库
        try {
            javax.script.ScriptEngine engine = new javax.script.ScriptEngineManager()
                    .getEngineByName("javascript");
            if (engine == null) return null;
            Object result = engine.eval("Java.asJSONCompatible(" + json + ")");
            if (result instanceof Map) return (Map<String, Object>) result;
        } catch (Exception e) {
            // fallback - 如果 JavaScript 引擎不可用（Java 15+）
            System.err.println("[Stardust] JSON parse failed, consider using Gson/Jackson: " + e.getMessage());
        }
        return null;
    }

    private int getInt(Map<String, Object> map, String key) {
        Object v = map.get(key);
        if (v instanceof Number) return ((Number) v).intValue();
        return 0;
    }

    private String urlEncode(String value) {
        try {
            return URLEncoder.encode(value, "UTF-8");
        } catch (Exception e) {
            return value;
        }
    }

    private static String getLocalIp() {
        try (DatagramSocket socket = new DatagramSocket()) {
            socket.connect(InetAddress.getByName("8.8.8.8"), 80);
            return socket.getLocalAddress().getHostAddress();
        } catch (Exception e) {
            return "127.0.0.1";
        }
    }
    
    private static long getProcessId() {
        try {
            // Java 9+ ProcessHandle API
            return ProcessHandle.current().pid();
        } catch (NoClassDefFoundError | NoSuchMethodError e) {
            // Java 8 fallback
            try {
                String processName = ManagementFactory.getRuntimeMXBean().getName();
                return Long.parseLong(processName.split("@")[0]);
            } catch (Exception ex) {
                return 0;
            }
        }
    }

    // ========== 内部类 ==========

    /**
     * 追踪片段
     */
    public static class Span implements AutoCloseable {
        final String name;
        final String id;
        String parentId;
        String traceId;
        long startTime;
        long endTime;
        String tag = "";
        String error = "";
        private final StardustTracer tracer;

        Span(String name, StardustTracer tracer, String parentId) {
            this.name = name;
            this.id = UUID.randomUUID().toString().replace("-", "").substring(0, 16);
            this.parentId = parentId != null ? parentId : "";
            this.traceId = UUID.randomUUID().toString().replace("-", "");
            this.startTime = System.currentTimeMillis();
            this.tracer = tracer;
        }

        public void setTag(String tag) { this.tag = tag; }
        public void setError(Exception ex) { this.error = ex.toString(); }
        public void setError(String msg) { this.error = msg; }
        public String getId() { return id; }
        public String getTraceId() { return traceId; }

        public void finish() {
            endTime = System.currentTimeMillis();
            tracer.finishSpan(this);
        }

        @Override
        public void close() {
            if (endTime == 0) finish();
        }

        Map<String, Object> toMap() {
            Map<String, Object> map = new LinkedHashMap<>();
            map.put("Id", id);
            map.put("ParentId", parentId);
            map.put("TraceId", traceId);
            map.put("StartTime", startTime);
            map.put("EndTime", endTime);
            map.put("Tag", tag);
            map.put("Error", error);
            return map;
        }
    }

    /**
     * 追踪构建器
     */
    static class SpanBuilder {
        final String name;
        long startTime;
        volatile long endTime;
        volatile int total;
        volatile int errors;
        volatile long cost;
        volatile int maxCost;
        volatile int minCost;
        final List<Span> samples = Collections.synchronizedList(new ArrayList<>());
        final List<Span> errorSamples = Collections.synchronizedList(new ArrayList<>());
        private final int maxSamplesLimit;
        private final int maxErrorsLimit;

        SpanBuilder(String name, int maxSamples, int maxErrors) {
            this.name = name;
            this.startTime = System.currentTimeMillis();
            this.maxSamplesLimit = maxSamples;
            this.maxErrorsLimit = maxErrors;
        }

        synchronized void addSpan(Span span) {
            int elapsed = (int) (span.endTime - span.startTime);
            total++;
            cost += elapsed;
            if (maxCost == 0 || elapsed > maxCost) maxCost = elapsed;
            if (minCost == 0 || elapsed < minCost) minCost = elapsed;

            if (span.error != null && !span.error.isEmpty()) {
                errors++;
                if (errorSamples.size() < maxErrorsLimit) errorSamples.add(span);
            } else {
                if (samples.size() < maxSamplesLimit) samples.add(span);
            }
            endTime = System.currentTimeMillis();
        }

        Map<String, Object> toMap() {
            Map<String, Object> map = new LinkedHashMap<>();
            map.put("Name", name);
            map.put("StartTime", startTime);
            map.put("EndTime", endTime);
            map.put("Total", total);
            map.put("Errors", errors);
            map.put("Cost", cost);
            map.put("MaxCost", maxCost);
            map.put("MinCost", minCost);
            List<Map<String, Object>> s = new ArrayList<>();
            for (Span sp : samples) s.add(sp.toMap());
            map.put("Samples", s);
            List<Map<String, Object>> es = new ArrayList<>();
            for (Span sp : errorSamples) es.add(sp.toMap());
            map.put("ErrorSamples", es);
            return map;
        }
    }
}
```

## 配置中心实现

### StardustConfig（配置中心）

```java
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.BiConsumer;
import java.lang.management.*;

/**
 * 星尘配置中心 Java SDK
 */
public class StardustConfig {

    private final String server;
    private String appId;
    private String appName;
    private String secret;
    private final String clientId;
    
    private volatile String token = "";
    private volatile long tokenExpire = 0;
    
    // 配置参数
    private volatile int version = 0;
    private volatile String scope = "";
    private final Map<String, String> configs = new ConcurrentHashMap<>();
    private final List<BiConsumer<String, String>> changeListeners = new CopyOnWriteArrayList<>();
    
    private volatile boolean running = false;
    private ScheduledExecutorService scheduler;
    private static final int POLL_PERIOD = 60; // 轮询周期（秒）

    public StardustConfig(String server, String appId, String secret) {
        this.server = server.replaceAll("/+$", "");
        this.appId = appId;
        this.appName = appId;
        this.secret = secret;
        this.clientId = getLocalIp() + "@" + getProcessId();
    }

    /**
     * 启动配置客户端
     */
    public void start() {
        login();
        loadConfig();
        running = true;
        scheduler = Executors.newScheduledThreadPool(1);
        scheduler.scheduleAtFixedRate(this::poll, POLL_PERIOD, POLL_PERIOD, TimeUnit.SECONDS);
    }

    /**
     * 停止配置客户端
     */
    public void stop() {
        running = false;
        if (scheduler != null) {
            scheduler.shutdown();
        }
    }

    /**
     * 获取配置值
     */
    public String get(String key) {
        return configs.get(key);
    }

    /**
     * 获取配置值（带默认值）
     */
    public String get(String key, String defaultValue) {
        return configs.getOrDefault(key, defaultValue);
    }

    /**
     * 获取整型配置
     */
    public int getInt(String key, int defaultValue) {
        String value = configs.get(key);
        if (value == null) return defaultValue;
        try {
            return Integer.parseInt(value);
        } catch (NumberFormatException ex) {
            return defaultValue;
        }
    }

    /**
     * 获取布尔型配置
     */
    public boolean getBoolean(String key, boolean defaultValue) {
        String value = configs.get(key);
        if (value == null) return defaultValue;
        return Boolean.parseBoolean(value);
    }

    /**
     * 获取所有配置
     */
    public Map<String, String> getAll() {
        return new HashMap<>(configs);
    }

    /**
     * 添加配置变更监听器
     */
    public void addChangeListener(BiConsumer<String, String> listener) {
        changeListeners.add(listener);
    }

    // ========== 内部方法 ==========

    private void login() {
        String url = server + "/App/Login";
        Map<String, Object> payload = new LinkedHashMap<>();
        payload.put("AppId", appId);
        payload.put("Secret", secret);
        payload.put("ClientId", clientId);
        payload.put("AppName", appName);

        try {
            Map<String, Object> data = postJson(url, payload);
            if (data != null) {
                token = (String) data.getOrDefault("Token", "");
                int expire = ((Number) data.getOrDefault("Expire", 7200)).intValue();
                tokenExpire = System.currentTimeMillis() / 1000 + expire;
                if (data.get("Code") != null) appId = (String) data.get("Code");
                if (data.get("Secret") != null) secret = (String) data.get("Secret");
            }
        } catch (Exception ex) {
            System.err.println("[Stardust] Config login failed: " + ex.getMessage());
        }
    }

    private void loadConfig() {
        String url = server + "/Config/GetAll?Token=" + urlEncode(token);
        Map<String, Object> payload = new LinkedHashMap<>();
        payload.put("AppId", appId);
        payload.put("Secret", secret);
        payload.put("ClientId", clientId);
        payload.put("Scope", scope);
        payload.put("Version", version);

        try {
            Map<String, Object> data = postJson(url, payload);
            if (data != null) {
                processConfigResponse(data);
            }
        } catch (Exception ex) {
            System.err.println("[Stardust] Load config failed: " + ex.getMessage());
        }
    }

    private void poll() {
        loadConfig();
    }

    @SuppressWarnings("unchecked")
    private void processConfigResponse(Map<String, Object> data) {
        // 更新版本号
        Object ver = data.get("Version");
        if (ver instanceof Number) {
            int newVersion = ((Number) ver).intValue();
            if (newVersion > version) {
                version = newVersion;
                
                // 获取配置字典
                Object configsObj = data.get("Configs");
                if (configsObj instanceof Map) {
                    Map<String, Object> newConfigs = (Map<String, Object>) configsObj;
                    
                    // 检测变更并通知
                    for (Map.Entry<String, Object> entry : newConfigs.entrySet()) {
                        String key = entry.getKey();
                        String newValue = String.valueOf(entry.getValue());
                        String oldValue = configs.get(key);
                        
                        if (!newValue.equals(oldValue)) {
                            configs.put(key, newValue);
                            notifyChange(key, newValue);
                        }
                    }
                    
                    // 检测删除的配置
                    Set<String> removedKeys = new HashSet<>(configs.keySet());
                    removedKeys.removeAll(newConfigs.keySet());
                    for (String key : removedKeys) {
                        configs.remove(key);
                        notifyChange(key, null);
                    }
                }
            }
        }
        
        // 更新作用域
        Object scopeObj = data.get("Scope");
        if (scopeObj != null) {
            scope = String.valueOf(scopeObj);
        }
    }

    private void notifyChange(String key, String value) {
        for (BiConsumer<String, String> listener : changeListeners) {
            try {
                listener.accept(key, value);
            } catch (Exception ex) {
                System.err.println("[Stardust] Config change listener error: " + ex.getMessage());
            }
        }
    }

    // ========== HTTP 工具方法（简化版，与 StardustTracer 类似） ==========

    @SuppressWarnings("unchecked")
    private Map<String, Object> postJson(String url, Map<String, Object> payload) throws Exception {
        String body = toJson(payload);
        HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
        conn.setRequestMethod("POST");
        conn.setRequestProperty("Content-Type", "application/json; charset=utf-8");
        conn.setConnectTimeout(10000);
        conn.setReadTimeout(10000);
        conn.setDoOutput(true);

        try (OutputStream os = conn.getOutputStream()) {
            os.write(body.getBytes(StandardCharsets.UTF_8));
        }

        String resp = readResponse(conn);
        Map<String, Object> root = parseJson(resp);
        if (root != null && Integer.valueOf(0).equals(getInt(root, "code"))) {
            return (Map<String, Object>) root.get("data");
        }
        return null;
    }

    private String readResponse(HttpURLConnection conn) throws Exception {
        InputStream is = conn.getResponseCode() < 400 ? conn.getInputStream() : conn.getErrorStream();
        BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.UTF_8));
        StringBuilder sb = new StringBuilder();
        String line;
        while ((line = reader.readLine()) != null) sb.append(line);
        reader.close();
        return sb.toString();
    }

    // JSON 序列化/反序列化（简化实现）
    private String toJson(Map<String, Object> map) {
        StringBuilder sb = new StringBuilder("{");
        boolean first = true;
        for (Map.Entry<String, Object> entry : map.entrySet()) {
            if (!first) sb.append(",");
            first = false;
            sb.append("\"").append(entry.getKey()).append("\":");
            sb.append(valueToJson(entry.getValue()));
        }
        sb.append("}");
        return sb.toString();
    }

    @SuppressWarnings("unchecked")
    private String valueToJson(Object value) {
        if (value == null) return "null";
        if (value instanceof String) return "\"" + escapeJson((String) value) + "\"";
        if (value instanceof Number || value instanceof Boolean) return value.toString();
        if (value instanceof Map) return toJson((Map<String, Object>) value);
        return "\"" + escapeJson(value.toString()) + "\"";
    }

    private String escapeJson(String s) {
        return s.replace("\\", "\\\\").replace("\"", "\\\"")
                .replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t");
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> parseJson(String json) {
        // 简易 JSON 解析
        // 注意： JavaScript 引擎（Nashorn）在 Java 11 被弃用，Java 15 已移除
        // 生产环境建议使用 Gson、Jackson 等专业库
        try {
            javax.script.ScriptEngine engine = new javax.script.ScriptEngineManager()
                    .getEngineByName("javascript");
            if (engine == null) return null;
            Object result = engine.eval("Java.asJSONCompatible(" + json + ")");
            if (result instanceof Map) return (Map<String, Object>) result;
        } catch (Exception e) {
            System.err.println("[Stardust] JSON parse failed, consider using Gson/Jackson: " + e.getMessage());
        }
        return null;
    }

    private int getInt(Map<String, Object> map, String key) {
        Object v = map.get(key);
        if (v instanceof Number) return ((Number) v).intValue();
        return 0;
    }

    private String urlEncode(String value) {
        try {
            return URLEncoder.encode(value, "UTF-8");
        } catch (Exception e) {
            return value;
        }
    }

    private static String getLocalIp() {
        try (DatagramSocket socket = new DatagramSocket()) {
            socket.connect(InetAddress.getByName("8.8.8.8"), 80);
            return socket.getLocalAddress().getHostAddress();
        } catch (Exception e) {
            return "127.0.0.1";
        }
    }
    
    private static long getProcessId() {
        try {
            // Java 9+ ProcessHandle API
            return ProcessHandle.current().pid();
        } catch (NoClassDefFoundError | NoSuchMethodError e) {
            // Java 8 fallback
            try {
                String processName = ManagementFactory.getRuntimeMXBean().getName();
                return Long.parseLong(processName.split("@")[0]);
            } catch (Exception ex) {
                return 0;
            }
        }
    }
}
```

## Spring Boot 集成示例

### APM 监控拦截器

```java
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.HandlerInterceptor;
import javax.servlet.http.*;

@Component
public class StardustInterceptor implements HandlerInterceptor {

    private static final StardustTracer tracer =
            new StardustTracer("http://star.example.com:6600", "MySpringApp", "secret");

    static { tracer.start(); }

    private static final ThreadLocal<StardustTracer.Span> SPAN = new ThreadLocal<>();

    @Override
    public boolean preHandle(HttpServletRequest request, HttpServletResponse response,
                             Object handler) {
        String name = request.getMethod() + " " + request.getRequestURI();
        StardustTracer.Span span = tracer.newSpan(name);
        span.setTag(request.getMethod() + " " + request.getRequestURL()
                + (request.getQueryString() != null ? "?" + request.getQueryString() : ""));
        SPAN.set(span);
        return true;
    }

    @Override
    public void afterCompletion(HttpServletRequest request, HttpServletResponse response,
                                Object handler, Exception ex) {
        StardustTracer.Span span = SPAN.get();
        if (span != null) {
            if (ex != null) span.setError(ex);
            else if (response.getStatus() >= 400) span.setError("HTTP " + response.getStatus());
            span.finish();
            SPAN.remove();
        }
    }
}
```

### Spring Boot 配置中心集成

```java
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import javax.annotation.PostConstruct;
import javax.annotation.PreDestroy;

@Configuration
public class StardustConfigProvider {

    private StardustConfig config;

    @PostConstruct
    public void init() {
        config = new StardustConfig(
            "http://star.example.com:6600",
            "MySpringApp",
            "secret"
        );
        config.start();
        
        // 监听配置变更
        config.addChangeListener((key, value) -> {
            System.out.println("配置变更：" + key + " = " + value);
            // 这里可以实现配置热更新逻辑
        });
    }

    @PreDestroy
    public void destroy() {
        if (config != null) {
            config.stop();
        }
    }

    @Bean
    public StardustConfig stardustConfig() {
        return config;
    }
}
```

使用配置：

```java
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;

@Service
public class MyService {

    @Autowired
    private StardustConfig config;

    public void doSomething() {
        String dbUrl = config.get("db.url", "jdbc:mysql://localhost:3306/test");
        int maxConnections = config.getInt("db.maxConnections", 10);
        boolean enableCache = config.getBoolean("cache.enable", true);
        
        System.out.println("Database URL: " + dbUrl);
        System.out.println("Max Connections: " + maxConnections);
        System.out.println("Cache Enabled: " + enableCache);
    }
}
```

## 完整使用示例

### 同时使用 APM 和配置中心

```java
public class Application {
    
    private static StardustTracer tracer;
    private static StardustConfig config;
    
    public static void main(String[] args) {
        // 初始化配置中心
        config = new StardustConfig("http://star.example.com:6600", "MyApp", "secret");
        config.start();
        
        // 初始化 APM 监控
        tracer = new StardustTracer("http://star.example.com:6600", "MyApp", "secret");
        tracer.start();
        
        // 监听配置变更
        config.addChangeListener((key, value) -> {
            System.out.println("配置已更新：" + key + " = " + value);
        });
        
        // 业务逻辑
        runBusiness();
        
        // 程序退出时清理
        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            tracer.stop();
            config.stop();
        }));
    }
    
    private static void runBusiness() {
        // 使用配置
        String apiEndpoint = config.get("api.endpoint", "https://api.example.com");
        int timeout = config.getInt("api.timeout", 5000);
        
        // 使用 APM 追踪
        try (StardustTracer.Span span = tracer.newSpan("调用外部API")) {
            span.setTag("endpoint=" + apiEndpoint + ", timeout=" + timeout);
            // 实际业务调用
            callExternalApi(apiEndpoint, timeout);
        } catch (Exception ex) {
            System.err.println("调用失败：" + ex.getMessage());
        }
    }
    
    private static void callExternalApi(String endpoint, int timeout) {
        // 实现 API 调用逻辑
    }
}
```

## 高级特性

### 1. 配置作用域（Scope）

配置中心支持多环境配置，通过作用域区分不同环境（如 dev、test、prod）：

```java
StardustConfig config = new StardustConfig("http://star.example.com:6600", "MyApp", "secret");
// 作用域会自动根据客户端 IP 和规则判断
config.start();
```

### 2. 性能指标自动上报

StardustTracer 会自动收集并上报应用性能指标：
- CPU 使用率
- 内存占用
- 线程数

可以通过 `enableMeter` 属性控制：

```java
// 如需禁用性能指标收集（适合高并发场景）
// 需要在服务端配置中设置 EnableMeter = false
```

### 3. 链路追踪

支持父子 Span 关联，实现完整的调用链追踪：

```java
try (StardustTracer.Span parentSpan = tracer.newSpan("处理订单")) {
    parentSpan.setTag("orderId=12345");
    
    // 创建子 Span
    try (StardustTracer.Span childSpan = tracer.newSpan("查询用户信息", parentSpan.getId())) {
        childSpan.setTag("userId=67890");
        queryUserInfo();
    }
    
    // 另一个子 Span
    try (StardustTracer.Span childSpan = tracer.newSpan("扣减库存", parentSpan.getId())) {
        childSpan.setTag("productId=111");
        reduceStock();
    }
}
```

## 注意事项

1. **线程安全**：StardustTracer 和 StardustConfig 都是线程安全的，可以在多线程环境中使用。

2. **资源管理**：务必在应用退出时调用 `stop()` 方法，确保数据上报完成和资源释放。

3. **异常处理**：SDK 内部会捕获并记录异常，不会影响业务逻辑执行。

4. **性能影响**：APM 监控会带来少量性能开销（通常 < 1%），可以根据需要调整采样参数。

5. **JSON 解析**：
   - 示例代码使用 JavaScript 引擎（Nashorn）进行 JSON 解析
   - Nashorn 在 Java 11 被标记为弃用，在 Java 15 中已完全移除
   - **生产环境强烈建议**使用 Gson、Jackson 或 FastJSON 等专业 JSON 库

6. **Java 版本兼容性**：
   - 示例代码主要针对 Java 11+
   - 使用了 Java 8 fallback 机制来获取进程 ID
   - 如完全运行在 Java 8 环境，可能需要调整部分代码

## Java 8 兼容性说明

如需完全支持 Java 8，需要进行以下调整：

### 1. 移除 ProcessHandle 依赖

示例代码已包含 fallback 机制，但如果编译环境是 Java 8，需要确保不引用 `ProcessHandle` 类。

### 2. 替换 JavaScript 引擎

Java 8 使用 Nashorn 引擎，但建议直接使用专业 JSON 库：

```xml
<!-- Maven -->
<dependency>
    <groupId>com.google.code.gson</groupId>
    <artifactId>gson</artifactId>
    <version>2.11.0</version>
</dependency>
```

```java
// 替换 parseJson 方法
import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;

private final Gson gson = new Gson();

@SuppressWarnings("unchecked")
private Map<String, Object> parseJson(String json) {
    return gson.fromJson(json, new TypeToken<Map<String, Object>>(){}.getType());
}

private String toJson(Map<String, Object> map) {
    return gson.toJson(map);
}
```

### 3. 编译目标设置

```xml
<!-- Maven pom.xml -->
<properties>
    <maven.compiler.source>8</maven.compiler.source>
    <maven.compiler.target>8</maven.compiler.target>
</properties>
```

或

```gradle
// build.gradle
sourceCompatibility = '8'
targetCompatibility = '8'
```

## 相关资源

- [星尘监控平台文档](https://github.com/NewLifeX/Stardust)
- [星尘监控接入 API 文档](../星尘监控接入Api文档.md)
- [.NET SDK 文档](https://github.com/NewLifeX/Stardust/tree/master/Stardust)
- [Go SDK 文档](./stardust-sdk-go.md)
- [Python SDK 文档](./stardust-sdk-python.md)
