package com.newlife.stardust;

import java.nio.charset.Charset;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Base64;
import java.util.Deque;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.TimeUnit;

/**
 * Stardust APM tracer for Java 8.
 *
 * <p>The tracer logs in with the application id and secret, sends periodic heartbeats
 * with process metrics, aggregates spans and reports them to the Stardust server.
 * It has no runtime dependencies beyond the JDK.</p>
 *
 * <pre>
 * // 一行接入：创建、启动并注册为默认实例
 * StardustTracer tracer = StardustTracer.init("http://star.example.com", "MyApp", "secret");
 * // 需要自定义名称/版本时改用 new StardustTracer(...).setVersion("1.0.0").start()
 * try (Span span = tracer.newSpan("order.create")) {
 *     span.setTag("orderId=123");
 *     // ... business code ...
 * }
 * tracer.stop();
 * </pre>
 */
public class StardustTracer {

    /** SDK version. */
    public static final String VERSION = "1.0.0";

    private static final long TOKEN_REFRESH_WINDOW_MS = 5 * 60 * 1000L;
    private static final int REPORT_GZIP_THRESHOLD = 1024;
    private static final Charset UTF8 = Charset.forName("UTF-8");

    private static final String[] DEFAULT_EXCLUDES = {
            "/Trace/Report", "/Trace/ReportRaw", "/App/Login", "/App/Ping"
    };

    private final String server;
    private volatile String appId;
    private volatile String secret;
    private final HttpTransport transport;
    private final AppMetrics metrics;

    private volatile String appName;
    private volatile String version = VERSION;
    private volatile String clientId;
    private volatile boolean enableMeter = true;
    private volatile int period = 60;
    private volatile int heartbeatPeriod = 30;
    private volatile int maxSamples = 1;
    private volatile int maxErrors = 10;
    private volatile int maxTagLength = 1024;
    private volatile boolean debug;
    private volatile boolean autoInstrument = true;

    private static volatile StardustTracer defaultTracer;

    private volatile String token;
    private volatile long tokenExpireAt;
    private volatile boolean running;

    private final Object loginLock = new Object();
    private final Object reportLock = new Object();
    private final Object pingLock = new Object();
    private final Object scheduleLock = new Object();
    private final Map<String, SpanBuilder> builders = new LinkedHashMap<String, SpanBuilder>();
    private final Set<String> excludes = new HashSet<String>();
    private final ThreadLocal<Deque<Span>> spanStack = new ThreadLocal<Deque<Span>>() {
        @Override
        protected Deque<Span> initialValue() {
            return new ArrayDeque<Span>();
        }
    };

    private ScheduledExecutorService scheduler;
    private ScheduledFuture<?> reportTask;
    private ScheduledFuture<?> heartbeatTask;

    /**
     * Creates a tracer.
     *
     * @param server Stardust server base URL, for example {@code http://localhost:6600}
     * @param appId application id
     * @param secret application secret
     */
    public StardustTracer(String server, String appId, String secret) {
        this(server, appId, secret, null);
    }

    /**
     * Creates a tracer.
     *
     * @param server Stardust server base URL, for example {@code http://localhost:6600}
     * @param appId application id
     * @param secret application secret
     * @param appName display name, defaults to the application id
     */
    public StardustTracer(String server, String appId, String secret, String appName) {
        if (server == null || server.trim().length() == 0) throw new IllegalArgumentException("server is required");
        if (appId == null || appId.trim().length() == 0) throw new IllegalArgumentException("appId is required");

        this.server = server.trim();
        this.appId = appId.trim();
        this.secret = secret == null ? "" : secret;
        this.appName = appName == null || appName.trim().length() == 0 ? this.appId : appName.trim();
        this.transport = new HttpTransport(this.server);
        this.metrics = new AppMetrics(this);
        synchronized (excludes) {
            for (String exclude : DEFAULT_EXCLUDES) {
                excludes.add(exclude);
            }
        }

        // 自动埋点默认无需额外注册：最后创建的追踪器成为进程默认实例
        if (autoInstrument) defaultTracer = this;
    }

    /**
     * Sets the application display name.
     *
     * @param appName application name
     * @return this tracer
     */
    public StardustTracer setAppName(String appName) {
        if (appName != null && appName.trim().length() > 0) this.appName = appName.trim();
        return this;
    }

