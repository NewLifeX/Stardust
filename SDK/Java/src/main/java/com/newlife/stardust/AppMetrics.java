package com.newlife.stardust;

import java.lang.management.GarbageCollectorMXBean;
import java.lang.management.ManagementFactory;
import java.lang.management.MemoryMXBean;
import java.lang.management.RuntimeMXBean;
import java.lang.management.ThreadMXBean;
import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Enumeration;
import java.util.List;
import java.util.Locale;

/**
 * Collects process metrics for heartbeats using only JMX APIs from the JDK.
 */
final class AppMetrics {

    private static final String FALLBACK_IP = "127.0.0.1";

    private final StardustTracer tracer;
    private final RuntimeMXBean runtimeBean = ManagementFactory.getRuntimeMXBean();
    private final MemoryMXBean memoryBean = ManagementFactory.getMemoryMXBean();
    private final ThreadMXBean threadBean = ManagementFactory.getThreadMXBean();
    private final java.lang.management.OperatingSystemMXBean osBean = ManagementFactory.getOperatingSystemMXBean();
    private final List<GarbageCollectorMXBean> gcBeans = ManagementFactory.getGarbageCollectorMXBeans();

    private long lastProcessCpuTime = -1L;
    private long lastWallNanos = -1L;
    private long lastGcCount = -1L;
    private double lastCpuUsage;

    AppMetrics(StardustTracer tracer) {
        this.tracer = tracer;
    }

    AppInfo collect() {
        final AppInfo info = new AppInfo();
        try {
            info.Id = pid();
            info.Name = processName(tracer.getAppName());
            info.Version = tracer.getVersion();
            info.CommandLine = property("sun.java.command", "");
            info.UserName = property("user.name", "");
            info.MachineName = hostName();
            info.IP = localIp();
            info.StartTime = formatStartTime(runtimeBean.getStartTime());

            final Long processCpuTime = processCpuTime();
            if (processCpuTime != null) info.ProcessorTime = processCpuTime.longValue();
            info.CpuUsage = cpuUsage(processCpuTime);
            info.HeapSize = heapUsed();
            info.WorkingSet = workingSet(info.HeapSize);
            info.Threads = threadBean.getThreadCount();
            info.Handles = openFileDescriptors();
            info.GCCount = gcCountDelta();

            info.Connections = 0;
            info.Listens = "";
            info.Time = System.currentTimeMillis();
        } catch (Throwable e) {
            tracer.log("Failed to collect application metrics", e);
        }

        return info;
    }

    static int pid() {
        try {
            final String name = ManagementFactory.getRuntimeMXBean().getName();
            if (name != null) {
                final int index = name.indexOf('@');
                final String value = index > 0 ? name.substring(0, index) : name;
                return Integer.parseInt(value);
            }
        } catch (Throwable ignored) {
            // Fall through to unknown pid.
        }

        return 0;
    }

    static String processName(String appName) {
        final String command = property("sun.java.command", "");
        if (command.trim().length() > 0) {
            final String value = command.trim();
            final int index = firstWhitespace(value);
            final String token = index > 0 ? value.substring(0, index) : value;
            if (token.length() > 0) return token;
        }

        return appName == null ? "" : appName;
    }

    static String localIp() {
        try {
            final Enumeration<NetworkInterface> interfaces = NetworkInterface.getNetworkInterfaces();
            if (interfaces != null) {
                while (interfaces.hasMoreElements()) {
                    final NetworkInterface network = interfaces.nextElement();
                    if (!network.isUp() || network.isLoopback()) continue;

                    final Enumeration<InetAddress> addresses = network.getInetAddresses();
                    while (addresses.hasMoreElements()) {
                        final InetAddress address = addresses.nextElement();
                        if (address instanceof Inet4Address && !address.isLoopbackAddress() && !address.isLinkLocalAddress()) {
                            return address.getHostAddress();
                        }
                    }
                }
            }
        } catch (Throwable ignored) {
            // Fall through to loopback.
        }

        return FALLBACK_IP;
    }

