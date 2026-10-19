package stardust

import (
	"context"
	"database/sql"
	"database/sql/driver"
	"errors"
	"io"
	"testing"
)

// ========== 测试用假驱动 ==========

type fakeDriver struct {
	failExec    bool
	skipExec    bool
	contextStmt bool
}

func (d *fakeDriver) Open(name string) (driver.Conn, error) {
	return &fakeConn{driver: d}, nil
}

func (d *fakeDriver) OpenConnector(name string) (driver.Connector, error) {
	return &fakeConnector{driver: d, dsn: name}, nil
}

type fakeConnector struct {
	driver *fakeDriver
	dsn    string
}

func (c *fakeConnector) Connect(ctx context.Context) (driver.Conn, error) {
	return c.driver.Open(c.dsn)
}

func (c *fakeConnector) Driver() driver.Driver {
	return c.driver
}

type fakeConn struct {
	driver *fakeDriver
	closed bool
}

func (c *fakeConn) Prepare(query string) (driver.Stmt, error) {
	if c.driver.contextStmt {
		return &fakeContextStmt{query: query}, nil
	}
	return &fakeStmt{query: query}, nil
}

func (c *fakeConn) Close() error {
	c.closed = true
	return nil
}

func (c *fakeConn) Begin() (driver.Tx, error) {
	return &fakeTx{}, nil
}

func (c *fakeConn) BeginTx(ctx context.Context, opts driver.TxOptions) (driver.Tx, error) {
	return &fakeTx{}, nil
}

func (c *fakeConn) ExecContext(ctx context.Context, query string, args []driver.NamedValue) (driver.Result, error) {
	if c.driver.skipExec {
		return nil, driver.ErrSkip
	}
	if c.driver.failExec {
		return nil, errors.New("exec failed")
	}
	return driver.RowsAffected(1), nil
}

func (c *fakeConn) QueryContext(ctx context.Context, query string, args []driver.NamedValue) (driver.Rows, error) {
	return &fakeRows{}, nil
}

func (c *fakeConn) Ping(ctx context.Context) error {
	return nil
}

func (c *fakeConn) ResetSession(ctx context.Context) error {
	return nil
}

func (c *fakeConn) IsValid() bool {
	return !c.closed
}

type fakeStmt struct {
	query string
}

func (s *fakeStmt) Close() error  { return nil }
func (s *fakeStmt) NumInput() int { return -1 }

func (s *fakeStmt) Exec(args []driver.Value) (driver.Result, error) {
	return driver.RowsAffected(1), nil
}

func (s *fakeStmt) Query(args []driver.Value) (driver.Rows, error) {
	return &fakeRows{}, nil
}

type fakeContextStmt struct {
	query string
}

func (s *fakeContextStmt) Close() error  { return nil }
func (s *fakeContextStmt) NumInput() int { return -1 }

func (s *fakeContextStmt) Exec(args []driver.Value) (driver.Result, error) {
	return driver.RowsAffected(1), nil
}

func (s *fakeContextStmt) Query(args []driver.Value) (driver.Rows, error) {
	return &fakeRows{}, nil
}

func (s *fakeContextStmt) ExecContext(ctx context.Context, args []driver.NamedValue) (driver.Result, error) {
	return driver.RowsAffected(1), nil
}

func (s *fakeContextStmt) QueryContext(ctx context.Context, args []driver.NamedValue) (driver.Rows, error) {
	return &fakeRows{}, nil
}

type fakeTx struct {
	committed  bool
	rolledBack bool
}

func (t *fakeTx) Commit() error {
	t.committed = true
	return nil
}

func (t *fakeTx) Rollback() error {
	t.rolledBack = true
	return nil
}

type fakeRows struct {
	done bool
}

func (r *fakeRows) Columns() []string { return []string{"id"} }
func (r *fakeRows) Close() error      { return nil }

func (r *fakeRows) Next(dest []driver.Value) error {
	if r.done {
		return io.EOF
	}
	r.done = true
	dest[0] = int64(1)
	return nil
}

func newDBTestTracer(t *testing.T) *Tracer {
	t.Helper()

	tracer := NewTracer("http://127.0.0.1:1", "TestApp", "TestSecret")
	t.Cleanup(func() {
		tracer.SetAutoInstrument(false)
		SetDefaultTracer(nil)
	})
	return tracer
}

func openFakeDB(t *testing.T, tracer *Tracer, d *fakeDriver) *sql.DB {
	t.Helper()

	db, err := OpenDB(d, "fake-dsn", tracer)
	if err != nil {
		t.Fatalf("OpenDB failed: %v", err)
	}
	t.Cleanup(func() { db.Close() })
	return db
}

// ========== 测试 ==========

