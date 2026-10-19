package com.newlife.stardust;

import java.util.EnumSet;
import java.util.Set;

import javax.servlet.DispatcherType;
import javax.servlet.FilterRegistration;
import javax.servlet.ServletContainerInitializer;
import javax.servlet.ServletContext;
import javax.servlet.ServletException;

/**
 * Registers {@link StardustFilter} automatically when a Servlet 3.0+ container starts.
 *
 * <p>The initializer is discovered through
 * {@code META-INF/services/javax.servlet.ServletContainerInitializer}. It maps the filter to
 * {@code /*} for {@code REQUEST} dispatches only. Registration is best effort: any failure is
 * ignored so application startup is never affected.</p>
 *
 * <p>The filter created by the container uses the no-arg constructor and therefore resolves
 * {@link StardustTracer#getDefault()} on every request. Creating a tracer with
 * auto-instrumentation enabled registers it as the default automatically:</p>
 *
 * <pre>
 * StardustTracer tracer = new StardustTracer(server, appId, secret).start();
 * </pre>
 */
public class StardustServletInitializer implements ServletContainerInitializer {

    static final String FILTER_NAME = "stardust";

    @Override
    public void onStartup(Set<Class<?>> classes, ServletContext context) throws ServletException {
        if (context == null) return;

        try {
            if (context.getFilterRegistration(FILTER_NAME) != null) return;

            final FilterRegistration.Dynamic registration = context.addFilter(FILTER_NAME, StardustFilter.class);
            if (registration == null) return;

            registration.addMappingForUrlPatterns(EnumSet.of(DispatcherType.REQUEST), true, "/*");
            registration.setAsyncSupported(true);
        } catch (Throwable e) {
            // Registration is optional; never break application startup.
            final StardustTracer tracer = StardustTracer.getDefault();
            if (tracer != null) tracer.log("Stardust servlet auto-registration failed", e);
        }
    }
}