    static String hostName() {
        try {
            return InetAddress.getLocalHost().getHostName();
        } catch (Throwable ignored) {
            // Fall through to environment variables.
        }

        String name = System.getenv("COMPUTERNAME");
        if (name == null || name.length() == 0) name = System.getenv("HOSTNAME");

        return name == null ? "" : name;
    }

    static String formatStartTime(long startTime) {
        final SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSSXXX", Locale.ROOT);
        return format.format(new Date(startTime));
    }

    private long heapUsed() {
        final java.lang.management.MemoryUsage usage = memoryBean.getHeapMemoryUsage();
        return usage == null ? 0L : usage.getUsed();
    }

    private long workingSet(long heapUsed) {
        final com.sun.management.OperatingSystemMXBean bean = comSunOsBean();
        if (bean != null) {
            final long committed = bean.getCommittedVirtualMemorySize();
            if (committed > 0) return committed;
        }

        long total = heapUsed;
        final java.lang.management.MemoryUsage nonHeap = memoryBean.getNonHeapMemoryUsage();
        if (nonHeap != null && nonHeap.getUsed() > 0) total += nonHeap.getUsed();

        return total;
    }

    private Long processCpuTime() {
        final com.sun.management.OperatingSystemMXBean bean = comSunOsBean();
        if (bean != null) {
            final long nanos = bean.getProcessCpuTime();
            if (nanos >= 0) return Long.valueOf(nanos / 1000000L);
        }

        return null;
    }

    private double cpuUsage(Long processCpuTime) {
        final long now = System.nanoTime();
        double usage = Double.NaN;

        if (processCpuTime != null && lastProcessCpuTime >= 0 && lastWallNanos >= 0) {
            final long cpuDelta = processCpuTime.longValue() - lastProcessCpuTime;
            final long wallDeltaNanos = now - lastWallNanos;
            if (wallDeltaNanos > 0) usage = (double) cpuDelta / ((double) wallDeltaNanos / 1000000.0d);
        }

        if (Double.isNaN(usage) || Double.isInfinite(usage)) {
            final com.sun.management.OperatingSystemMXBean bean = comSunOsBean();
            if (bean != null) {
                final double load = bean.getProcessCpuLoad();
                if (load >= 0) usage = load;
            }
        }

        if (Double.isNaN(usage) || Double.isInfinite(usage) || usage < 0) usage = 0;
        if (usage > 1) usage = 1;
        lastCpuUsage = usage;

        if (processCpuTime != null) lastProcessCpuTime = processCpuTime.longValue();
        lastWallNanos = now;

        return lastCpuUsage;
    }

    private int openFileDescriptors() {
        final com.sun.management.OperatingSystemMXBean bean = comSunOsBean();
        if (bean instanceof com.sun.management.UnixOperatingSystemMXBean) {
            final long count = ((com.sun.management.UnixOperatingSystemMXBean) bean).getOpenFileDescriptorCount();
            if (count > 0 && count <= Integer.MAX_VALUE) return (int) count;
        }

        return 0;
    }

    private int gcCountDelta() {
        long total = 0;
        for (GarbageCollectorMXBean bean : gcBeans) {
            final long count = bean.getCollectionCount();
            if (count > 0) total += count;
        }

        int delta = 0;
        if (lastGcCount >= 0) {
            final long value = total - lastGcCount;
            if (value > 0) delta = value > Integer.MAX_VALUE ? Integer.MAX_VALUE : (int) value;
        }
        lastGcCount = total;

        return delta;
    }

    private com.sun.management.OperatingSystemMXBean comSunOsBean() {
        return osBean instanceof com.sun.management.OperatingSystemMXBean
                ? (com.sun.management.OperatingSystemMXBean) osBean
                : null;
    }

    private static String property(String key, String defaultValue) {
        try {
            final String value = System.getProperty(key);
            return value == null ? defaultValue : value;
        } catch (Throwable ignored) {
            return defaultValue;
        }
    }

    private static int firstWhitespace(String value) {
        for (int i = 0; i < value.length(); i++) {
            if (Character.isWhitespace(value.charAt(i))) return i;
        }

        return -1;
    }
}
