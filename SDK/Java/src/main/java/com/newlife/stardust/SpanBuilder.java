package com.newlife.stardust;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Aggregated statistics for one operation name within a report period.
 */
public final class SpanBuilder {

    private final String name;
    private int maxSamples;
    private int maxErrors;

    private long startTime;
    private long endTime;
    private long total;
    private long errors;
    private long cost;
    private long maxCost;
    private long minCost;

    private final List<Span> samples = new ArrayList<Span>();
    private final List<Span> errorSamples = new ArrayList<Span>();

    /**
     * Creates a builder with the default sample limits (1 normal, 10 error).
     *
     * @param name operation name
     */
    public SpanBuilder(String name) {
        this(name, 1, 10);
    }

    /**
     * Creates a builder.
     *
     * @param name operation name
     * @param maxSamples maximum number of normal samples
     * @param maxErrors maximum number of error samples
     */
    public SpanBuilder(String name, int maxSamples, int maxErrors) {
        if (name == null) throw new IllegalArgumentException("name is required");

        this.name = name;
        this.maxSamples = maxSamples;
        this.maxErrors = maxErrors;
    }

    /**
     * Adds a finished span to the aggregate.
     *
     * @param span span to add, may be {@code null}
     */
    public void add(Span span) {
        if (span == null || span.isNoop()) return;

        final long start = span.getStartTime();
        final long end = span.getEndTime() > 0 ? span.getEndTime() : System.currentTimeMillis();
        final long duration = Math.max(0L, end - start);

        if (total == 0) {
            startTime = start;
            endTime = end;
            minCost = duration;
        } else {
            if (start < startTime) startTime = start;
            if (end > endTime) endTime = end;
            if (duration < minCost) minCost = duration;
        }

        total++;
        cost += duration;
        if (duration > maxCost) maxCost = duration;

        final String error = span.getError();
        if (error != null && error.length() > 0) {
            errors++;
            if (maxErrors < 0 || errorSamples.size() < maxErrors) errorSamples.add(span);
        } else if (maxSamples < 0 || samples.size() < maxSamples) {
            samples.add(span);
        }
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
     * Returns the total number of spans.
     *
     * @return total count
     */
    public long getTotal() {
        return total;
    }

    /**
     * Returns the number of error spans.
     *
     * @return error count
     */
    public long getErrors() {
        return errors;
    }

    /**
     * Returns the total cost in milliseconds.
     *
     * @return total cost
     */
    public long getCost() {
        return cost;
    }

    /**
     * Returns the maximum cost in milliseconds.
     *
     * @return maximum cost
     */
    public long getMaxCost() {
        return maxCost;
    }

    /**
     * Returns the minimum cost in milliseconds.
     *
     * @return minimum cost
     */
    public long getMinCost() {
        return minCost;
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
     * Returns the span end time in Unix milliseconds.
     *
     * @return end time
     */
    public long getEndTime() {
        return endTime;
    }

    /**
     * Returns normal samples.
     *
     * @return immutable sample list
     */
    public List<Span> getSamples() {
        return Collections.unmodifiableList(samples);
    }

    /**
     * Returns error samples.
     *
     * @return immutable error sample list
     */
    public List<Span> getErrorSamples() {
        return Collections.unmodifiableList(errorSamples);
    }

    /**
     * Updates the maximum number of normal samples.
     *
     * @param maxSamples maximum samples, negative for unlimited
     */
    public void setMaxSamples(int maxSamples) {
        this.maxSamples = maxSamples;
        while (maxSamples >= 0 && samples.size() > maxSamples) {
            samples.remove(samples.size() - 1);
        }
    }

    /**
     * Updates the maximum number of error samples.
     *
     * @param maxErrors maximum error samples, negative for unlimited
     */
    public void setMaxErrors(int maxErrors) {
        this.maxErrors = maxErrors;
        while (maxErrors >= 0 && errorSamples.size() > maxErrors) {
            errorSamples.remove(errorSamples.size() - 1);
        }
    }

    /**
     * Converts the aggregate to the server builder format.
     *
     * @return builder map
     */
    public Map<String, Object> toMap() {
        final Map<String, Object> map = new LinkedHashMap<String, Object>();
        map.put("Name", name);
        map.put("StartTime", Long.valueOf(startTime));
        map.put("EndTime", Long.valueOf(endTime));
        map.put("Total", Long.valueOf(total));
        map.put("Errors", Long.valueOf(errors));
        map.put("Cost", Long.valueOf(cost));
        map.put("MaxCost", Long.valueOf(maxCost));
        map.put("MinCost", Long.valueOf(minCost));
        map.put("Samples", toSampleList(samples));
        map.put("ErrorSamples", toSampleList(errorSamples));

        return map;
    }

    private static List<Map<String, Object>> toSampleList(List<Span> spans) {
        final List<Map<String, Object>> list = new ArrayList<Map<String, Object>>(spans.size());
        for (Span span : spans) {
            list.add(span.toMap());
        }

        return list;
    }
}
