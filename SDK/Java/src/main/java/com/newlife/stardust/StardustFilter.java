package com.newlife.stardust;

import java.io.IOException;

import javax.servlet.Filter;
import javax.servlet.FilterChain;
import javax.servlet.FilterConfig;
import javax.servlet.ServletException;
import javax.servlet.ServletRequest;
import javax.servlet.ServletResponse;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

/**
 * Servlet filter that records one server span per HTTP request.
 *
 * <p>The span name is {@code METHOD requestURI} and the tag is {@code METHOD originalURL}.
 * An inbound {@code traceparent} header is parsed so the trace id and upstream span id are
 * reused. Responses with status 500 or above and unhandled exceptions are marked as errors.</p>
 *
 * <p>Requests already instrumented by another Stardust filter are passed through, so a
 * request produces only one server span.</p>
 *
 * <p>This class requires the Servlet API at compile time only; add
 * {@code javax.servlet:javax.servlet-api:3.1.0} with {@code provided} scope.</p>
 *
 * <pre>
 * StardustFilter filter = new StardustFilter(tracer);
 * </pre>
 */
public class StardustFilter implements Filter {

    static final String SERVER_SPAN_ATTRIBUTE = "com.newlife.stardust.serverSpan";

    private final StardustTracer tracer;
    private final boolean dynamicTracer;

    /**
     * Creates a filter that resolves {@link StardustTracer#getDefault()} on every request.
     */
    public StardustFilter() {
        this.tracer = null;
        this.dynamicTracer = true;
    }

    /**
     * Creates a filter bound to a tracer. A {@code null} tracer makes the filter a no-op.
     *
     * @param tracer tracer, may be {@code null}
     */
    public StardustFilter(StardustTracer tracer) {
        this.tracer = tracer;
        this.dynamicTracer = false;
    }

    /**
     * Returns the bound tracer, or the current default tracer for the no-arg constructor.
     *
     * @return tracer or {@code null}
     */
    public StardustTracer getTracer() {
        return resolveTracer();
    }

    @Override
    public void init(FilterConfig filterConfig) throws ServletException {
        // Nothing to initialize.
    }

    @Override
    public void doFilter(ServletRequest request, ServletResponse response, FilterChain chain) throws IOException, ServletException {
        final StardustTracer current = resolveTracer();
        if (current == null || !current.isAutoInstrument()) {
            chain.doFilter(request, response);
            return;
        }

        if (isInstrumented(request)) {
            chain.doFilter(request, response);
            return;
        }

        final HttpServletRequest httpRequest = request instanceof HttpServletRequest ? (HttpServletRequest) request : null;
        final String method = httpRequest != null ? httpRequest.getMethod() : "REQUEST";
        final String uri = httpRequest != null ? httpRequest.getRequestURI() : "";
        final String name = method + " " + uri;

        Span span = null;
        boolean marked = false;
        try {
            if (!current.isExcluded(name)) {
                final TraceContext context = httpRequest != null
                        ? TraceContext.parse(httpRequest.getHeader(TraceContext.TRACEPARENT_HEADER))
                        : null;
                span = context != null ? current.newSpan(name, context) : current.newSpan(name);
                span.setTag(httpRequest != null ? requestTag(httpRequest) : name);

                try {
                    request.setAttribute(SERVER_SPAN_ATTRIBUTE, Boolean.TRUE);
                    marked = true;
                } catch (Throwable ignored) {
                    // Attribute support is optional on custom request wrappers.
                }
            }
        } catch (Throwable e) {
            current.log("Stardust servlet instrumentation failed", e);
            if (span != null) span.finish();
            span = null;
        }

        try {
            chain.doFilter(request, response);

            if (span != null && response instanceof HttpServletResponse) {
                final int status = ((HttpServletResponse) response).getStatus();
                if (status >= 500) span.setError("HTTP " + status);
            }
        } catch (IOException e) {
            if (span != null) span.setError(e);
            throw e;
        } catch (ServletException e) {
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
            if (marked) {
                try {
                    request.removeAttribute(SERVER_SPAN_ATTRIBUTE);
                } catch (Throwable ignored) {
                    // Ignore custom request wrappers without attribute support.
                }
            }
        }
    }

    @Override
    public void destroy() {
        // The tracer lifecycle is managed by the application.
    }

    private StardustTracer resolveTracer() {
        return dynamicTracer ? StardustTracer.getDefault() : tracer;
    }

    private static boolean isInstrumented(ServletRequest request) {
        try {
            return Boolean.TRUE.equals(request.getAttribute(SERVER_SPAN_ATTRIBUTE));
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static String requestTag(HttpServletRequest request) {
        final StringBuffer buffer = request.getRequestURL();
        String url = buffer == null ? request.getRequestURI() : buffer.toString();
        if (url == null) url = "";

        final String query = request.getQueryString();
        if (query != null && query.length() > 0) url += "?" + query;

        return request.getMethod() + " " + url;
    }
}
