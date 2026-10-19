package com.newlife.stardust;

import java.util.UUID;

/**
 * W3C Trace Context ({@code traceparent}) for cross-service tracing.
 *
 * <p>The header format is {@code 00-<trace-id>-<span-id>-<flags>}. The SDK generates 32 hex
 * trace ids and 16 hex span ids. When parsing, shorter non-empty hexadecimal trace ids are
 * accepted for compatibility with the .NET implementation.</p>
 *
 * <pre>
 * TraceContext context = TraceContext.parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
 * if (context != null) {
 *     String header = context.format();
 * }
 * </pre>
 */
public final class TraceContext {

    /** HTTP header name of the W3C trace context. */
    public static final String TRACEPARENT_HEADER = "traceparent";

    private static final String VERSION = "00";
    private static final int MAX_TRACE_ID_LENGTH = 32;

    private final String traceId;
    private final String spanId;
    private final boolean sampled;

    /**
     * Creates a trace context.
     *
     * @param traceId trace id, non-empty hexadecimal, at most 32 characters
     * @param spanId span id, exactly 16 hexadecimal characters
     * @param sampled whether the upstream sampled the trace
     * @throws IllegalArgumentException when the ids are not valid
     */
    public TraceContext(String traceId, String spanId, boolean sampled) {
        final String trace = lower(traceId);
        final String span = lower(spanId);
        if (!isHex(trace) || trace.length() > MAX_TRACE_ID_LENGTH || isZero(trace)) {
            throw new IllegalArgumentException("traceId must be a non-zero hexadecimal string of at most " + MAX_TRACE_ID_LENGTH + " characters");
        }
        if (span == null || span.length() != 16 || !isHex(span) || isZero(span)) {
            throw new IllegalArgumentException("spanId must be a non-zero 16 character hexadecimal string");
        }

        this.traceId = trace;
        this.spanId = span;
        this.sampled = sampled;
    }

    /**
     * Parses a {@code traceparent} header value.
     *
     * <p>Returns {@code null} for missing or invalid values; never throws.</p>
     *
     * @param traceparent header value, may be {@code null}
     * @return parsed context or {@code null}
     */
    public static TraceContext parse(String traceparent) {
        if (traceparent == null) return null;

        final String value = traceparent.trim();
        if (value.length() == 0) return null;

        final String[] parts = value.split("-", -1);
        if (parts.length < 4) return null;

        final String version = lower(parts[0]);
        if (version == null || version.length() != 2 || !isHex(version) || "ff".equals(version)) return null;
        if (VERSION.equals(version) && parts.length != 4) return null;

        final String traceId = lower(parts[1]);
        if (traceId == null || traceId.length() == 0 || traceId.length() > MAX_TRACE_ID_LENGTH
                || !isHex(traceId) || isZero(traceId)) {
            return null;
        }

        final String spanId = lower(parts[2]);
        if (spanId == null || spanId.length() != 16 || !isHex(spanId) || isZero(spanId)) return null;

        final String flags = lower(parts[3]);
        if (flags == null || flags.length() != 2 || !isHex(flags)) return null;

        return new TraceContext(traceId, spanId, (Integer.parseInt(flags, 16) & 0x01) != 0);
    }

    /**
     * Formats this context as a {@code traceparent} header value.
     *
     * @return header value, never {@code null}
     */
    public String format() {
        return format(this);
    }

    /**
     * Formats a context as a {@code traceparent} header value.
     *
     * <p>Returns an empty string when the context is {@code null}.</p>
     *
     * @param context trace context, may be {@code null}
     * @return header value, never {@code null}
     */
    public static String format(TraceContext context) {
        if (context == null) return "";

        return VERSION + "-" + context.traceId + "-" + context.spanId + "-" + (context.sampled ? "01" : "00");
    }

    /**
     * Generates a random 32 character lowercase hexadecimal trace id.
     *
     * @return trace id
     */
    public static String newTraceId() {
        while (true) {
            final String id = UUID.randomUUID().toString().replace("-", "");
            if (!isZero(id)) return id;
        }
    }

    /**
     * Generates a random 16 character lowercase hexadecimal span id.
     *
     * @return span id
     */
    public static String newSpanId() {
        while (true) {
            final String id = UUID.randomUUID().toString().replace("-", "").substring(0, 16);
            if (!isZero(id)) return id;
        }
    }

    /**
     * Returns the trace id.
     *
     * @return trace id
     */
    public String getTraceId() {
        return traceId;
    }

    /**
     * Returns the upstream span id.
     *
     * @return span id
     */
    public String getSpanId() {
        return spanId;
    }

    /**
     * Returns whether the upstream sampled the trace.
     *
     * @return sampled flag
     */
    public boolean isSampled() {
        return sampled;
    }

    @Override
    public String toString() {
        return format();
    }

    private static String lower(String value) {
        return value == null ? null : value.trim().toLowerCase();
    }

    private static boolean isHex(String value) {
        if (value == null || value.length() == 0) return false;

        for (int i = 0; i < value.length(); i++) {
            final char c = value.charAt(i);
            if ((c < '0' || c > '9') && (c < 'a' || c > 'f')) return false;
        }

        return true;
    }

    private static boolean isZero(String value) {
        if (value == null || value.length() == 0) return true;

        for (int i = 0; i < value.length(); i++) {
            if (value.charAt(i) != '0') return false;
        }

        return true;
    }
}
