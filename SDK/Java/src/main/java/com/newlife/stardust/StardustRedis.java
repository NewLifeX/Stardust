package com.newlife.stardust;

import java.util.Locale;
import java.util.concurrent.Callable;
import java.util.function.Supplier;

/**
 * Generic Redis instrumentation helper.
 *
 * <p>Redis clients do not share a standard interface, so this helper wraps a call in a
 * {@code redis:{COMMAND}} span. Use it around Jedis, Lettuce, Redisson or any other client:</p>
 *
 * <pre>
 * String value = StardustRedis.call(tracer, "GET", "user:1", () -&gt; jedis.get("user:1"));
 * </pre>
 *
 * <p>Use {@link #callChecked(StardustTracer, String, String, Callable)} when the client
 * throws checked exceptions.</p>
 */
public final class StardustRedis {

    private StardustRedis() {
    }

    /**
     * Runs a Redis command with the default tracer.
     *
     * @param command command name, for example {@code GET}
     * @param tag key or argument summary, may be {@code null}
     * @param supplier command body
     * @param <T> result type
     * @return command result
     */
    public static <T> T call(String command, String tag, Supplier<T> supplier) {
        return call(StardustTracer.getDefault(), command, tag, supplier);
    }

    /**
     * Runs a Redis command.
     *
     * @param tracer tracer, may be {@code null}
     * @param command command name, for example {@code GET}
     * @param tag key or argument summary, may be {@code null}
     * @param supplier command body
     * @param <T> result type
     * @return command result
     */
    public static <T> T call(StardustTracer tracer, String command, String tag, Supplier<T> supplier) {
        final Span span = startSpan(tracer, command, tag);
        try {
            final T result = supplier.get();
            finish(span, null);

            return result;
        } catch (RuntimeException e) {
            finish(span, e);
            throw e;
        } catch (Error e) {
            finish(span, e);
            throw e;
        }
    }

    /**
     * Runs a Redis command that may throw a checked exception with the default tracer.
     *
     * @param command command name, for example {@code GET}
     * @param tag key or argument summary, may be {@code null}
     * @param callable command body
     * @param <T> result type
     * @return command result
     * @throws Exception when the command fails
     */
    public static <T> T callChecked(String command, String tag, Callable<T> callable) throws Exception {
        return callChecked(StardustTracer.getDefault(), command, tag, callable);
    }

    /**
     * Runs a Redis command that may throw a checked exception.
     *
     * @param tracer tracer, may be {@code null}
     * @param command command name, for example {@code GET}
     * @param tag key or argument summary, may be {@code null}
     * @param callable command body
     * @param <T> result type
     * @return command result
     * @throws Exception when the command fails
     */
    public static <T> T callChecked(StardustTracer tracer, String command, String tag, Callable<T> callable) throws Exception {
        final Span span = startSpan(tracer, command, tag);
        try {
            final T result = callable.call();
            finish(span, null);

            return result;
        } catch (Exception e) {
            finish(span, e);
            throw e;
        } catch (Error e) {
            finish(span, e);
            throw e;
        }
    }

    private static Span startSpan(StardustTracer tracer, String command, String tag) {
        if (tracer == null || !tracer.isAutoInstrument()) return null;

        Span span = null;
        try {
            span = tracer.newSpan(redisSpanName(command));
            span.setTag(tag);

            return span;
        } catch (Throwable ignored) {
            if (span != null) span.finish();
            return null;
        }
    }

    private static void finish(Span span, Throwable error) {
        if (span == null) return;

        if (error != null) span.setError(error);
        span.finish();
    }

    private static String redisSpanName(String command) {
        String name = command == null ? "" : command.trim();
        if (name.length() == 0) return "redis:UNKNOWN";

        for (int i = 0; i < name.length(); i++) {
            final char c = name.charAt(i);
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') {
                name = name.substring(0, i);
                break;
            }
        }

        return "redis:" + name.toUpperCase(Locale.ROOT);
    }
}