    /**
     * Sets the application version reported to the server.
     *
     * @param version version string
     * @return this tracer
     */
    public StardustTracer setVersion(String version) {
        if (version != null && version.trim().length() > 0) this.version = version.trim();
        return this;
    }

    /**
     * Enables or disables application metrics in reports.
     *
     * @param enableMeter {@code true} to include process metrics
     * @return this tracer
     */
    public StardustTracer setEnableMeter(boolean enableMeter) {
        this.enableMeter = enableMeter;
        return this;
    }

    /**
     * Sets the report period. The server response can override this value.
     *
     * @param period period in seconds
     * @return this tracer
     */
    public StardustTracer setPeriod(int period) {
        if (period > 0) {
            this.period = period;
            if (running) scheduleReport();
        }
        return this;
    }

    /**
     * Sets the heartbeat period.
     *
     * @param heartbeatPeriod period in seconds
     * @return this tracer
     */
    public StardustTracer setHeartbeatPeriod(int heartbeatPeriod) {
        if (heartbeatPeriod > 0) {
            this.heartbeatPeriod = heartbeatPeriod;
            if (running) scheduleHeartbeat();
        }
        return this;
    }

    /**
     * Sets the maximum number of normal samples per builder and period.
     *
     * @param maxSamples maximum samples
     * @return this tracer
     */
    public StardustTracer setMaxSamples(int maxSamples) {
        this.maxSamples = maxSamples;
        applyBuilderLimits();
        return this;
    }

    /**
     * Sets the maximum number of error samples per builder and period.
     *
     * @param maxErrors maximum error samples
     * @return this tracer
     */
    public StardustTracer setMaxErrors(int maxErrors) {
        this.maxErrors = maxErrors;
        applyBuilderLimits();
        return this;
    }

    /**
     * Sets the maximum tag length. Longer tags are truncated.
     *
     * @param maxTagLength maximum tag length, values below 1 disable truncation
     * @return this tracer
     */
    public StardustTracer setMaxTagLength(int maxTagLength) {
        this.maxTagLength = maxTagLength;
        return this;
    }

    /**
     * Sets the client id. The default is {@code ip@pid}.
     *
     * @param clientId client id
     * @return this tracer
     */
    public StardustTracer setClientId(String clientId) {
        if (clientId != null && clientId.trim().length() > 0) this.clientId = clientId.trim();
        return this;
    }

    /**
     * Enables debug logging to standard error. Monitoring errors are only logged when enabled.
     *
     * @param debug debug flag
     * @return this tracer
     */
    public StardustTracer setDebug(boolean debug) {
        this.debug = debug;
        return this;
    }

    /**
     * Enables or disables automatic instrumentation helpers.
     *
     * <p>When disabled, {@link StardustFilter}, {@link StardustHttp}, {@link StardustJdbc}
     * and {@link StardustRedis} pass calls through without creating spans or injecting
     * {@code traceparent}. Manual {@link #newSpan(String)} calls are not affected. The
     * default is {@code true}.</p>
     *
     * @param autoInstrument {@code true} to enable automatic instrumentation
     * @return this tracer
     */
    public StardustTracer setAutoInstrument(boolean autoInstrument) {
        this.autoInstrument = autoInstrument;
        return this;
    }

    /**
     * Returns whether automatic instrumentation helpers are enabled.
     *
     * @return automatic instrumentation flag
     */
    public boolean isAutoInstrument() {
        return autoInstrument;
    }

    /**
     * Returns the process-wide default tracer used by automatic instrumentation helpers.
     *
     * @return default tracer, or {@code null} when not configured
     */
    public static StardustTracer getDefault() {
        return defaultTracer;
    }

    /**
     * Creates and starts a tracer in one call.
     *
     * <p>Equivalent to {@code new StardustTracer(server, appId, secret).start()}. The created
     * tracer is registered as the process default, so Servlet filters, {@code StardustHttp},
     * {@code StardustJdbc} and {@code StardustRedis} work without extra registration.</p>
     *
     * @param server Stardust server base URL, for example {@code http://localhost:6600}
     * @param appId application id
     * @param secret application secret
     * @return the started tracer
     */
    public static StardustTracer init(String server, String appId, String secret) {
        return new StardustTracer(server, appId, secret).start();
    }

