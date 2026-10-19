package com.newlife.stardust;

import java.io.IOException;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.atomic.AtomicInteger;

import javax.servlet.FilterChain;
import javax.servlet.ServletException;
import javax.servlet.ServletRequest;
import javax.servlet.ServletResponse;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class StardustFilterTest {

    @AfterEach
    void clearDefault() {
        StardustTracer.setDefault(null);
    }

    @Test
    @DisplayName("入口复用上游 traceparent 的 TraceId 与 ParentId")
    void reusesUpstreamTraceContext() throws Exception {
        final TestTracer tracer = new TestTracer();
        final StardustFilter filter = new StardustFilter(tracer);
        final Map<String, Object> attributes = new HashMap<String, Object>();
        final HttpServletRequest request = request("GET", "/orders", "id=1",
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", attributes);
        final HttpServletResponse response = response(200);
        final AtomicInteger calls = new AtomicInteger();

        filter.doFilter(request, response, chain(calls, null));

        assertEquals(1, calls.get());
        assertEquals(1, tracer.spans.size());

        final Span span = tracer.last();
        assertEquals("GET /orders", span.getName());
        assertEquals("4bf92f3577b34da6a3ce929d0e0e4736", span.getTraceId());
        assertEquals("00f067aa0ba902b7", span.getParentId());
        assertEquals("GET http://example.com/orders?id=1", span.getTag());
        assertEquals("", span.getError());
        assertTrue(span.isFinished());
        assertTrue(span.getEndTime() >= span.getStartTime());
        assertNull(attributes.get(StardustFilter.SERVER_SPAN_ATTRIBUTE));
    }

    @Test
    @DisplayName("缺少 traceparent 时生成 32 位 TraceId")
    void generatesTraceIdWhenHeaderMissing() throws Exception {
        final TestTracer tracer = new TestTracer();
        final StardustFilter filter = new StardustFilter(tracer);

        filter.doFilter(request("POST", "/orders", null, null, new HashMap<String, Object>()),
                response(200), chain(new AtomicInteger(), null));

        final Span span = tracer.last();
        assertEquals(32, span.getTraceId().length());
        assertNull(span.getParentId());
        assertEquals("POST /orders", span.getName());
    }

    @Test
    @DisplayName("5xx 响应标记为错误")
    void marksServerError() throws Exception {
        final TestTracer tracer = new TestTracer();
        final StardustFilter filter = new StardustFilter(tracer);

        filter.doFilter(request("GET", "/broken", null, null, new HashMap<String, Object>()),
                response(503), chain(new AtomicInteger(), null));

        final Span span = tracer.last();
        assertEquals("HTTP 503", span.getError());
        assertTrue(span.isFinished());
    }

    @Test
    @DisplayName("未处理异常标记错误并原样抛出")
    void marksAndRethrowsException() {
        final TestTracer tracer = new TestTracer();
        final StardustFilter filter = new StardustFilter(tracer);
        final ServletException failure = new ServletException("boom");

        final ServletException thrown = assertThrows(ServletException.class, () -> filter.doFilter(
                request("GET", "/fail", null, null, new HashMap<String, Object>()),
                response(200), chain(new AtomicInteger(), failure)));

        assertSame(failure, thrown);
        final Span span = tracer.last();
        assertTrue(span.getError().contains("boom"));
        assertTrue(span.isFinished());
    }

    @Test
    @DisplayName("无参构造器动态解析默认 Tracer")
    void noArgConstructorResolvesDefaultDynamically() throws Exception {
        final TestTracer tracer = new TestTracer();
        StardustTracer.setDefault(null);
        final StardustFilter filter = new StardustFilter();

        filter.doFilter(request("GET", "/before", null, null, new HashMap<String, Object>()),
                response(200), chain(new AtomicInteger(), null));
        assertEquals(0, tracer.spans.size());

        StardustTracer.setDefault(tracer);
        filter.doFilter(request("GET", "/after", null, null, new HashMap<String, Object>()),
                response(200), chain(new AtomicInteger(), null));

        assertEquals(1, tracer.spans.size());
        assertEquals("GET /after", tracer.last().getName());
        assertSame(tracer, filter.getTracer());

        StardustTracer.setDefault(null);
    }

    @Test
    @DisplayName("null Tracer 与关闭自动埋点时直接放行")
    void passesThroughWhenDisabled() throws Exception {
        final TestTracer tracer = new TestTracer();
        tracer.setAutoInstrument(false);

        final AtomicInteger disabledCalls = new AtomicInteger();
        new StardustFilter(tracer).doFilter(
                request("GET", "/disabled", null, null, new HashMap<String, Object>()),
                response(200), chain(disabledCalls, null));
        assertEquals(1, disabledCalls.get());
        assertEquals(0, tracer.spans.size());

        final AtomicInteger nullCalls = new AtomicInteger();
        new StardustFilter(null).doFilter(
                request("GET", "/null", null, null, new HashMap<String, Object>()),
                response(200), chain(nullCalls, null));
        assertEquals(1, nullCalls.get());
    }

    @Test
    @DisplayName("已埋点请求与排除路径不重复创建入口 Span")
    void skipsDuplicateAndExcludedRequests() throws Exception {
        final TestTracer tracer = new TestTracer();
        final StardustFilter filter = new StardustFilter(tracer);

        final Map<String, Object> attributes = new HashMap<String, Object>();
        attributes.put(StardustFilter.SERVER_SPAN_ATTRIBUTE, Boolean.TRUE);
        filter.doFilter(request("GET", "/duplicate", null, null, attributes),
                response(200), chain(new AtomicInteger(), null));
        assertEquals(0, tracer.spans.size());

        filter.doFilter(request("POST", "/App/Login", null, null, new HashMap<String, Object>()),
                response(200), chain(new AtomicInteger(), null));
        assertEquals(0, tracer.spans.size());
        assertFalse(tracer.isExcluded("/orders"));
    }

    private static FilterChain chain(final AtomicInteger calls, final ServletException failure) {
        return new FilterChain() {
            @Override
            public void doFilter(ServletRequest request, ServletResponse response) throws IOException, ServletException {
                calls.incrementAndGet();
                if (failure != null) throw failure;
            }
        };
    }

    private static HttpServletRequest request(final String method, final String uri, final String query,
                                              final String traceparent, final Map<String, Object> attributes) {
        return (HttpServletRequest) Proxy.newProxyInstance(
                HttpServletRequest.class.getClassLoader(),
                new Class<?>[]{HttpServletRequest.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method invoked, Object[] args) {
                        final String name = invoked.getName();
                        if ("getMethod".equals(name)) return method;
                        if ("getRequestURI".equals(name)) return uri;
                        if ("getQueryString".equals(name)) return query;
                        if ("getRequestURL".equals(name)) return new StringBuffer("http://example.com" + uri);
                        if ("getHeader".equals(name) && args != null && args.length == 1
                                && TraceContext.TRACEPARENT_HEADER.equalsIgnoreCase(String.valueOf(args[0]))) {
                            return traceparent;
                        }
                        if ("getAttribute".equals(name) && args != null && args.length == 1) {
                            return attributes.get(args[0]);
                        }
                        if ("setAttribute".equals(name) && args != null && args.length == 2) {
                            attributes.put(String.valueOf(args[0]), args[1]);
                            return null;
                        }
                        if ("removeAttribute".equals(name) && args != null && args.length == 1) {
                            attributes.remove(args[0]);
                            return null;
                        }
                        if ("toString".equals(name)) return "TestRequest";
                        if ("hashCode".equals(name)) return Integer.valueOf(System.identityHashCode(proxy));
                        if ("equals".equals(name)) return Boolean.valueOf(proxy == args[0]);

                        return defaultValue(invoked.getReturnType());
                    }
                });
    }

    private static HttpServletResponse response(final int status) {
        return (HttpServletResponse) Proxy.newProxyInstance(
                HttpServletResponse.class.getClassLoader(),
                new Class<?>[]{HttpServletResponse.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        if ("getStatus".equals(method.getName())) return Integer.valueOf(status);
                        if ("toString".equals(method.getName())) return "TestResponse";
                        if ("hashCode".equals(method.getName())) return Integer.valueOf(System.identityHashCode(proxy));
                        if ("equals".equals(method.getName())) return Boolean.valueOf(proxy == args[0]);

                        return defaultValue(method.getReturnType());
                    }
                });
    }

    private static Object defaultValue(Class<?> type) {
        if (type == Boolean.TYPE) return Boolean.FALSE;
        if (type == Integer.TYPE) return Integer.valueOf(0);
        if (type == Long.TYPE) return Long.valueOf(0L);
        if (type == Double.TYPE) return Double.valueOf(0D);
        if (type == Float.TYPE) return Float.valueOf(0F);
        if (type == Short.TYPE) return Short.valueOf((short) 0);
        if (type == Byte.TYPE) return Byte.valueOf((byte) 0);
        if (type == Character.TYPE) return Character.valueOf('\0');
        if (type == Void.TYPE) return null;

        return null;
    }
}
