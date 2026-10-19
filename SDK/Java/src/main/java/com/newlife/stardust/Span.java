package com.newlife.stardust;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * A single tracing span.
 *
 * <p>Spans are normally created through {@link StardustTracer#newSpan(String)} and
 * finished automatically by try-with-resources:</p>
 *
 * <pre>
 * try (Span span = tracer.newSpan("db.query")) {
 *     span.setTag("select * from users");
 *     // ... work ...
 * }
 * </pre>
 */
public final class Span implements AutoCloseable {

    private final StardustTracer tracer;
    private final String id;
    private final String parentId;
    private final String traceId;
    private final String name;
    private final long startTime;
    private final boolean noop;

    private long endTime;
    private String tag = "";
    private String error = "";
    private boolean finished;

    Span(StardustTracer tracer, String id, String parentId, String traceId, String name, long startTime) {
        this(tracer, id, parentId, traceId, name, startTime, false);
    }

    private Span(StardustTracer tracer, String id, String parentId, String traceId, String name, long startTime, boolean noop) {
        this.tracer = tracer;
        this.id = id;
        this.parentId = parentId;
        this.traceId = traceId;
        this.name = name;
        this.startTime = startTime;
        this.noop = noop;
    }

    static Span noop(String name) {
        return new Span(null, "", null, null, name, 0L, true);
    }

    /**
     * Returns the span id.
     *
     * @return span id
     */
    public String getId() {
        return id;
    }

    /**
     * Returns the parent span id, or {@code null} for a root span.
     *
     * @return parent span id
     */
    public String getParentId() {
        return parentId;
    }

    /**
     * Returns the trace id shared by all spans in one trace.
     *
     * @return trace id
     */
    public String getTraceId() {
        return traceId;
    }

    /**
     * Returns the operation name.
     *
     * @return operation name
     */
    public String getName() {
        return name;
    }

    /**
     * Returns the span start time in Unix milliseconds.
     *
     * @return start time
     */
    public long getStartTime() {
        return startTime;
    }

    /**
     * Returns the span end time in Unix milliseconds, or 0 when not finished.
     *
     * @return end time
     */
    public long getEndTime() {
        return endTime;
    }

    /**
     * Returns the span duration in milliseconds.
     *
     * @return duration in milliseconds, or 0 when not finished
     */
    public long getDuration() {
        return endTime > startTime ? endTime - startTime : 0L;
    }

    /**
     * Returns the tag attached to the span.
     *
     * @return tag, never {@code null}
     */
    public String getTag() {
        return tag;
    }

    /**
     * Sets the tag. The value is truncated to the configured maximum tag length.
     *
     * @param tag tag text, may be {@code null}
     * @return this span
     */
    public Span setTag(String tag) {
        if (noop) return this;
        this.tag = tracer != null ? tracer.truncateTag(tag) : (tag == null ? "" : tag);

        return this;
    }

    /**
     * Returns the error text.
     *
     * @return error text, never {@code null}
     */
    public String getError() {
        return error;
    }

    /**
     * Marks the span as failed.
     *
     * @param error error text, may be {@code null}
     * @return this span
     */
    public Span setError(String error) {
        if (noop) return this;
        this.error = error == null ? "" : error;

        return this;
    }

    /**
     * Marks the span as failed using an exception.
     *
     * @param error exception, may be {@code null}
     * @return this span
     */
    public Span setError(Throwable error) {
        if (noop) return this;
        this.error = error == null ? "" : error.toString();

        return this;
    }

    /**
     * Returns whether the span has been finished.
     *
     * @return {@code true} when finished
     */
    public boolean isFinished() {
        return finished;
    }

    /**
     * Returns whether this is an excluded/no-op span.
     *
     * @return {@code true} for no-op spans
     */
    public boolean isNoop() {
        return noop;
    }

    /**
     * Finishes the span and records it in its builder. Calling this more than once is safe.
     */
    public void finish() {
        finishAt(System.currentTimeMillis());
    }

    void finishAt(long end) {
        if (finished || noop) return;

        finished = true;
        endTime = Math.max(end, startTime);
        if (tracer != null) tracer.endSpan(this);
    }

    /**
     * Finishes the span.
     */
    @Override
    public void close() {
        finish();
    }

    /**
     * Converts the span to the server sample format.
     *
     * @return sample map
     */
    public Map<String, Object> toMap() {
        final Map<String, Object> map = new LinkedHashMap<String, Object>();
        map.put("Id", id);
        map.put("ParentId", parentId);
        map.put("TraceId", traceId);
        map.put("StartTime", Long.valueOf(startTime));
        map.put("EndTime", Long.valueOf(endTime));
        map.put("Tag", tag);
        map.put("Error", error);

        return map;
    }
}