    /**
     * Sets the process-wide default tracer used by automatic instrumentation helpers.
     *
     * <p>A tracer created with auto-instrumentation enabled automatically becomes the default,
     * so Servlet filters, {@code StardustHttp}, {@code StardustJdbc} and {@code StardustRedis}
     * can find it without an extra call. Use this method to override the default explicitly.</p>
     *
     * @param tracer tracer, may be {@code null} to clear the default
     */
    public static void setDefault(StardustTracer tracer) {
        defaultTracer = tracer;
    }

    /**
     * Returns the configured server URL.
     *
     * @return server URL
     */
    public String getServer() {
        return server;
    }

    /**
     * Returns the application id.
     *
     * @return application id
     */
    public String getAppId() {
        return appId;
    }

    /**
     * Returns the application name.
     *
     * @return application name
     */
    public String getAppName() {
        return appName;
    }

    /**
     * Returns the reported version.
     *
     * @return version
     */
    public String getVersion() {
        return version;
    }

    /**
     * Returns the client id, generating {@code ip@pid} on first use.
     *
     * @return client id
     */
    public String getClientId() {
        String value = clientId;
        if (value == null) {
            value = AppMetrics.localIp() + "@" + AppMetrics.pid();
            clientId = value;
        }

        return value;
    }

    /**
     * Returns the current report period in seconds.
     *
     * @return report period
     */
    public int getPeriod() {
        return period;
    }

    /**
     * Returns the heartbeat period in seconds.
     *
     * @return heartbeat period
     */
    public int getHeartbeatPeriod() {
        return heartbeatPeriod;
    }

    /**
     * Returns whether application metrics are included in reports.
     *
     * @return metric collection flag
     */
    public boolean getEnableMeter() {
        return enableMeter;
    }

    /**
     * Returns the maximum tag length.
     *
     * @return maximum tag length
     */
    public int getMaxTagLength() {
        return maxTagLength;
    }

    /**
     * Returns whether debug logging is enabled.
     *
     * @return debug flag
     */
    public boolean isDebug() {
        return debug;
    }

    /**
     * Returns whether the tracer has been started and not stopped.
     *
     * @return running flag
     */
    public boolean isRunning() {
        return running;
    }

    /**
     * Starts the tracer: logs in and schedules heartbeat and report tasks.
     *
     * <p>Login failures are logged (when debug is enabled) and retried by the scheduled
     * tasks; this method never throws.</p>
     *
     * @return this tracer, for fluent chaining
     */
    public synchronized StardustTracer start() {
        if (running) return this;

        running = true;
        scheduler = Executors.newSingleThreadScheduledExecutor(new ThreadFactory() {
            @Override
            public Thread newThread(Runnable runnable) {
                final Thread thread = new Thread(runnable, "stardust-tracer");
                thread.setDaemon(true);
                return thread;
            }
        });

        try {
            ensureToken();
        } catch (Throwable e) {
            log("Stardust login failed", e);
        }

        scheduleReport();
        scheduleHeartbeat();

        return this;
    }

