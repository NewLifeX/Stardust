package com.newlife.stardust;

import java.lang.reflect.InvocationHandler;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.sql.CallableStatement;
import java.sql.Connection;
import java.sql.DatabaseMetaData;
import java.sql.PreparedStatement;
import java.sql.Statement;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import javax.sql.DataSource;

/**
 * One-line JDBC instrumentation based on dynamic proxies.
 *
 * <p>{@link #wrap(DataSource)} and {@link #wrap(Connection)} return proxies that create a
 * {@code db:{system}:{action}:{tables}} span for every execute call and record SQL,
 * exceptions and batch statements. The tracer is resolved from
 * {@link StardustTracer#getDefault()} at execution time, so the tracer can be configured
 * before or after wrapping.</p>
 *
 * <pre>
 * DataSource dataSource = StardustJdbc.wrap(rawDataSource);
 * Connection connection = StardustJdbc.wrap(rawConnection);
 * </pre>
 *
 * <p>Limitations: only {@link DataSource}, {@link Connection}, {@link Statement},
 * {@link PreparedStatement} and {@link CallableStatement} interfaces are proxied. Casting a
 * proxy to a vendor-specific type (for example {@code OracleConnection}) is not supported;
 * use the original object via {@code unwrap} for vendor APIs.</p>
 */
public final class StardustJdbc {

    private static final Set<String> EXECUTE_METHODS = new LinkedHashSet<String>();
    private static final Pattern TABLE_PATTERN = Pattern.compile(
            "\\b(?:from|into|update|join|drop\\s+table|truncate\\s+table)\\s+([\\w.$`\\[\\]\"]+)",
            Pattern.CASE_INSENSITIVE);
    private static final Pattern SHARD_PATTERN = Pattern.compile(
            "^(.+?)[_\\-.](?:19|20)\\d{2}(?:0[1-9]|1[0-2])?(?:0[1-9]|[12]\\d|3[01])?$");

    static {
        EXECUTE_METHODS.add("execute");
        EXECUTE_METHODS.add("executeQuery");
        EXECUTE_METHODS.add("executeUpdate");
        EXECUTE_METHODS.add("executeLargeUpdate");
        EXECUTE_METHODS.add("executeBatch");
        EXECUTE_METHODS.add("executeLargeBatch");
    }

    private StardustJdbc() {
    }

    /**
     * Wraps a data source so returned connections and statements are traced.
     *
     * @param dataSource data source, may be {@code null}
     * @return traced data source, or the original value when it is already wrapped
     */
    public static DataSource wrap(DataSource dataSource) {
        if (dataSource == null || isWrapped(dataSource)) return dataSource;

        return (DataSource) Proxy.newProxyInstance(
                DataSource.class.getClassLoader(),
                new Class<?>[]{DataSource.class},
                new DataSourceHandler(dataSource));
    }

    /**
     * Wraps a connection so created statements are traced.
     *
     * @param connection connection, may be {@code null}
     * @return traced connection, or the original value when it is already wrapped
     */
    public static Connection wrap(Connection connection) {
        if (connection == null || isWrapped(connection)) return connection;

        return (Connection) Proxy.newProxyInstance(
                Connection.class.getClassLoader(),
                new Class<?>[]{Connection.class},
                new ConnectionHandler(connection));
    }

    private static Statement wrapStatement(Statement statement, ConnectionInfo info, String sql) {
        if (statement == null || isWrapped(statement)) return statement;

        final Class<?> type;
        if (statement instanceof CallableStatement) {
            type = CallableStatement.class;
        } else if (statement instanceof PreparedStatement) {
            type = PreparedStatement.class;
        } else {
            type = Statement.class;
        }

        return (Statement) Proxy.newProxyInstance(
                type.getClassLoader(),
                new Class<?>[]{type},
                new StatementHandler(statement, info, sql));
    }

    private static boolean isWrapped(Object value) {
        return value != null
                && Proxy.isProxyClass(value.getClass())
                && Proxy.getInvocationHandler(value) instanceof BaseHandler;
    }

    private abstract static class BaseHandler implements InvocationHandler {

        final Object target;

        BaseHandler(Object target) {
            this.target = target;
        }

        @Override
        public final Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
            if (method.getDeclaringClass() == Object.class) return invokeObject(proxy, method, args);