func TestDBSpanNameParsing(t *testing.T) {
	tests := []struct {
		query string
		want  string
	}{
		{"SELECT * FROM users", "db:sqlite:select:users"},
		{"select * from User_20240101 where Id=1", "db:sqlite:select:User"},
		{"INSERT INTO orders (Id) VALUES (1)", "db:sqlite:insert:orders"},
		{"insert into `order_detail` values (1)", "db:sqlite:insert:order_detail"},
		{"UPDATE [users] SET Name='a'", "db:sqlite:update:users"},
		{"delete from logs_202401", "db:sqlite:delete:logs"},
		{"SELECT u.* FROM users u JOIN orders o ON u.Id=o.UserId", "db:sqlite:select:users,orders"},
		{"DROP TABLE temp_data_2024", "db:sqlite:drop:temp_data"},
		{"TRUNCATE TABLE audit_log_20240101", "db:sqlite:truncate:audit_log"},
		{"SELECT * FROM app.users", "db:sqlite:select:users"},
		{"  -- comment\n SELECT * FROM users", "db:sqlite:select:users"},
		{"PRAGMA journal_mode", "db:sqlite:pragma"},
		{"", "db:sqlite:exec"},
	}

	for _, tt := range tests {
		if got := dbSpanName("sqlite", tt.query); got != tt.want {
			t.Errorf("dbSpanName(%q) = %q, want %q", tt.query, got, tt.want)
		}
	}
}

func TestDBExecContextSpan(t *testing.T) {
	tracer := newDBTestTracer(t)
	db := openFakeDB(t, tracer, &fakeDriver{})

	parent := tracer.NewSpan("parent", "")
	ctx := ContextWithSpan(context.Background(), parent)

	if _, err := db.ExecContext(ctx, "INSERT INTO User_20240101 (Name) VALUES (?)", "alice"); err != nil {
		t.Fatalf("ExecContext failed: %v", err)
	}

	builder := findBuilder(tracer, "db:fake:insert:User")
	if builder == nil {
		t.Fatal("db span was not recorded")
	}
	if builder.Total != 1 {
		t.Errorf("span total = %d, want 1", builder.Total)
	}
	sample := builder.Samples[0]
	if sample.TraceID != parent.TraceID {
		t.Errorf("span trace = %s, want parent trace %s", sample.TraceID, parent.TraceID)
	}
	if sample.ParentID != parent.ID {
		t.Errorf("span parent = %s, want %s", sample.ParentID, parent.ID)
	}
	if sample.Tag != "INSERT INTO User_20240101 (Name) VALUES (?)" {
		t.Errorf("span tag = %q, want SQL text", sample.Tag)
	}
}

func TestDBQueryContextSpan(t *testing.T) {
	tracer := newDBTestTracer(t)
	db := openFakeDB(t, tracer, &fakeDriver{})

	rows, err := db.QueryContext(context.Background(),
		"SELECT u.* FROM users u JOIN orders o ON u.Id = o.UserId")
	if err != nil {
		t.Fatalf("QueryContext failed: %v", err)
	}
	defer rows.Close()

	if total := builderTotal(tracer, "db:fake:select:users,orders"); total != 1 {
		t.Errorf("span total = %d, want 1", total)
	}
}

func TestDBExecErrorSpan(t *testing.T) {
	tracer := newDBTestTracer(t)
	db := openFakeDB(t, tracer, &fakeDriver{failExec: true})

	if _, err := db.ExecContext(context.Background(), "DELETE FROM sessions"); err == nil {
		t.Fatal("ExecContext should return the driver error")
	}

	builder := findBuilder(tracer, "db:fake:delete:sessions")
	if builder == nil {
		t.Fatal("db span was not recorded")
	}
	if builder.Errors != 1 {
		t.Errorf("expected 1 error span, got %d", builder.Errors)
	}
}

// 驱动返回 ErrSkip 时，应交给 database/sql 回退到预编译路径，
// 且不产生一个空的多余片段。
func TestDBExecContextErrSkipFallback(t *testing.T) {
	tracer := newDBTestTracer(t)
	db := openFakeDB(t, tracer, &fakeDriver{skipExec: true})

	if _, err := db.ExecContext(context.Background(), "INSERT INTO users (Name) VALUES (?)", "alice"); err != nil {
		t.Fatalf("ExecContext fallback failed: %v", err)
	}

	// prepare 片段 + 语句 Exec 片段，共 2 个；直接执行产生的 ErrSkip 片段被丢弃
	if total := builderTotal(tracer, "db:fake:insert:users"); total != 2 {
		t.Errorf("span total = %d, want 2 (prepare + stmt exec)", total)
	}
}

func TestDBPreparedStatementContext(t *testing.T) {
	tracer := newDBTestTracer(t)
	db := openFakeDB(t, tracer, &fakeDriver{contextStmt: true})

	stmt, err := db.PrepareContext(context.Background(), "UPDATE users SET Name=?")
	if err != nil {
		t.Fatalf("PrepareContext failed: %v", err)
	}
	defer stmt.Close()

	if _, err := stmt.ExecContext(context.Background(), "alice"); err != nil {
		t.Fatalf("stmt.ExecContext failed: %v", err)
	}

	if total := builderTotal(tracer, "db:fake:update:users"); total != 2 {
		t.Errorf("span total = %d, want 2 (prepare + exec)", total)
	}
}