    /**
     * Stops the tracer: flushes pending spans, cancels scheduled tasks and shuts down the
     * scheduler. Calling this method more than once is safe and it never throws.
     */
    public synchronized void stop() {
        if (!running) return;

        running = false;
        try {
            flush();
        } catch (Throwable e) {
            log("Stardust flush on stop failed", e);
        }

        synchronized (scheduleLock) {
            if (reportTask != null) {
                reportTask.cancel(false);
                reportTask = null;
            }
            if (heartbeatTask != null) {
                heartbeatTask.cancel(false);
                heartbeatTask = null;
            }
        }

        if (scheduler != null) {
            scheduler.shutdownNow();
            try {
                scheduler.awaitTermination(2, TimeUnit.SECONDS);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            scheduler = null;
        }

        spanStack.remove();
        invalidateToken();
    }

    /**
     * Flushes all completed spans to the server synchronously.
     *
     * <p>This method never throws; failures are logged only when debug is enabled.</p>
     *
     * @return {@code true} when a report was sent successfully
     */
    public boolean flush() {
        synchronized (reportLock) {
            try {
                final Map<String, Object> body = buildReport();
                if (body == null) return false;

                final String json = Json.stringify(body);
                final boolean gzip = json.getBytes(UTF8).length > REPORT_GZIP_THRESHOLD;
                final Map<String, Object> data = postWithRetry(gzip ? "/Trace/ReportRaw" : "/Trace/Report", body, gzip);
                applyResponse(TraceResponse.fromMap(data));

                return true;
            } catch (Throwable e) {
                log("Stardust report failed", e);
                return false;
            }
        }
    }

    /**
     * Sends a heartbeat synchronously, including process metrics.
     *
     * @return {@code true} when the heartbeat succeeded
     */
    public boolean pingNow() {
        synchronized (pingLock) {
            try {
                final AppInfo info = metrics.collect();
                final Map<String, Object> data = postWithRetry("/App/Ping", info.toMap(), false);
                final String renewed = stringValue(data.get("Token"));
                if (renewed.length() > 0) {
                    token = renewed;
                    final long expire = jwtExpireAt(renewed);
                    if (expire > 0) tokenExpireAt = expire;
                }

                return true;
            } catch (Throwable e) {
                log("Stardust heartbeat failed", e);
                return false;
            }
        }
    }

    /**
     * Creates a new span, using the current thread's active span as parent.
     *
     * <p>The returned span must be finished; try-with-resources is the simplest option.
     * Excluded operation names return a no-op span.</p>
     *
     * @param name operation name
     * @return span
     */
    public Span newSpan(String name) {
        final String spanName = name == null || name.length() == 0 ? "unknown" : name;
        if (isExcluded(spanName)) return Span.noop(spanName);

        final Deque<Span> stack = spanStack.get();
        final Span parent = stack.peek();
        final String traceId = parent != null ? parent.getTraceId() : newTraceId();
        final String parentId = parent != null ? parent.getId() : null;
        final Span span = new Span(this, newSpanId(), parentId, traceId, spanName, System.currentTimeMillis());
        stack.push(span);

        return span;
    }

    /**
     * Creates a new span from an upstream W3C trace context.
     *
     * <p>The trace id is reused and the upstream span id becomes the parent id, which
     * connects this server span to the caller's trace.</p>
     *
     * @param name operation name
     * @param context upstream trace context, may be {@code null}
     * @return span
     */
    public Span newSpan(String name, TraceContext context) {
        if (context == null) return newSpan(name);

        final String spanName = name == null || name.length() == 0 ? "unknown" : name;
        if (isExcluded(spanName)) return Span.noop(spanName);

        final Span span = new Span(this, newSpanId(), context.getSpanId(), context.getTraceId(), spanName, System.currentTimeMillis());
        spanStack.get().push(span);

        return span;
    }

    /**
     * Returns the current thread's active span.
     *
     * @return active span, or {@code null} when no span is active
     */
    public Span currentSpan() {
        final Deque<Span> stack = spanStack.get();
        final Span span = stack.peek();
        if (span == null) spanStack.remove();

        return span;
    }

    /**
     * Creates a new span with an explicit parent id.
     *
     * <p>When the parent id belongs to an active span on the current thread, the trace id
     * is inherited; otherwise a new trace is started (useful for cross-thread propagation).</p>
     *
     * @param name operation name
     * @param parentId parent span id, may be {@code null}
     * @return span
     */
    public Span newSpan(String name, String parentId) {
        if (parentId == null || parentId.length() == 0) return newSpan(name);

        final String spanName = name == null || name.length() == 0 ? "unknown" : name;
        if (isExcluded(spanName)) return Span.noop(spanName);

        final Deque<Span> stack = spanStack.get();
        String traceId = null;
        for (Span active : stack) {
            if (parentId.equals(active.getId())) {
                traceId = active.getTraceId();
                break;
            }
        }
        if (traceId == null) traceId = newTraceId();

        final Span span = new Span(this, newSpanId(), parentId, traceId, spanName, System.currentTimeMillis());
        stack.push(span);

        return span;
    }

    /**
     * Returns whether an operation name is excluded from tracing.
     *
     * @param name operation name or request URI
     * @return {@code true} when excluded
     */
    public boolean isExcluded(String name) {
        if (name == null) return false;

        final String value = name.trim();
        if (value.length() == 0) return false;

        final int space = value.indexOf(' ');
        final String path = space >= 0 ? value.substring(space + 1).trim() : value;

        synchronized (excludes) {
            for (String exclude : excludes) {
                if (exclude == null || exclude.length() == 0) continue;

                if (matches(exclude, value) || matches(exclude, path)) return true;
            }
        }

        return false;
    }

    /**
     * Adds an operation name to the exclusion list.
     *
     * @param name operation name or URI prefix; supports {@code *} and {@code ?} wildcards
     */
    public void addExclude(String name) {
        if (name == null || name.trim().length() == 0) return;

        synchronized (excludes) {
            excludes.add(name.trim());
        }
    }

    String truncateTag(String tag) {
        if (tag == null) return "";

        final int limit = maxTagLength;
        if (limit > 0 && tag.length() > limit) return tag.substring(0, limit);

        return tag;
    }

    void endSpan(Span span) {
        if (span == null || span.isNoop()) return;

        final Deque<Span> stack = spanStack.get();
        while (!stack.isEmpty()) {
            final Span top = stack.pop();
            if (top == span) break;
        }

        synchronized (builders) {
            SpanBuilder builder = builders.get(span.getName());
            if (builder == null) {
                builder = new SpanBuilder(span.getName(), maxSamples, maxErrors);
                builders.put(span.getName(), builder);
            }
            builder.add(span);
        }

        if (stack.isEmpty()) spanStack.remove();
    }

    void log(String message, Throwable error) {
        if (!debug) return;

        System.err.println("[Stardust] " + message + (error == null ? "" : ": " + error));
        if (error != null) error.printStackTrace(System.err);
    }

    private Map<String, Object> buildReport() {
        final List<Map<String, Object>> list = new ArrayList<Map<String, Object>>();
        synchronized (builders) {
            if (builders.isEmpty()) return null;

            for (SpanBuilder builder : builders.values()) {
                list.add(builder.toMap());
            }
            builders.clear();
        }

        final Map<String, Object> body = new LinkedHashMap<String, Object>();
        body.put("AppId", appId);
        body.put("AppName", appName);
        body.put("ClientId", getClientId());
        body.put("Version", version);
        if (enableMeter) body.put("Info", metrics.collect().toMap());
        body.put("Builders", list);

        return body;
    }

    private Map<String, Object> postWithRetry(String path, Map<String, Object> body, boolean gzip) {
        final String json = Json.stringify(body);
        try {
            return transport.post(path, ensureToken(), json, gzip);
        } catch (StardustException e) {
            if (e.getCode() != 401) throw e;

            log("Stardust token rejected, re-login and retry", null);
            invalidateToken();
            final String renewed = ensureToken();

            return transport.post(path, renewed, json, gzip);
        }
    }

    private String ensureToken() {
        synchronized (loginLock) {
            final String current = token;
            final long now = System.currentTimeMillis();
            if (current != null && current.length() > 0 && tokenExpireAt - now > TOKEN_REFRESH_WINDOW_MS) return current;

            login();
            return token;
        }
    }

    private void login() {
        final Map<String, Object> body = new LinkedHashMap<String, Object>();
        body.put("AppId", appId);
        body.put("Secret", secret);
        body.put("ClientId", getClientId());
        body.put("AppName", appName);
        body.put("Version", version);
        body.put("IP", AppMetrics.localIp());

        final Map<String, Object> data = transport.post("/App/Login", null, Json.stringify(body), false);
        final String value = stringValue(data.get("Token"));
        if (value.length() == 0) throw new StardustException(-1, "Login response did not contain a Token");

        final int expire = intValue(data.get("Expire"), 7200);
        token = value;
        tokenExpireAt = System.currentTimeMillis() + Math.max(1, expire) * 1000L;

        final String code = stringValue(data.get("Code"));
        if (code.length() > 0) appId = code;
        final String issuedSecret = stringValue(data.get("Secret"));
        if (issuedSecret.length() > 0) secret = issuedSecret;
    }

    private void invalidateToken() {
        token = null;
        tokenExpireAt = 0L;
    }

    private void applyResponse(TraceResponse response) {
        if (response == null) return;

        boolean reschedule = false;
        if (response.getPeriod() > 0 && response.getPeriod() != period) {
            period = response.getPeriod();
            reschedule = true;
        }
        if (response.getMaxSamples() >= 0) maxSamples = response.getMaxSamples();
        if (response.getMaxErrors() >= 0) maxErrors = response.getMaxErrors();
        if (response.getMaxTagLength() > 0) maxTagLength = response.getMaxTagLength();
        if (response.getEnableMeter() != null) enableMeter = response.getEnableMeter().booleanValue();

        final String[] serverExcludes = response.getExcludes();
        if (serverExcludes != null) {
            synchronized (excludes) {
                for (String exclude : serverExcludes) {
                    if (exclude != null && exclude.trim().length() > 0) excludes.add(exclude.trim());
                }
            }
        }

        applyBuilderLimits();

        if (reschedule && running) scheduleReport();
    }

    private void applyBuilderLimits() {
        synchronized (builders) {
            for (SpanBuilder builder : builders.values()) {
                builder.setMaxSamples(maxSamples);
                builder.setMaxErrors(maxErrors);
            }
        }
    }

    private void scheduleReport() {
        synchronized (scheduleLock) {
            if (!running || scheduler == null) return;

            if (reportTask != null) reportTask.cancel(false);
            final long delay = Math.max(1, period) * 1000L;
            reportTask = scheduler.scheduleWithFixedDelay(new Runnable() {
                @Override
                public void run() {
                    if (!running) return;

                    try {
                        flush();
                    } catch (Throwable e) {
                        log("Stardust scheduled report failed", e);
                    }
                }
            }, delay, delay, TimeUnit.MILLISECONDS);
        }
    }

    private void scheduleHeartbeat() {
        synchronized (scheduleLock) {
            if (!running || scheduler == null) return;

            if (heartbeatTask != null) heartbeatTask.cancel(false);
            final long delay = Math.max(1, heartbeatPeriod) * 1000L;
            heartbeatTask = scheduler.scheduleWithFixedDelay(new Runnable() {
                @Override
                public void run() {
                    if (!running) return;

                    try {
                        pingNow();
                    } catch (Throwable e) {
                        log("Stardust scheduled heartbeat failed", e);
                    }
                }
            }, delay, delay, TimeUnit.MILLISECONDS);
        }
    }

    private static String newTraceId() {
        return TraceContext.newTraceId();
    }

    private static String newSpanId() {
        return TraceContext.newSpanId();
    }

    private static boolean matches(String pattern, String value) {
        if (value == null) return false;
        if (pattern.indexOf('*') < 0 && pattern.indexOf('?') < 0) {
            return value.equals(pattern) || value.startsWith(pattern) || value.endsWith(" " + pattern);
        }

        return wildcardMatch(pattern, 0, value, 0);
    }

    private static boolean wildcardMatch(String pattern, int pi, String value, int vi) {
        while (pi < pattern.length()) {
            final char pc = pattern.charAt(pi);
            if (pc == '*') {
                while (pi < pattern.length() && pattern.charAt(pi) == '*') pi++;
                if (pi == pattern.length()) return true;
                for (int i = vi; i <= value.length(); i++) {
                    if (wildcardMatch(pattern, pi, value, i)) return true;
                }

                return false;
            }
            if (vi >= value.length()) return false;
            if (pc != '?' && pc != value.charAt(vi)) return false;
            pi++;
            vi++;
        }

        return vi == value.length();
    }

    private static String stringValue(Object value) {
        return value == null ? "" : value.toString();
    }

    private static int intValue(Object value, int defaultValue) {
        if (value instanceof Number) return ((Number) value).intValue();
        if (value != null) {
            try {
                return Integer.parseInt(value.toString().trim());
            } catch (NumberFormatException ignored) {
                // Use default.
            }
        }

        return defaultValue;
    }

    private static long jwtExpireAt(String jwt) {
        try {
            final String[] parts = jwt.split("\\.");
            if (parts.length < 2) return 0L;

            final byte[] payload = Base64.getUrlDecoder().decode(parts[1]);
            final Map<String, Object> claims = Json.parseObject(new String(payload, UTF8));
            final Object exp = claims.get("exp");
            if (exp instanceof Number) return ((Number) exp).longValue() * 1000L;
        } catch (Throwable ignored) {
            // JWT parsing is optional; keep the previous expiry.
        }

        return 0L;
    }
}
