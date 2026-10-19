package com.newlife.stardust;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpServer;

import java.io.IOException;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ThreadFactory;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class StardustHttpTest {

    private HttpServer server;
    private ExecutorService executor;
    private String baseUrl;
    private volatile String traceparent;

    @BeforeEach
    void startServer() throws IOException {
        traceparent = null;
        server = HttpServer.create(new InetSocketAddress(InetAddress.getByName("127.0.0.1"), 0), 0);
        executor = Executors.newSingleThreadExecutor(new ThreadFactory() {
            @Override
            public Thread newThread(Runnable runnable) {
                final Thread thread = new Thread(runnable, "stardust-http-test");
                thread.setDaemon(true);
                return thread;
            }
        });
        server.setExecutor(executor);
        server.createContext("/ok", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                traceparent = exchange.getRequestHeaders().getFirst(TraceContext.TRACEPARENT_HEADER);
                respond(exchange, 200);
            }
        });
        server.createContext("/fail", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                traceparent = exchange.getRequestHeaders().getFirst(TraceContext.TRACEPARENT_HEADER);
                respond(exchange, 500);
            }
        });
        server.start();
        baseUrl = "http://127.0.0.1:" + server.getAddress().getPort();
    }

    @AfterEach
    void stopServer() {
        StardustTracer.setDefault(null);
        if (server != null) server.stop(0);
        if (executor != null) executor.shutdownNow();
    }

    @Test
    @DisplayName("execute 创建出站 Span 并注入 traceparent")
    void createsClientSpanAndInjectsTraceparent() throws Exception {
        final TestTracer tracer = new TestTracer();
        StardustTracer.setDefault(tracer);

        final String url = baseUrl + "/ok?x=1";
        final HttpURLConnection connection = StardustHttp.execute("GET", url);
        try {
            assertEquals(200, connection.getResponseCode());
        } finally {
            connection.disconnect();
        }

        assertEquals(1, tracer.spans.size());
        final Span span = tracer.last();
        assertEquals("GET 127.0.0.1:" + server.getAddress().getPort() + "/ok", span.getName());
        assertEquals(url, span.getTag());
        assertEquals("", span.getError());
        assertTrue(span.isFinished());
        assertEquals("00-" + span.getTraceId() + "-" + span.getId() + "-01", traceparent);
    }

    @Test
    @DisplayName("4xx/5xx 响应标记出站 Span 为错误")
    void marksHttpError() throws Exception {
        final TestTracer tracer = new TestTracer();
        StardustTracer.setDefault(tracer);

        final HttpURLConnection connection = StardustHttp.execute("GET", baseUrl + "/fail");
        try {
            assertEquals(500, connection.getResponseCode());
        } finally {
            connection.disconnect();
        }

        assertEquals("HTTP 500", tracer.last().getError());
    }

    @Test
    @DisplayName("open 仅注入当前 Span 上下文，不额外创建 Span")
    void openInjectsCurrentSpanContext() throws Exception {
        final TestTracer tracer = new TestTracer();
        StardustTracer.setDefault(tracer);

        try (Span span = tracer.newSpan("manual")) {
            final HttpURLConnection connection = StardustHttp.open(baseUrl + "/ok");
            try {
                assertEquals(200, connection.getResponseCode());
            } finally {
                connection.disconnect();
            }

            assertEquals("00-" + span.getTraceId() + "-" + span.getId() + "-01", traceparent);
        }

        assertEquals(1, tracer.spans.size());
    }

    @Test
    @DisplayName("关闭自动埋点后不创建 Span 也不注入头")
    void optOutSkipsSpanAndHeader() throws Exception {
        final TestTracer tracer = new TestTracer();
        tracer.setAutoInstrument(false);
        StardustTracer.setDefault(tracer);

        final HttpURLConnection connection = StardustHttp.execute("GET", baseUrl + "/ok");
        try {
            assertEquals(200, connection.getResponseCode());
        } finally {
            connection.disconnect();
        }

        assertEquals(0, tracer.spans.size());
        assertNull(traceparent);
    }

    private static void respond(HttpExchange exchange, int status) throws IOException {
        exchange.sendResponseHeaders(status, -1);
        exchange.close();
    }
}
