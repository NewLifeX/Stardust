package com.newlife.stardust;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpServer;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.nio.charset.Charset;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.zip.GZIPInputStream;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.fail;

class TracerIntegrationTest {

    private static final Charset UTF8 = Charset.forName("UTF-8");
    private static final String CONFIG_RESPONSE = "{\"code\":0,\"data\":{"
            + "\"Period\":15,\"MaxSamples\":5,\"MaxErrors\":7,\"Timeout\":5000,"
            + "\"MaxTagLength\":64,\"RequestTagLength\":128,\"EnableMeter\":true,"
            + "\"Excludes\":[\"/health\",\"/actuator/*\"]}}";

    private HttpServer server;
    private ExecutorService serverExecutor;
    private String baseUrl;

    private final AtomicInteger loginCount = new AtomicInteger();
    private final AtomicInteger pingCount = new AtomicInteger();
    private final AtomicInteger reportCount = new AtomicInteger();
    private final AtomicInteger rawCount = new AtomicInteger();
    private final AtomicBoolean failFirstReport = new AtomicBoolean();
    private final List<String> loginBodies = Collections.synchronizedList(new ArrayList<String>());
    private final List<String> reportQueries = Collections.synchronizedList(new ArrayList<String>());

    private volatile String lastPingBody;
    private volatile String lastPingQuery;
    private volatile String lastReportBody;
    private volatile String lastReportContentType;
    private volatile String lastRawBody;
    private volatile String lastRawContentType;
    private volatile int lastRawCompressedLength;
    private volatile int loginExpireSeconds = 7200;

