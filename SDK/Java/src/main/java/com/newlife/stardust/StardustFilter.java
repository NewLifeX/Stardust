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
 * Servlet filter that records one span per HTTP request.
 *
 * <p>The span name is {@code METHOD requestURI}; the query string is used as the tag.
 * Responses with status 500 or above and thrown exceptions are marked as errors.</p>
 *
 * <p>This class requires the Servlet API at compile time only; add
 * {@code javax.servlet:javax.servlet-api:3.1.0} with {@code provided} scope.</p>
 *
 * <pre>
 * StardustFilter filter = new StardustFilter(tracer);
 * </pre>
 */
public class StardustFilter implements Filter {

    private final StardustTracer tracer;

    /**
     * Creates a filter without a tracer. Requests pass through untouched.
     */
    public StardustFilter() {
        this(null);
    }

    /**
     * Creates a filter bound to a tracer.
     *
     * @param tracer tracer, may be {@code null}
     */
    public StardustFilter(StardustTracer tracer) {
        this.tracer = tracer;
    }

    /**
     * Returns the bound tracer.
     *
     * @return tracer or {@code null}
     */
    public StardustTracer getTracer() {
        return tracer;
    }

    @Override
    public void init(FilterConfig filterConfig) throws ServletException {
        // Nothing to initialize.
    }

    @Override
    public void doFilter(ServletRequest request, ServletResponse response, FilterChain chain) throws IOException, ServletException {
        if (tracer == null) {
            chain.doFilter(request, response);
            return;
        }

        final String method = request instanceof HttpServletRequest ? ((HttpServletRequest) request).getMethod() : "REQUEST";
        final String uri = request instanceof HttpServletRequest ? ((HttpServletRequest) request).getRequestURI() : "";
        if (tracer.isExcluded(uri)) {
            chain.doFilter(request, response);
            return;
        }

        final Span span = tracer.newSpan(method + " " + uri);
        try {
            if (request instanceof HttpServletRequest) {
                span.setTag(((HttpServletRequest) request).getQueryString());
            }

            chain.doFilter(request, response);

            if (response instanceof HttpServletResponse) {
                final int status = ((HttpServletResponse) response).getStatus();
                if (status >= 500) span.setError("HTTP " + status);
            }
        } catch (IOException e) {
            span.setError(e);
            throw e;
        } catch (ServletException e) {
            span.setError(e);
            throw e;
        } catch (RuntimeException e) {
            span.setError(e);
            throw e;
        } catch (Error e) {
            span.setError(e);
            throw e;
        } finally {
            span.close();
        }
    }

    @Override
    public void destroy() {
        // The tracer lifecycle is managed by the application.
    }
}