            try {
                return invokeTarget(proxy, method, args);
            } catch (InvocationTargetException e) {
                throw e.getTargetException();
            }
        }

        abstract Object invokeTarget(Object proxy, Method method, Object[] args) throws Throwable;

        Object call(Method method, Object[] args) throws Throwable {
            return method.invoke(target, args);
        }

        private Object invokeObject(Object proxy, Method method, Object[] args) throws Throwable {
            final String name = method.getName();
            if ("equals".equals(name)) return Boolean.valueOf(proxy == args[0] || target.equals(args[0]));
            if ("hashCode".equals(name)) return Integer.valueOf(target.hashCode());
            if ("toString".equals(name)) return target.toString();

            return method.invoke(target, args);
        }
    }

    private static final class DataSourceHandler extends BaseHandler {

        DataSourceHandler(DataSource target) {
            super(target);
        }

        @Override
        Object invokeTarget(Object proxy, Method method, Object[] args) throws Throwable {
            final Object result = call(method, args);
            if ("getConnection".equals(method.getName()) && result instanceof Connection) {
                return StardustJdbc.wrap((Connection) result);
            }

            return result;
        }
    }

    private static final class ConnectionHandler extends BaseHandler {

        private final ConnectionInfo info;

        ConnectionHandler(Connection target) {
            super(target);
            this.info = new ConnectionInfo(target);
        }

        @Override
        Object invokeTarget(Object proxy, Method method, Object[] args) throws Throwable {
            final Object result = call(method, args);

            final String name = method.getName();
            if (("createStatement".equals(name) || "prepareStatement".equals(name) || "prepareCall".equals(name))
                    && result instanceof Statement) {
                String sql = null;
                if (args != null && args.length > 0 && args[0] instanceof String) sql = (String) args[0];

                return StardustJdbc.wrapStatement((Statement) result, info, sql);
            }

            return result;
        }
    }

    private static final class StatementHandler extends BaseHandler {

        private final ConnectionInfo info;
        private final String preparedSql;
        private final List<String> batchSql = new ArrayList<String>();

        StatementHandler(Statement target, ConnectionInfo info, String sql) {
            super(target);
            this.info = info;
            this.preparedSql = sql;
        }

        @Override
        Object invokeTarget(Object proxy, Method method, Object[] args) throws Throwable {
            final String name = method.getName();

            if ("addBatch".equals(name) && args != null && args.length == 1 && args[0] instanceof String) {
                batchSql.add((String) args[0]);
                return call(method, args);
            }
            if ("clearBatch".equals(name)) batchSql.clear();

            if (!EXECUTE_METHODS.contains(name)) return call(method, args);

            String sql = preparedSql;
            if (args != null && args.length > 0 && args[0] instanceof String) {
                sql = (String) args[0];
            } else if (sql == null && !batchSql.isEmpty()) {
                sql = batchSql.get(0);
            }

            return execute(method, args, sql);
        }

        private Object execute(Method method, Object[] args, String sql) throws Throwable {
            Span span = null;
            try {
                final StardustTracer tracer = StardustTracer.getDefault();
                if (tracer != null && tracer.isAutoInstrument()) {
                    final String action = sql == null || sql.trim().length() == 0 ? "batch" : sqlAction(sql);
                    final String tables = sql == null ? "" : sqlTables(sql);
                    span = tracer.newSpan(dbSpanName(info.system(), action, tables));
                    span.setTag(sql == null ? "" : sql);
                }
            } catch (Throwable ignored) {
                // Instrumentation must never break business calls, and a half-created span must not leak.
                if (span != null) span.finish();
                span = null;
            }

            try {
                return call(method, args);
            } catch (InvocationTargetException e) {
                if (span != null) span.setError(e.getTargetException());
                throw e;
            } catch (Throwable e) {
                if (span != null) span.setError(e);
                throw e;
            } finally {
                if (span != null) span.finish();
            }
        }
    }

    private static final class ConnectionInfo {

        private final Connection connection;
        private volatile String system;

        ConnectionInfo(Connection connection) {
            this.connection = connection;
        }

        String system() {
            String value = system;
            if (value == null) {
                value = detectSystem(connection);
                system = value;
            }

            return value;
        }
    }

    private static String detectSystem(Connection connection) {
        if (connection == null) return "unknown";

        String product = null;
        String url = null;
        try {
            final DatabaseMetaData metadata = connection.getMetaData();
            if (metadata != null) {
                product = metadata.getDatabaseProductName();
                url = metadata.getURL();
            }
        } catch (Throwable ignored) {
            // Fall back to the connection class name.
        }

        final String normalized = normalizeSystem(product);
        if (normalized.length() > 0) return normalized;

        final String fromUrl = normalizeSystem(url);
        if (fromUrl.length() > 0) return fromUrl;

        final String className = connection.getClass().getName();
        final String fromClass = normalizeSystem(className);
        if (fromClass.length() > 0) return fromClass;

        return "unknown";
    }

    private static String normalizeSystem(String value) {
        if (value == null) return "";

        final String text = value.toLowerCase(Locale.ROOT);
        if (text.indexOf("sqlite") >= 0) return "sqlite";
        if (text.indexOf("mariadb") >= 0 || text.indexOf("mysql") >= 0) return "mysql";
        if (text.indexOf("postgres") >= 0) return "postgres";
        if (text.indexOf("microsoft") >= 0 || text.indexOf("sql server") >= 0 || text.indexOf("sqlserver") >= 0) return "sqlserver";
        if (text.indexOf("oracle") >= 0) return "oracle";
        if (text.indexOf("clickhouse") >= 0) return "clickhouse";
        if (text.indexOf("kingbase") >= 0) return "kingbase";
        if (text.indexOf("dameng") >= 0) return "dm";
        if (text.indexOf("db2") >= 0) return "db2";
        if (text.indexOf("h2") >= 0) return "h2";
        if (value.indexOf(':') >= 0 || value.indexOf('/') >= 0 || value.indexOf(' ') >= 0
                || value.indexOf('.') >= 0) {
            return "";
        }

        return text.trim();
    }

    private static String dbSpanName(String system, String action, String tables) {
        final StringBuilder name = new StringBuilder("db:");
        name.append(system == null || system.length() == 0 ? "unknown" : system);
        name.append(':');
        name.append(action == null || action.length() == 0 ? "exec" : action);
        if (tables != null && tables.length() > 0) name.append(':').append(tables);

        return name.toString();
    }

    private static String sqlAction(String sql) {
        final String text = stripLeadingComments(sql);
        int end = 0;
        while (end < text.length()) {
            final char c = text.charAt(end);
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_') {
                end++;
            } else {
                break;
            }
        }
        if (end == 0) return "exec";

        final String action = text.substring(0, end).toLowerCase(Locale.ROOT);

        return "with".equals(action) ? "select" : action;
    }

    private static String stripLeadingComments(String sql) {
        String text = sql == null ? "" : sql.trim();
        while (text.length() > 0) {
            if (text.startsWith("--")) {
                final int index = text.indexOf('\n');
                if (index < 0) return "";
                text = text.substring(index + 1).trim();
            } else if (text.startsWith("/*")) {
                final int index = text.indexOf("*/");
                if (index < 0) return "";
                text = text.substring(index + 2).trim();
            } else {
                break;
            }
        }

        return text;
    }

    private static String sqlTables(String sql) {
        if (sql == null) return "";

        final Matcher matcher = TABLE_PATTERN.matcher(sql);
        final Set<String> tables = new LinkedHashSet<String>();
        while (matcher.find()) {
            final String table = normalizeTableName(matcher.group(1));
            if (table.length() > 0) tables.add(table);
        }

        final StringBuilder value = new StringBuilder();
        for (String table : tables) {
            if (value.length() > 0) value.append(',');
            value.append(table);
        }

        return value.toString();
    }

    private static String normalizeTableName(String value) {
        String name = value == null ? "" : value.trim();
        name = stripQuotes(name);

        final int dot = name.lastIndexOf('.');
        if (dot >= 0) name = stripQuotes(name.substring(dot + 1));

        final Matcher shard = SHARD_PATTERN.matcher(name);
        if (shard.matches()) name = shard.group(1);

        return name;
    }

    private static String stripQuotes(String value) {
        int start = 0;
        int end = value.length();
        while (start < end) {
            final char c = value.charAt(start);
            if (c == '`' || c == '"' || c == '[') {
                start++;
            } else {
                break;
            }
        }
        while (end > start) {
            final char c = value.charAt(end - 1);
            if (c == '`' || c == '"' || c == ']') {
                end--;
            } else {
                break;
            }
        }

        return value.substring(start, end);
    }
}
