package com.newlife.stardust;

import java.io.IOException;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * One-line tracing wrapper around {@link HttpURLConnection}.
 *
 * <p>The JDK provides no global hook for outgoing HTTP calls, so automatic client spans are
 * only possible when the call goes through this class. Applications using OkHttp, Apache
 * HttpClient or Lettuce should create a client span plus {@code traceparent} header in their
 * interceptor; see the README for the extension point.</p>
 *
 * <pre>
 * HttpURLConnection connection = StardustHttp.open("https://api.example.com/orders");
 * try {
 *     // use connection
 * } finally {
 *     connection.disconnect();
 * }
 *
 * // or let StardustHttp create a METHOD host/path span and record the response status
 * HttpURLConnection traced = StardustHttp.execute("GET", "https://api.example.com/orders");
 * </pre>
 */
public final class StardustHttp {

    private StardustHttp() {
    }

    /**
     * Opens an HTTP connection and injects {@code traceparent} from the current span.
     *
     * <p>No span is created; use {@link #execute(String, String)} when a client span is
     * wanted. Callers that already set a {@code traceparent} header are not overridden.</p>
     *
     * @param url absolute HTTP or HTTPS URL
     * @return open connection
     * @throws IOException when the URL cannot be opened
     */
    public static HttpURLConnection open(String url) throws IOException {
        return open(new URL(url));
    }

    /**
     * Opens an HTTP connection and injects {@code traceparent} from the current span.
     *
     * @param url absolute HTTP or HTTPS URL
     * @return open connection
     * @throws IOException when the URL cannot be opened
     */
    public static HttpURLConnection open(URL url) throws IOException {
        final HttpURLConnection connection = openConnection(url);
        final StardustTracer tracer = StardustTracer.getDefault();
        if (tracer != null && tracer.isAutoInstrument()) inject(connection, tracer.currentSpan());

        return connection;
    }

    /**
     * Executes a request, creating a client span named {@code METHOD host/path}.
     *
     * @param method HTTP method, defaults to {@code GET}
     * @param url absolute HTTP or HTTPS URL
     * @return connection after response headers were received; the caller reads the body
     * @throws IOException when the request fails
     */
    public static HttpURLConnection execute(String method, String url) throws IOException {
        return execute(StardustTracer.getDefault(), method, url, null, null);
    }

    /**
     * Executes a request with a body, creating a client span named {@code METHOD host/path}.
     *
     * @param method HTTP method, defaults to {@code GET}
     * @param url absolute HTTP or HTTPS URL
     * @param body request body, may be {@code null}
     * @param contentType content type, may be {@code null}
     * @return connection after response headers were received; the caller reads the body
     * @throws IOException when the request fails
     */
    public static HttpURLConnection execute(String method, String url, byte[] body, String contentType) throws IOException {
        return execute(StardustTracer.getDefault(), method, url, body, contentType);
    }

    /**
     * Executes a request using an explicit tracer.
     *
     * <p>The span is finished after response headers are received, even when the request
     * fails. Response bodies are not read here; a span therefore covers connection setup,
     * request sending and response headers.</p>
     *
     * @param tracer tracer, may be {@code null}
     * @param method HTTP method, defaults to {@code GET}
     * @param url absolute HTTP or HTTPS URL
     * @param body request body, may be {@code null}
     * @param contentType content type, may be {@code null}
     * @return connection after response headers were received
     * @throws IOException when the request fails
     */
    public static HttpURLConnection execute(StardustTracer tracer, String method, String url, byte[] body, String contentType) throws IOException {
        final String verb = method == null || method.trim().length() == 0 ? "GET" : method.trim().toUpperCase();
        final URL target = new URL(url);

        Span span = null;
        if (tracer != null && tracer.isAutoInstrument()) {
            try {
                span = tracer.newSpan(spanName(verb, target));
                span.setTag(target.toString());
            } catch (Throwable e) {
                if (span != null) span.finish();
                span = null;
            }
        }

        try {
            final HttpURLConnection connection = openConnection(target);
            connection.setRequestMethod(verb);
            inject(connection, span);

            if (body != null && body.length > 0) {
                connection.setDoOutput(true);
                if (contentType != null && contentType.length() > 0) connection.setRequestProperty("Content-Type", contentType);
                connection.setFixedLengthStreamingMode(body.length);

                final OutputStream output = connection.getOutputStream();
                try {
                    output.write(body);
                } finally {
                    output.close();
                }
            }

            final int status = connection.getResponseCode();
            if (span != null && status >= 400) span.setError("HTTP " + status);

            return connection;
        } catch (IOException e) {
            if (span != null) span.setError(e);
            throw e;
        } catch (RuntimeException e) {
            if (span != null) span.setError(e);
            throw e;
        } catch (Error e) {
            if (span != null) span.setError(e);
            throw e;
        } finally {
            if (span != null) span.finish();
        }
    }

    /**
     * Injects a {@code traceparent} header derived from the given span.
     *
     * <p>This is the extension point for other HTTP clients: create a span with
     * {@link StardustTracer#currentSpan()} as parent, add it to the outbound request, and
     * finish it when the response arrives.</p>
     *
     * @param connection target connection
     * @param span current span, may be {@code null} or a no-op span
     */
    public static void inject(HttpURLConnection connection, Span span) {
        if (connection == null || span == null || span.isNoop()) return;

        try {
            if (connection.getRequestProperty(TraceContext.TRACEPARENT_HEADER) != null) return;

            connection.setRequestProperty(TraceContext.TRACEPARENT_HEADER, traceparent(span));
        } catch (Throwable ignored) {
            // Header injection must never break the business request.
        }
    }

    /**
     * Formats a span as a {@code traceparent} header value.
     *
     * <p>Extension point for clients without a JDK hook (OkHttp interceptor, Lettuce
     * {@code RedisClient} command listener, Apache HttpClient request interceptor): create a
     * span, pass it here and set the returned value as the {@code traceparent} header.</p>
     *
     * @param span span, may be {@code null} or a no-op span
     * @return header value, or an empty string when the span is not usable
     */
    public static String traceparent(Span span) {
        if (span == null || span.isNoop() || span.getTraceId() == null) return "";

        try {
            return new TraceContext(span.getTraceId(), span.getId(), true).format();
        } catch (IllegalArgumentException e) {
            return "";
        }
    }

    private static HttpURLConnection openConnection(URL url) throws IOException {
        if (url == null) throw new IllegalArgumentException("url is required");

        final String protocol = url.getProtocol();
        if (!"http".equalsIgnoreCase(protocol) && !"https".equalsIgnoreCase(protocol)) {
            throw new IllegalArgumentException("only http and https URLs are supported");
        }

        return (HttpURLConnection) url.openConnection();
    }

    private static String spanName(String method, URL url) {
        String host = url.getHost();
        if (host == null) host = "";
        if (url.getPort() > 0) host += ":" + url.getPort();

        String path = url.getPath();
        if (path == null || path.length() == 0) path = "/";

        return method + " " + host + path;
    }
}
