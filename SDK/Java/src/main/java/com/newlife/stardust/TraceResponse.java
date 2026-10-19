package com.newlife.stardust;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Sampling parameters returned by the Stardust server after a report.
 */
public final class TraceResponse {

    private int period = 60;
    private int maxSamples = 1;
    private int maxErrors = 10;
    private int timeout;
    private int maxTagLength = 1024;
    private int requestTagLength = 1024;
    private Boolean enableMeter;
    private String[] excludes;

    /**
     * Returns the sampling period in seconds.
     *
     * @return sampling period in seconds
     */
    public int getPeriod() {
        return period;
    }

    /**
     * Sets the sampling period in seconds.
     *
     * @param period sampling period in seconds
     */
    public void setPeriod(int period) {
        this.period = period;
    }

    /**
     * Returns the maximum number of normal samples per builder and period.
     *
     * @return maximum normal samples
     */
    public int getMaxSamples() {
        return maxSamples;
    }

    /**
     * Sets the maximum number of normal samples per builder and period.
     *
     * @param maxSamples maximum normal samples
     */
    public void setMaxSamples(int maxSamples) {
        this.maxSamples = maxSamples;
    }

    /**
     * Returns the maximum number of error samples per builder and period.
     *
     * @return maximum error samples
     */
    public int getMaxErrors() {
        return maxErrors;
    }

    /**
     * Sets the maximum number of error samples per builder and period.
     *
     * @param maxErrors maximum error samples
     */
    public void setMaxErrors(int maxErrors) {
        this.maxErrors = maxErrors;
    }

    /**
     * Returns the slow-operation timeout in milliseconds.
     *
     * @return timeout in milliseconds
     */
    public int getTimeout() {
        return timeout;
    }

    /**
     * Sets the slow-operation timeout in milliseconds.
     *
     * @param timeout timeout in milliseconds
     */
    public void setTimeout(int timeout) {
        this.timeout = timeout;
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
     * Sets the maximum tag length.
     *
     * @param maxTagLength maximum tag length
     */
    public void setMaxTagLength(int maxTagLength) {
        this.maxTagLength = maxTagLength;
    }

    /**
     * Returns the maximum request tag length.
     *
     * @return maximum request tag length
     */
    public int getRequestTagLength() {
        return requestTagLength;
    }

    /**
     * Sets the maximum request tag length.
     *
     * @param requestTagLength maximum request tag length
     */
    public void setRequestTagLength(int requestTagLength) {
        this.requestTagLength = requestTagLength;
    }

    /**
     * Returns whether the server wants application metrics, or {@code null} when not specified.
     *
     * @return metric collection flag
     */
    public Boolean getEnableMeter() {
        return enableMeter;
    }

    /**
     * Sets whether the server wants application metrics.
     *
     * @param enableMeter metric collection flag
     */
    public void setEnableMeter(Boolean enableMeter) {
        this.enableMeter = enableMeter;
    }

    /**
     * Returns operation names excluded by the server.
     *
     * @return excluded operation names, or {@code null}
     */
    public String[] getExcludes() {
        return excludes;
    }

    /**
     * Sets operation names excluded by the server.
     *
     * @param excludes excluded operation names
     */
    public void setExcludes(String[] excludes) {
        this.excludes = excludes;
    }

    /**
     * Builds a response from the {@code data} object of the server envelope.
     *
     * @param data response data
     * @return parsed response
     */
    static TraceResponse fromMap(Map<String, Object> data) {
        final TraceResponse response = new TraceResponse();
        if (data == null) return response;

        response.period = intValue(data.get("Period"), response.period);
        response.maxSamples = intValue(data.get("MaxSamples"), response.maxSamples);
        response.maxErrors = intValue(data.get("MaxErrors"), response.maxErrors);
        response.timeout = intValue(data.get("Timeout"), response.timeout);
        response.maxTagLength = intValue(data.get("MaxTagLength"), response.maxTagLength);
        response.requestTagLength = intValue(data.get("RequestTagLength"), response.requestTagLength);

        final Object enableMeter = data.get("EnableMeter");
        if (enableMeter instanceof Boolean) response.enableMeter = (Boolean) enableMeter;

        final Object excludes = data.get("Excludes");
        if (excludes instanceof String[]) {
            response.excludes = (String[]) excludes;
        } else if (excludes instanceof Iterable) {
            final Iterable<?> values = (Iterable<?>) excludes;
            final java.util.List<String> list = new java.util.ArrayList<String>();
            for (Object value : values) {
                if (value != null) list.add(String.valueOf(value));
            }
            response.excludes = list.toArray(new String[list.size()]);
        } else if (excludes instanceof String) {
            final String value = (String) excludes;
            response.excludes = value.length() == 0 ? new String[0] : value.split("[,;]");
        }

        return response;
    }

    private static int intValue(Object value, int defaultValue) {
        if (value instanceof Number) return ((Number) value).intValue();
        if (value != null) {
            try {
                return Integer.parseInt(value.toString().trim());
            } catch (NumberFormatException ignored) {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /**
     * Converts the response to a map, primarily for diagnostics.
     *
     * @return response fields
     */
    public Map<String, Object> toMap() {
        final Map<String, Object> map = new LinkedHashMap<String, Object>();
        map.put("Period", Integer.valueOf(period));
        map.put("MaxSamples", Integer.valueOf(maxSamples));
        map.put("MaxErrors", Integer.valueOf(maxErrors));
        map.put("Timeout", Integer.valueOf(timeout));
        map.put("MaxTagLength", Integer.valueOf(maxTagLength));
        map.put("RequestTagLength", Integer.valueOf(requestTagLength));
        map.put("EnableMeter", enableMeter);
        map.put("Excludes", excludes);

        return map;
    }
}
