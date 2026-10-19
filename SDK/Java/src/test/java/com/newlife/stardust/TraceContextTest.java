package com.newlife.stardust;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class TraceContextTest {

    @AfterEach
    void clearDefault() {
        StardustTracer.setDefault(null);
    }

    @Test
    @DisplayName("解析合法的 traceparent 并提取采样标记")
    void parseValidTraceparent() {
        final TraceContext context = TraceContext.parse(
                "00-4BF92F3577B34DA6A3CE929D0E0E4736-00F067AA0BA902B7-01");

        assertNotNull(context);
        assertEquals("4bf92f3577b34da6a3ce929d0e0e4736", context.getTraceId());
        assertEquals("00f067aa0ba902b7", context.getSpanId());
        assertTrue(context.isSampled());
        assertEquals("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", context.format());
    }

    @Test
    @DisplayName("兼容 .NET 历史实现的短 TraceId")
    void parseAcceptsShorterTraceId() {
        final TraceContext context = TraceContext.parse("00-1234567890abcdef-00f067aa0ba902b7-00");

        assertNotNull(context);
        assertEquals("1234567890abcdef", context.getTraceId());
        assertFalse(context.isSampled());
        assertEquals("00-1234567890abcdef-00f067aa0ba902b7-00", TraceContext.format(context));
    }

    @Test
    @DisplayName("拒绝非法 traceparent")
    void parseRejectsInvalidValues() {
        assertNull(TraceContext.parse(null));
        assertNull(TraceContext.parse(""));
        assertNull(TraceContext.parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7"));
        assertNull(TraceContext.parse("ff-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"));
        assertNull(TraceContext.parse("00-00000000000000000000000000000000-00f067aa0ba902b7-01"));
        assertNull(TraceContext.parse("00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01"));
        assertNull(TraceContext.parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b-01"));
        assertNull(TraceContext.parse("00-not-hex-00f067aa0ba902b7-01"));
        assertNull(TraceContext.parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01-extra"));
        assertEquals("", TraceContext.format(null));
    }

    @Test
    @DisplayName("生成的 TraceId 为 32 位、SpanId 为 16 位小写十六进制")
    void generatedIdsHaveExpectedLength() {
        for (int i = 0; i < 20; i++) {
            final String traceId = TraceContext.newTraceId();
            final String spanId = TraceContext.newSpanId();

            assertEquals(32, traceId.length());
            assertEquals(16, spanId.length());
            assertTrue(traceId.matches("[0-9a-f]{32}"));
            assertTrue(spanId.matches("[0-9a-f]{16}"));
            assertFalse(traceId.matches("0+"));
            assertFalse(spanId.matches("0+"));
        }
    }

    @Test
    @DisplayName("构造器校验标识长度与字符集")
    void constructorValidatesIds() {
        assertThrows(IllegalArgumentException.class, () -> new TraceContext("xyz", "00f067aa0ba902b7", true));
        assertThrows(IllegalArgumentException.class, () -> new TraceContext("4bf92f3577b34da6a3ce929d0e0e4736", "short", true));
    }

    @Test
    @DisplayName("Tracer 使用上游上下文创建入口 Span 并维护当前 Span")
    void tracerCreatesSpanFromContext() {
        final TestTracer tracer = new TestTracer();
        final TraceContext context = TraceContext.parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        final Span root = tracer.newSpan("GET /orders", context);
        try {
            assertEquals("4bf92f3577b34da6a3ce929d0e0e4736", root.getTraceId());
            assertEquals("00f067aa0ba902b7", root.getParentId());
            assertEquals(16, root.getId().length());
            assertTrue(root.getId().matches("[0-9a-f]{16}"));
            assertEquals(root, tracer.currentSpan());

            try (Span child = tracer.newSpan("db:sqlite:select:orders")) {
                assertEquals(root.getTraceId(), child.getTraceId());
                assertEquals(root.getId(), child.getParentId());
                assertEquals(child, tracer.currentSpan());
            }
            assertEquals(root, tracer.currentSpan());
        } finally {
            root.finish();
        }
        assertNull(tracer.currentSpan());
    }

    @Test
    @DisplayName("无上游上下文时生成 32 位 TraceId，默认自动埋点开启")
    void tracerGeneratesLongTraceIdAndDefaults() {
        final TestTracer tracer = new TestTracer();
        try (Span span = tracer.newSpan("root")) {
            assertEquals(32, span.getTraceId().length());
            assertTrue(span.getTraceId().matches("[0-9a-f]{32}"));
            assertNull(span.getParentId());
        }

        assertTrue(tracer.isAutoInstrument());
        // 构造时自动成为默认实例，无需额外调用 setDefault
        assertEquals(tracer, StardustTracer.getDefault());
        assertFalse(tracer.setAutoInstrument(false).isAutoInstrument());

        StardustTracer.setDefault(null);
        assertNull(StardustTracer.getDefault());
    }
}
