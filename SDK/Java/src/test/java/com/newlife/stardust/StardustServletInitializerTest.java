package com.newlife.stardust;

import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.EnumSet;

import javax.servlet.DispatcherType;
import javax.servlet.FilterRegistration;
import javax.servlet.ServletContext;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class StardustServletInitializerTest {

    @Test
    @DisplayName("容器启动时把 Filter 映射到 /* 的 REQUEST 分发")
    void registersFilterForRequestDispatch() throws Exception {
        final String[] patterns = new String[1];
        final EnumSet<DispatcherType>[] dispatchers = new EnumSet[1];
        final boolean[] async = new boolean[1];

        final FilterRegistration.Dynamic dynamic = (FilterRegistration.Dynamic) Proxy.newProxyInstance(
                FilterRegistration.Dynamic.class.getClassLoader(),
                new Class<?>[]{FilterRegistration.Dynamic.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        if ("addMappingForUrlPatterns".equals(method.getName())) {
                            dispatchers[0] = (EnumSet<DispatcherType>) args[0];
                            patterns[0] = ((String[]) args[2])[0];
                            return null;
                        }
                        if ("setAsyncSupported".equals(method.getName())) {
                            async[0] = Boolean.TRUE.equals(args[0]);
                            return null;
                        }

                        return null;
                    }
                });

        final ServletContext context = (ServletContext) Proxy.newProxyInstance(
                ServletContext.class.getClassLoader(),
                new Class<?>[]{ServletContext.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        if ("getFilterRegistration".equals(method.getName())) return null;
                        if ("addFilter".equals(method.getName())) {
                            assertEquals(StardustServletInitializer.FILTER_NAME, args[0]);
                            assertEquals(StardustFilter.class, args[1]);
                            return dynamic;
                        }

                        return null;
                    }
                });

        new StardustServletInitializer().onStartup(null, context);

        assertEquals(EnumSet.of(DispatcherType.REQUEST), dispatchers[0]);
        assertArrayEquals(new String[]{"/*"}, patterns);
        assertTrue(async[0]);
    }

    @Test
    @DisplayName("不支持动态注册时不阻断应用启动")
    void ignoresUnsupportedRegistration() {
        final ServletContext context = (ServletContext) Proxy.newProxyInstance(
                ServletContext.class.getClassLoader(),
                new Class<?>[]{ServletContext.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        if ("addFilter".equals(method.getName())) throw new UnsupportedOperationException("no dynamic registration");

                        return null;
                    }
                });

        assertDoesNotThrow(() -> new StardustServletInitializer().onStartup(null, context));
    }

    @Test
    @DisplayName("ServiceLoader 服务声明存在")
    void serviceDeclarationExists() {
        assertNotNull(StardustServletInitializer.class.getClassLoader()
                .getResource("META-INF/services/javax.servlet.ServletContainerInitializer"));
    }
}