func TestDBTransactionSpans(t *testing.T) {
	tracer := newDBTestTracer(t)
	db := openFakeDB(t, tracer, &fakeDriver{})

	tx, err := db.BeginTx(context.Background(), nil)
	if err != nil {
		t.Fatalf("BeginTx failed: %v", err)
	}
	if err := tx.Commit(); err != nil {
		t.Fatalf("Commit failed: %v", err)
	}

	if total := builderTotal(tracer, "db:fake:begin"); total != 1 {
		t.Errorf("begin span total = %d, want 1", total)
	}
	if total := builderTotal(tracer, "db:fake:commit"); total != 1 {
		t.Errorf("commit span total = %d, want 1", total)
	}
}

func TestDBPingSpan(t *testing.T) {
	tracer := newDBTestTracer(t)
	db := openFakeDB(t, tracer, &fakeDriver{})

	if err := db.PingContext(context.Background()); err != nil {
		t.Fatalf("PingContext failed: %v", err)
	}

	if total := builderTotal(tracer, "db:fake:ping"); total != 1 {
		t.Errorf("ping span total = %d, want 1", total)
	}
}

func TestDBWrapConnector(t *testing.T) {
	tracer := newDBTestTracer(t)

	connector := &fakeConnector{driver: &fakeDriver{}, dsn: "fake-dsn"}
	db := sql.OpenDB(WrapConnector(connector, tracer))
	defer db.Close()

	if _, err := db.ExecContext(context.Background(), "UPDATE accounts SET Balance=0"); err != nil {
		t.Fatalf("ExecContext failed: %v", err)
	}
	if total := builderTotal(tracer, "db:fake:update:accounts"); total != 1 {
		t.Errorf("span total = %d, want 1", total)
	}
}

func TestDBRegisteredDriver(t *testing.T) {
	tracer := newDBTestTracer(t)

	const driverName = "stardust-fake-registered"
	sql.Register(driverName, WrapDriver(&fakeDriver{}, tracer))

	db, err := sql.Open(driverName, "fake-dsn")
	if err != nil {
		t.Fatalf("sql.Open failed: %v", err)
	}
	defer db.Close()

	if _, err := db.ExecContext(context.Background(), "DELETE FROM tokens"); err != nil {
		t.Fatalf("ExecContext failed: %v", err)
	}
	if total := builderTotal(tracer, "db:fake:delete:tokens"); total != 1 {
		t.Errorf("span total = %d, want 1", total)
	}
}

func TestDBWrapIdempotentAndNilSafe(t *testing.T) {
	tracer := newDBTestTracer(t)

	if WrapDriver(nil, tracer) != nil {
		t.Error("WrapDriver(nil) should return nil")
	}
	if WrapConnector(nil, tracer) != nil {
		t.Error("WrapConnector(nil) should return nil")
	}
	if _, err := OpenDB(nil, "dsn", tracer); err == nil {
		t.Error("OpenDB(nil) should return an error")
	}

	driverInstance := WrapDriver(&fakeDriver{}, tracer)
	if WrapDriver(driverInstance, tracer) != driverInstance {
		t.Error("WrapDriver should be idempotent")
	}

	connector := WrapConnector(&fakeConnector{driver: &fakeDriver{}, dsn: "dsn"}, tracer)
	if WrapConnector(connector, tracer) != connector {
		t.Error("WrapConnector should be idempotent")
	}
}

func TestTraceRedisSpan(t *testing.T) {
	tracer := newDBTestTracer(t)

	parent := tracer.NewSpan("parent", "")
	ctx := ContextWithSpan(context.Background(), parent)

	err := tracer.TraceRedis(ctx, "get", func(ctx context.Context) error {
		if SpanFromContext(ctx) == nil {
			t.Error("callback context should carry the redis span")
		}
		return nil
	})
	if err != nil {
		t.Fatalf("TraceRedis failed: %v", err)
	}

	builder := findBuilder(tracer, "redis:GET")
	if builder == nil {
		t.Fatal("redis span was not recorded")
	}
	if builder.Samples[0].ParentID != parent.ID {
		t.Errorf("redis parent = %s, want %s", builder.Samples[0].ParentID, parent.ID)
	}

	expected := errors.New("redis timeout")
	err = tracer.TraceRedisWithTag(context.Background(), "set user:1 value", "user:1", func(ctx context.Context) error {
		return expected
	})
	if err != expected {
		t.Errorf("TraceRedisWithTag should return callback error, got %v", err)
	}
	builder = findBuilder(tracer, "redis:SET")
	if builder == nil || builder.Errors != 1 {
		t.Fatal("redis error span should be recorded")
	}
	if builder.ErrorSamples[0].Tag != "user:1" {
		t.Errorf("redis tag = %q, want user:1", builder.ErrorSamples[0].Tag)
	}
}
