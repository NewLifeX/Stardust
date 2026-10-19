package com.newlife.stardust;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Process and application metrics sent with the heartbeat.
 *
 * <p>Field names match the server {@code AppInfo} contract. Fields that cannot be
 * collected on the current JVM remain zero or empty.</p>
 */
public final class AppInfo {

    /** Process id. */
    public int Id;

    /** Process/application name. */
    public String Name = "";

    /** Application version. */
    public String Version = "";

    /** Command line used to start the process. */
    public String CommandLine = "";

    /** Operating system user name. */
    public String UserName = "";

    /** Machine name. */
    public String MachineName = "";

    /** Local IPv4 address. */
    public String IP = "";

    /** Process start time in ISO-8601 format. */
    public String StartTime = "";

    /** Total process CPU time in milliseconds. */
    public long ProcessorTime;

    /** Process CPU usage in the last interval, 0..1. */
    public double CpuUsage;

    /** Working set (committed virtual memory or heap + non-heap) in bytes. */
    public long WorkingSet;

    /** Current heap usage in bytes. */
    public long HeapSize;

    /** Current thread count. */
    public int Threads;

    /** Available worker threads (not exposed by the JVM, kept for contract compatibility). */
    public int WorkerThreads;

    /** Available IO threads (not exposed by the JVM, kept for contract compatibility). */
    public int IOThreads;

    /** Active threads (not exposed by the JVM, kept for contract compatibility). */
    public int AvailableThreads;

    /** Pending work items (not exposed by the JVM, kept for contract compatibility). */
    public long PendingItems;

    /** Completed work items (not exposed by the JVM, kept for contract compatibility). */
    public long CompletedItems;

    /** Open file descriptor count, or 0 when unavailable. */
    public int Handles;

    /** TCP connection count, always 0 on the Java SDK. */
    public int Connections;

    /** GC collection count during the last interval. */
    public int GCCount;

    /** Listening endpoints, always empty on the Java SDK. */
    public String Listens = "";

    /** Collection time in Unix milliseconds. */
    public long Time;

    /**
     * Converts the metrics to a JSON-ready map.
     *
     * @return ordered map keyed by the server contract field names
     */
    public Map<String, Object> toMap() {
        final Map<String, Object> map = new LinkedHashMap<String, Object>();
        map.put("Id", Integer.valueOf(Id));
        map.put("Name", Name);
        map.put("Version", Version);
        map.put("CommandLine", CommandLine);
        map.put("UserName", UserName);
        map.put("MachineName", MachineName);
        map.put("IP", IP);
        map.put("StartTime", StartTime);
        map.put("ProcessorTime", Long.valueOf(ProcessorTime));
        map.put("CpuUsage", Double.valueOf(CpuUsage));
        map.put("WorkingSet", Long.valueOf(WorkingSet));
        map.put("HeapSize", Long.valueOf(HeapSize));
        map.put("Threads", Integer.valueOf(Threads));
        map.put("WorkerThreads", Integer.valueOf(WorkerThreads));
        map.put("IOThreads", Integer.valueOf(IOThreads));
        map.put("AvailableThreads", Integer.valueOf(AvailableThreads));
        map.put("PendingItems", Long.valueOf(PendingItems));
        map.put("CompletedItems", Long.valueOf(CompletedItems));
        map.put("Handles", Integer.valueOf(Handles));
        map.put("Connections", Integer.valueOf(Connections));
        map.put("GCCount", Integer.valueOf(GCCount));
        map.put("Listens", Listens);
        map.put("Time", Long.valueOf(Time));

        return map;
    }
}
