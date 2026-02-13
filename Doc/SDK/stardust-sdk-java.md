# 星尘监控 Java SDK

适用于 Java 8+，提供星尘 APM 监控的接入能力。

## 依赖

无额外依赖，使用 JDK 内置 HTTP 客户端。Java 11+ 可使用 `java.net.http.HttpClient`。

## 快速开始

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

## 完整代码

```java
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;
import java.util.zip.GZIPOutputStream;

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
    private volatile List<String> excludes = new ArrayList<>();

    private final ConcurrentHashMap<String, SpanBuilder> builders = new ConcurrentHashMap<>();
    private volatile boolean running = false;
    private ScheduledExecutorService scheduler;

    public StardustTracer(String server, String appId, String secret) {
        this.server = server.replaceAll("/+$", "");
        this.appId = appId;
        this.appName = appId;
        this.secret = secret;
        this.clientId = getLocalIp() + "@" + ProcessHandle.current().pid();
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
        payload.put("Builders", buildersData);

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
        Object exc = result.get("Excludes");
        if (exc instanceof List) {
            excludes = new ArrayList<>();
            for (Object e : (List<?>) exc) excludes.add(String.valueOf(e));
        }
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
        // 简易 JSON 解析，生产环境建议使用 Gson/Jackson
        // 此处仅做基本演示
        try {
            // 使用 javax.script 作为简单 JSON 解析
            javax.script.ScriptEngine engine = new javax.script.ScriptEngineManager()
                    .getEngineByName("javascript");
            if (engine == null) return null;
            Object result = engine.eval("Java.asJSONCompatible(" + json + ")");
            if (result instanceof Map) return (Map<String, Object>) result;
        } catch (Exception e) {
            // fallback
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

## Spring Boot 集成示例

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
