package com.newlife.stardust;

import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.sql.Connection;
import java.sql.DatabaseMetaData;
import java.sql.PreparedStatement;
import java.sql.SQLException;
import java.sql.Statement;

import javax.sql.DataSource;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotSame;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class StardustJdbcTest {

    @AfterEach
    void clearDefault() {
        StardustTracer.setDefault(null);
    }

    @Test
    @DisplayName("包装 DataSource 后为查询创建标准数据库 Span")
    void wrapsDataSourceAndQuery() throws Exception {
        final TestTracer tracer = new TestTracer();
        StardustTracer.setDefault(tracer);

        final PreparedStatement prepared = statement(PreparedStatement.class, false);
        final Statement plain = statement(Statement.class, false);
        final Connection target = connection("SQLite", plain, prepared);
        final DataSource raw = dataSource(target);

        final DataSource wrapped = StardustJdbc.wrap(raw);
        assertNotSame(raw, wrapped);
        assertTrue(Proxy.isProxyClass(wrapped.getClass()));
        assertSame(wrapped, StardustJdbc.wrap(wrapped));

        final Connection connection = wrapped.getConnection();
        assertTrue(Proxy.isProxyClass(connection.getClass()));

        final PreparedStatement statement = connection.prepareStatement("select * from users where id=?");
        statement.setInt(1, 1);
        statement.executeQuery();

        assertEquals(1, tracer.spans.size());
        final Span span = tracer.last();
        assertEquals("db:sqlite:select:users", span.getName());
        assertEquals("select * from users where id=?", span.getTag());
        assertEquals("", span.getError());
        assertTrue(span.isFinished());
    }

    @Test
    @DisplayName("解析 update/join、日期分表与批量语句")
    void parsesSqlActionsAndTables() throws Exception {
        final TestTracer tracer = new TestTracer();
        StardustTracer.setDefault(tracer);

        final Statement plain = statement(Statement.class, false);
        final Connection connection = StardustJdbc.wrap(connection("SQLite", plain, statement(PreparedStatement.class, false)));

        final Statement statement = connection.createStatement();
        statement.executeUpdate("update orders o join users u on o.user_id=u.id set o.status=1");
        assertEquals("db:sqlite:update:orders,users", tracer.last().getName());

        statement.addBatch("insert into order_items_202401(id) values(1)");
        statement.executeBatch();
        assertEquals("db:sqlite:insert:order_items", tracer.last().getName());

        statement.execute("truncate table audit_log");
        assertEquals("db:sqlite:truncate:audit_log", tracer.last().getName());

        assertEquals(3, tracer.spans.size());
    }

    @Test
    @DisplayName("驱动异常标记 Span 并原样抛出 SQLException")
    void marksSqlException() {
        final TestTracer tracer = new TestTracer();
        StardustTracer.setDefault(tracer);

        final Statement failing = statement(Statement.class, true);
        final Connection connection = StardustJdbc.wrap(connection("MySQL", failing, statement(PreparedStatement.class, true)));

        final SQLException thrown = assertThrows(SQLException.class,
                () -> connection.createStatement().executeUpdate("delete from users"));

        assertEquals("boom", thrown.getMessage());
        final Span span = tracer.last();
        assertEquals("db:mysql:delete:users", span.getName());
        assertTrue(span.getError().contains("boom"));
        assertTrue(span.isFinished());
        assertEquals(1, tracer.spans.size());
    }

    @Test
    @DisplayName("包装 Connection 且关闭自动埋点时不创建 Span")
    void wrapsConnectionAndRespectsOptOut() throws Exception {
        final Statement plain = statement(Statement.class, false);
        final Connection connection = StardustJdbc.wrap(connection("PostgreSQL", plain, statement(PreparedStatement.class, false)));

        assertTrue(Proxy.isProxyClass(connection.getClass()));
        assertSame(connection, StardustJdbc.wrap(connection));

        final TestTracer tracer = new TestTracer();
        tracer.setAutoInstrument(false);
        StardustTracer.setDefault(tracer);

        connection.createStatement().executeUpdate("delete from users");
        assertEquals(0, tracer.spans.size());

        StardustTracer.setDefault(null);
        connection.createStatement().executeUpdate("delete from users");
        assertEquals(0, tracer.spans.size());
    }

    private static DataSource dataSource(final Connection connection) {
        return (DataSource) Proxy.newProxyInstance(
                DataSource.class.getClassLoader(),
                new Class<?>[]{DataSource.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        if ("getConnection".equals(method.getName())) return connection;
                        if ("toString".equals(method.getName())) return "TestDataSource";
                        if ("hashCode".equals(method.getName())) return Integer.valueOf(System.identityHashCode(proxy));
                        if ("equals".equals(method.getName())) return Boolean.valueOf(proxy == args[0]);

                        return defaultValue(method.getReturnType());
                    }
                });
    }

    private static Connection connection(final String product, final Statement plain, final PreparedStatement prepared) {
        return (Connection) Proxy.newProxyInstance(
                Connection.class.getClassLoader(),
                new Class<?>[]{Connection.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        final String name = method.getName();
                        if ("getMetaData".equals(name)) return metadata(product);
                        if ("createStatement".equals(name)) return plain;
                        if ("prepareStatement".equals(name)) return prepared;
                        if ("prepareCall".equals(name)) return prepared;
                        if ("getAutoCommit".equals(name)) return Boolean.TRUE;
                        if ("isClosed".equals(name)) return Boolean.FALSE;
                        if ("toString".equals(name)) return "TestConnection";
                        if ("hashCode".equals(name)) return Integer.valueOf(System.identityHashCode(proxy));
                        if ("equals".equals(name)) return Boolean.valueOf(proxy == args[0]);

                        return defaultValue(method.getReturnType());
                    }
                });
    }

    private static DatabaseMetaData metadata(final String product) {
        return (DatabaseMetaData) Proxy.newProxyInstance(
                DatabaseMetaData.class.getClassLoader(),
                new Class<?>[]{DatabaseMetaData.class},
                new InvocationHandler() {
                    @Override
                    public Object invoke(Object proxy, Method method, Object[] args) {
                        if ("getDatabaseProductName".equals(method.getName())) return product;
                        if ("getURL".equals(method.getName())) return "jdbc:" + product.toLowerCase() + "://localhost/test";
                        if ("toString".equals(method.getName())) return "TestMetadata";
                        if ("hashCode".equals(method.getName())) return Integer.valueOf(System.identityHashCode(proxy));
                        if ("equals".equals(method.getName())) return Boolean.valueOf(proxy == args[0]);

                        return defaultValue(method.getReturnType());
                    }
                });
    }

    private static <T extends Statement> T statement(final Class<T> type, final boolean fail) {
        final InvocationHandler handler = new InvocationHandler() {
            @Override
            public Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
                final String name = method.getName();
                if ("execute".equals(name) || "executeQuery".equals(name) || "executeUpdate".equals(name)
                        || "executeLargeUpdate".equals(name) || "executeBatch".equals(name)
                        || "executeLargeBatch".equals(name)) {
                    if (fail) throw new SQLException("boom", "STATE");

                    if ("execute".equals(name)) return Boolean.FALSE;
                    if ("executeUpdate".equals(name)) return Integer.valueOf(1);
                    if ("executeLargeUpdate".equals(name)) return Long.valueOf(1L);
                    if ("executeBatch".equals(name)) return new int[0];
                    if ("executeLargeBatch".equals(name)) return new long[0];

                    return null;
                }
                if ("addBatch".equals(name) || "clearBatch".equals(name) || "close".equals(name)) return null;
                if ("isClosed".equals(name)) return Boolean.FALSE;
                if ("toString".equals(name)) return "TestStatement";
                if ("hashCode".equals(name)) return Integer.valueOf(System.identityHashCode(proxy));
                if ("equals".equals(name)) return Boolean.valueOf(proxy == args[0]);

                return defaultValue(method.getReturnType());
            }
        };

        return type.cast(Proxy.newProxyInstance(type.getClassLoader(), new Class<?>[]{type}, handler));
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