    @BeforeEach
    void startServer() throws IOException {
        loginCount.set(0);
        pingCount.set(0);
        reportCount.set(0);
        rawCount.set(0);
        failFirstReport.set(false);
        loginBodies.clear();
        reportQueries.clear();
        lastPingBody = null;
        lastPingQuery = null;
        lastReportBody = null;
        lastReportContentType = null;
        lastRawBody = null;
        lastRawContentType = null;
        lastRawCompressedLength = 0;
        loginExpireSeconds = 7200;

        server = HttpServer.create(new InetSocketAddress(InetAddress.getByName("127.0.0.1"), 0), 0);
        serverExecutor = Executors.newCachedThreadPool(new ThreadFactory() {
            @Override
            public Thread newThread(Runnable runnable) {
                final Thread thread = new Thread(runnable, "stardust-mock-server");
                thread.setDaemon(true);
                return thread;
            }
        });
        server.setExecutor(serverExecutor);

        server.createContext("/App/Login", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                final int count = loginCount.incrementAndGet();
                loginBodies.add(read(exchange.getRequestBody()));
                respond(exchange, 200, "{\"code\":0,\"data\":{\"Token\":\"token-" + count
                        + "\",\"Expire\":" + loginExpireSeconds
                        + ",\"Code\":\"testapp\",\"Secret\":\"secret\",\"ServerTime\":1700000000000}}");
            }
        });
        server.createContext("/App/Ping", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                pingCount.incrementAndGet();
                lastPingQuery = exchange.getRequestURI().getQuery();
                lastPingBody = read(exchange.getRequestBody());
                respond(exchange, 200, "{\"code\":0,\"data\":{}}");
            }
        });
        server.createContext("/Trace/ReportRaw", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                handleReport(exchange, true);
            }
        });
        server.createContext("/Trace/Report", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                handleReport(exchange, false);
            }
        });

        server.start();
        baseUrl = "http://127.0.0.1:" + server.getAddress().getPort();
    }

    @AfterEach
    void stopServer() {
        if (server != null) server.stop(0);
        if (serverExecutor != null) serverExecutor.shutdownNow();
    }

    @Test
    @DisplayName("登录请求包含版本与应用信息")
    void loginSendsVersionAndIp() {
        final StardustTracer tracer = newTracer();
        try {
            tracer.start();
            assertEquals(1, loginCount.get());
            assertEquals(1, loginBodies.size());

            final Map<String, Object> login = Json.parseObject(loginBodies.get(0));
            assertEquals("testapp", login.get("AppId"));
            assertEquals("secret", login.get("Secret"));
            assertEquals("demo-app", login.get("AppName"));
            assertEquals("1.2.3", login.get("Version"));
            assertNotNull(login.get("ClientId"));
            assertTrue(login.get("ClientId").toString().endsWith("@" + AppMetrics.pid()));
            assertTrue(String.valueOf(login.get("IP")).length() > 0);
        } finally {
            tracer.stop();
            tracer.stop();
        }
        assertEquals(1, loginCount.get());
    }

    @Test
    @DisplayName("init 一行创建并启动追踪器")
    void initStartsAndLogsIn() {
        final StardustTracer tracer = StardustTracer.init(baseUrl, "TestApp", "secret");
        try {
            assertTrue(tracer.isRunning());
            assertEquals(1, loginCount.get());
        } finally {
            tracer.stop();
        }
    }

    @Test
    @DisplayName("心跳报文包含 Version 与进程信息")
    void heartbeatContainsMetrics() {
        final StardustTracer tracer = newTracer();
        try {
            tracer.start();
            assertTrue(tracer.pingNow());
            assertEquals(1, pingCount.get());
            assertNotNull(lastPingQuery);
            assertTrue(lastPingQuery.startsWith("Token="));

            final Map<String, Object> ping = Json.parseObject(lastPingBody);
            assertEquals("1.2.3", ping.get("Version"));
            assertEquals(AppMetrics.pid(), ((Number) ping.get("Id")).intValue());
            assertTrue(((Number) ping.get("WorkingSet")).longValue() > 0);
            assertTrue(((Number) ping.get("HeapSize")).longValue() > 0);
            assertTrue(((Number) ping.get("Threads")).intValue() > 0);
            assertTrue(((Number) ping.get("Time")).longValue() > 0);
            assertTrue(String.valueOf(ping.get("StartTime")).contains("T"));
            assertTrue(String.valueOf(ping.get("IP")).length() > 0);
        } finally {
            tracer.stop();
        }
    }

    @Test
    @DisplayName("上报包含版本、应用信息与埋点，并应用服务端采样参数")
    @SuppressWarnings("unchecked")
    void reportAndApplyResponse() {
        final StardustTracer tracer = newTracer();
        try {
            tracer.start();

            final String rootId;
            final String childId;
            try (Span root = tracer.newSpan("order.create")) {
                root.setTag("orderId=1");
                rootId = root.getId();
                try (Span child = tracer.newSpan("db.insert")) {
                    child.setTag("insert into orders");
                    childId = child.getId();
                }
            }

            assertTrue(tracer.flush());
            assertEquals(1, reportCount.get());
            if (rawCount.get() == 1) {
                assertEquals("application/x-gzip", lastReportContentType);
            } else {
                assertEquals(0, rawCount.get());
                assertEquals("application/json", lastReportContentType);
            }
            assertNotNull(reportQueries.get(0));
            assertTrue(reportQueries.get(0).startsWith("Token="));

            final Map<String, Object> report = Json.parseObject(lastReportBody);
            assertEquals("testapp", report.get("AppId"));
            assertEquals("1.2.3", report.get("Version"));
            assertNotNull(report.get("Info"));

            final Map<String, Object> info = (Map<String, Object>) report.get("Info");
            assertTrue(((Number) info.get("WorkingSet")).longValue() > 0);
            assertTrue(((Number) info.get("HeapSize")).longValue() > 0);
            assertTrue(((Number) info.get("Threads")).intValue() > 0);
            assertTrue(((Number) info.get("Time")).longValue() > 0);

            final List<Map<String, Object>> builders = (List<Map<String, Object>>) report.get("Builders");
            assertEquals(2, builders.size());

            final Map<String, Object> rootBuilder = findBuilder(builders, "order.create");
            final Map<String, Object> childBuilder = findBuilder(builders, "db.insert");
            assertEquals(1L, ((Number) rootBuilder.get("Total")).longValue());
            assertEquals(1L, ((Number) childBuilder.get("Total")).longValue());

            final Map<String, Object> rootSample = (Map<String, Object>) ((List<?>) rootBuilder.get("Samples")).get(0);
            final Map<String, Object> childSample = (Map<String, Object>) ((List<?>) childBuilder.get("Samples")).get(0);
            assertEquals(rootId, rootSample.get("Id"));
            assertNull(rootSample.get("ParentId"));
            assertEquals(childId, childSample.get("Id"));
            assertEquals(rootId, childSample.get("ParentId"));
            assertEquals(rootSample.get("TraceId"), childSample.get("TraceId"));
            assertEquals(32, String.valueOf(rootSample.get("TraceId")).length());
            assertEquals(16, String.valueOf(rootSample.get("Id")).length());

            assertEquals(15, tracer.getPeriod());
            assertTrue(tracer.isExcluded("/health"));
            assertTrue(tracer.isExcluded("/actuator/metrics"));
            assertTrue(tracer.isExcluded("/App/Ping"));
            assertFalse(tracer.isExcluded("/api/orders"));

            try (Span truncated = tracer.newSpan("tag.test")) {
                truncated.setTag(repeat('x', 100));
                assertEquals(64, truncated.getTag().length());
            }
        } finally {
            tracer.stop();
        }
    }

    @Test
    @DisplayName("报文大于 1024 字节时 gzip 压缩并走 ReportRaw")
    @SuppressWarnings("unchecked")
    void gzipLargeReport() {
        final StardustTracer tracer = newTracer().setMaxSamples(10).setMaxErrors(10);
        try {
            tracer.start();

            final String longTag = repeat('x', 300);
            for (int i = 0; i < 40; i++) {
                try (Span span = tracer.newSpan("operation-" + i)) {
                    span.setTag(longTag);
                }
            }

            assertTrue(tracer.flush());
            assertEquals(1, rawCount.get());
            assertEquals(1, reportCount.get());
            assertEquals("application/x-gzip", lastRawContentType);
            assertTrue(lastRawCompressedLength > 0);

            final Map<String, Object> raw = Json.parseObject(lastRawBody);
            assertEquals(40, ((List<?>) raw.get("Builders")).size());
            assertEquals("1.2.3", raw.get("Version"));
            assertNotNull(raw.get("Info"));
        } finally {
            tracer.stop();
        }
    }

    @Test
    @DisplayName("上报返回 401 时重新登录并重试一次")
    void reLoginOn401() {
        failFirstReport.set(true);
        final StardustTracer tracer = newTracer();
        try {
            tracer.start();
            assertEquals(1, loginCount.get());

            try (Span span = tracer.newSpan("op.retry")) {
                span.setTag("retry");
            }

            assertTrue(tracer.flush());
            assertEquals(2, reportCount.get());
            assertEquals(2, loginCount.get());
            assertEquals(2, reportQueries.size());
            assertTrue(reportQueries.get(1).contains("token-2"));

            final Map<String, Object> report = Json.parseObject(lastReportBody);
            assertEquals("1.2.3", report.get("Version"));
        } finally {
            tracer.stop();
        }
    }

    @Test
    @DisplayName("令牌临近过期时重新登录")
    void reLoginWhenTokenExpiring() {
        loginExpireSeconds = 60;
        final StardustTracer tracer = newTracer();
        try {
            tracer.start();
            assertEquals(1, loginCount.get());

            assertTrue(tracer.pingNow());
            assertEquals(2, loginCount.get());
            assertEquals(1, pingCount.get());
        } finally {
            tracer.stop();
        }
    }

    @Test
    @DisplayName("小报文走 Report 接口且包含版本")
    void smallReportUsesJson() {
        final StardustTracer tracer = newTracer().setEnableMeter(false).setClientId("c");
        try {
            tracer.start();
            try (Span span = tracer.newSpan("small.op")) {
                span.setTag("t");
            }

            assertTrue(tracer.flush());
            assertEquals(1, reportCount.get());
            assertEquals(0, rawCount.get());
            assertEquals("application/json", lastReportContentType);

            final Map<String, Object> report = Json.parseObject(lastReportBody);
            assertEquals("1.2.3", report.get("Version"));
            assertNull(report.get("Info"));
            assertEquals(1, ((List<?>) report.get("Builders")).size());
        } finally {
            tracer.stop();
        }
    }

    @Test
    @DisplayName("stop 幂等且不阻塞")
    void stopIsIdempotentAndQuick() {
        final StardustTracer tracer = newTracer();
        tracer.start();

        final long begin = System.nanoTime();
        tracer.stop();
        tracer.stop();
        final long elapsedMs = (System.nanoTime() - begin) / 1000000L;

        assertFalse(tracer.isRunning());
        assertTrue(elapsedMs < 5000L, "stop took " + elapsedMs + "ms");
    }

    private StardustTracer newTracer() {
        return new StardustTracer(baseUrl, "testapp", "secret")
                .setAppName("demo-app")
                .setVersion("1.2.3")
                .setPeriod(3600)
                .setHeartbeatPeriod(3600);
    }

    private void handleReport(HttpExchange exchange, boolean raw) throws IOException {
        reportCount.incrementAndGet();
        reportQueries.add(exchange.getRequestURI().getQuery());
        lastReportContentType = exchange.getRequestHeaders().getFirst("Content-Type");

        final byte[] requestBody = readBytes(exchange.getRequestBody());
        if (raw) {
            rawCount.incrementAndGet();
            lastRawContentType = lastReportContentType;
            lastRawCompressedLength = requestBody.length;
            lastRawBody = gunzip(requestBody);
            lastReportBody = lastRawBody;
        } else {
            lastReportBody = new String(requestBody, UTF8);
        }

        if (failFirstReport.compareAndSet(true, false)) {
            respond(exchange, 401, "{\"code\":401,\"message\":\"token expired\"}");
            return;
        }
        respond(exchange, 200, CONFIG_RESPONSE);
    }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> findBuilder(List<Map<String, Object>> builders, String name) {
        for (Map<String, Object> builder : builders) {
            if (name.equals(builder.get("Name"))) return builder;
        }
        fail("Builder not found: " + name);
        return null;
    }

    private static String repeat(char value, int count) {
        final char[] chars = new char[count];
        Arrays.fill(chars, value);
        return new String(chars);
    }

    private static void respond(HttpExchange exchange, int status, String body) throws IOException {
        final byte[] bytes = body.getBytes(UTF8);
        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=utf-8");
        exchange.sendResponseHeaders(status, bytes.length);
        final OutputStream output = exchange.getResponseBody();
        try {
            output.write(bytes);
        } finally {
            output.close();
            exchange.close();
        }
    }

    private static String read(InputStream input) throws IOException {
        return new String(readBytes(input), UTF8);
    }

    private static byte[] readBytes(InputStream input) throws IOException {
        final ByteArrayOutputStream buffer = new ByteArrayOutputStream();
        try {
            final byte[] chunk = new byte[4096];
            int count;
            while ((count = input.read(chunk)) >= 0) {
                buffer.write(chunk, 0, count);
            }
        } finally {
            input.close();
        }
        return buffer.toByteArray();
    }

    private static String gunzip(byte[] compressed) throws IOException {
        final GZIPInputStream input = new GZIPInputStream(new ByteArrayInputStream(compressed));
        try {
            return read(input);
        } finally {
            input.close();
        }
    }
}
