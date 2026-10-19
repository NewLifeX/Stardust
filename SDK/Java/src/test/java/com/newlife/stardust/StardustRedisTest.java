package com.newlife.stardust;

import java.io.IOException;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class StardustRedisTest {

    @Test
    @DisplayName("Redis 命令创建 redis:COMMAND Span 并记录结果")
    void createsRedisSpan() {
        final TestTracer tracer = new TestTracer();
        final AtomicReference<String> value = new AtomicReference<String>();

        final String result = StardustRedis.call(tracer, "get user:1", "user:1", () -> {
            value.set("ok");
            return value.get();
        });

        assertEquals("ok", result);
        final Span span = tracer.last();
        assertEquals("redis:GET", span.getName());
        assertEquals("user:1", span.getTag());
        assertEquals("", span.getError());
        assertTrue(span.isFinished());
    }

    @Test
    @DisplayName("Redis 异常标记 Span 并原样抛出")
    void marksRedisException() {
        final TestTracer tracer = new TestTracer();
        final IllegalStateException failure = new IllegalStateException("redis down");

        final IllegalStateException thrown = assertThrows(IllegalStateException.class,
                () -> StardustRedis.call(tracer, "SET", "key", () -> {
                    throw failure;
                }));

        assertEquals(failure, thrown);
        assertTrue(tracer.last().getError().contains("redis down"));
        assertTrue(tracer.last().isFinished());
    }

    @Test
    @DisplayName("Callable 变体记录受检异常")
    void recordsCheckedException() {
        final TestTracer tracer = new TestTracer();

        assertThrows(IOException.class, () -> StardustRedis.callChecked(tracer, "GET", "key", () -> {
            throw new IOException("timeout");
        }));

        assertTrue(tracer.last().getError().contains("timeout"));
        assertTrue(tracer.last().isFinished());
    }

    @Test
    @DisplayName("关闭自动埋点后直接执行命令")
    void optOutRunsWithoutSpan() {
        final TestTracer tracer = new TestTracer();
        tracer.setAutoInstrument(false);

        assertEquals("ok", StardustRedis.call(tracer, "GET", "key", () -> "ok"));
        assertEquals(0, tracer.spans.size());
    }
}
