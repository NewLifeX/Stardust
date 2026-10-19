package stardust

import (
	"context"
	"database/sql"
	"database/sql/driver"
	"fmt"
	"regexp"
	"strings"
)

// WrapDriver 包装 database/sql 驱动，为连接、语句与事务创建 db:{system}:{action}:{tables} 片段。
// 常见用法：sql.Register("name", stardust.WrapDriver(&sqlite3.SQLiteDriver{}, tracer))。
// 传入 nil 返回 nil；重复包装时原样返回。
func WrapDriver(d driver.Driver, tracer *Tracer) driver.Driver {
	if d == nil {
		return nil
	}
	if _, ok := d.(*tracedDriver); ok {
		return d
	}
	return &tracedDriver{driver: d, tracer: tracer, system: detectDBSystem(d)}
}

// WrapDriver 使用当前追踪器包装驱动。
func (t *Tracer) WrapDriver(d driver.Driver) driver.Driver {
	return WrapDriver(d, t)
}

// WrapConnector 包装驱动连接器，适合 sql.OpenDB 一行接入。
// connector 为 nil 时返回 nil；重复包装时原样返回。
func WrapConnector(connector driver.Connector, tracer *Tracer) driver.Connector {
	if connector == nil {
		return nil
	}
	if _, ok := connector.(*tracedConnector); ok {
		return connector
	}
	return &tracedConnector{
		connector: connector,
		tracer:    tracer,
		system:    detectDBSystem(connector.Driver()),
	}
}

// WrapConnector 使用当前追踪器包装驱动连接器。
func (t *Tracer) WrapConnector(connector driver.Connector) driver.Connector {
	return WrapConnector(connector, t)
}

// OpenDB 使用包装后的驱动打开数据库，一行接入自动埋点。
//
// 示例：
//
//	db, err := stardust.OpenDB(&sqlite3.SQLiteDriver{}, "file:test.db", tracer)
func OpenDB(d driver.Driver, dataSourceName string, tracer *Tracer) (*sql.DB, error) {
	if d == nil {
		return nil, fmt.Errorf("stardust: nil driver")
	}
	connector := &driverConnector{driver: d, dataSourceName: dataSourceName}
	return sql.OpenDB(WrapConnector(connector, tracer)), nil
}

// OpenDB 使用当前追踪器打开数据库。
func (t *Tracer) OpenDB(d driver.Driver, dataSourceName string) (*sql.DB, error) {
	return OpenDB(d, dataSourceName, t)
}

// driverConnector 把普通 driver.Driver 适配为 driver.Connector
type driverConnector struct {
	driver         driver.Driver
	dataSourceName string
}

// Connect 打开底层连接
func (c *driverConnector) Connect(ctx context.Context) (driver.Conn, error) {
	return c.driver.Open(c.dataSourceName)
}

// Driver 返回底层驱动
func (c *driverConnector) Driver() driver.Driver {
	return c.driver
}

// tracedDriver 驱动包装
type tracedDriver struct {
	driver driver.Driver
	tracer *Tracer
	system string
}

// Open 打开连接并包装
func (d *tracedDriver) Open(name string) (driver.Conn, error) {
	conn, err := d.driver.Open(name)
	if err != nil {
		return nil, err
	}
	return wrapConn(conn, d.tracer, d.system), nil
}

// OpenConnector 返回包装后的连接器，优先复用驱动自身的 OpenConnector
func (d *tracedDriver) OpenConnector(name string) (driver.Connector, error) {
	connector, err := openDriverConnector(d.driver, name)
	if err != nil {
		return nil, err
	}
	return &tracedConnector{connector: connector, tracer: d.tracer, system: d.system}, nil
}

func openDriverConnector(d driver.Driver, name string) (driver.Connector, error) {
	if contextDriver, ok := d.(driver.DriverContext); ok {
		return contextDriver.OpenConnector(name)
	}
	return &driverConnector{driver: d, dataSourceName: name}, nil
}

// tracedConnector 连接器包装
type tracedConnector struct {
	connector driver.Connector
	tracer    *Tracer
	system    string
}

// Connect 打开并包装连接
func (c *tracedConnector) Connect(ctx context.Context) (driver.Conn, error) {
	conn, err := c.connector.Connect(ctx)
	if err != nil {
		return nil, err
	}
	return wrapConn(conn, c.tracer, c.system), nil
}

// Driver 返回包装后的驱动
func (c *tracedConnector) Driver() driver.Driver {
	return WrapDriver(c.connector.Driver(), c.tracer)
}

func wrapConn(conn driver.Conn, tracer *Tracer, system string) driver.Conn {
	if conn == nil {
		return nil
	}
	if _, ok := conn.(*tracedConn); ok {
		return conn
	}
	return &tracedConn{conn: conn, tracer: tracer, system: system}
}

// tracedConn 连接包装，覆盖 database/sql 的可选接口
type tracedConn struct {
	conn   driver.Conn
	tracer *Tracer
	system string
}

// Prepare 创建语句并包装
func (c *tracedConn) Prepare(query string) (driver.Stmt, error) {
	return c.PrepareContext(context.Background(), query)
}

// PrepareContext 创建语句，记录 prepare 片段
func (c *tracedConn) PrepareContext(ctx context.Context, query string) (driver.Stmt, error) {
	ctx, span := newInstrumentSpan(ctx, c.tracer, dbSpanName(c.system, query))
	if span != nil {
		span.Tag = query
	}

	var (
		stmt driver.Stmt
		err  error
	)
	if preparer, ok := c.conn.(driver.ConnPrepareContext); ok {
		stmt, err = preparer.PrepareContext(ctx, query)
	} else {
		stmt, err = c.conn.Prepare(query)
	}
	if err == driver.ErrSkip {
		return nil, err
	}
	if err != nil {
		finishInstrumentSpan(span, err)
		return nil, err
	}
	finishInstrumentSpan(span, nil)
	return wrapStmt(ctx, stmt, c.tracer, c.system, query), nil
}

// Close 关闭连接
func (c *tracedConn) Close() error {
	return c.conn.Close()
}

// Begin 开启事务
func (c *tracedConn) Begin() (driver.Tx, error) {
	return c.BeginTx(context.Background(), driver.TxOptions{})
}

// BeginTx 开启事务并包装
func (c *tracedConn) BeginTx(ctx context.Context, opts driver.TxOptions) (driver.Tx, error) {
	ctx, span := newInstrumentSpan(ctx, c.tracer, dbSpanNameAction(c.system, "begin", ""))

	var (
		tx  driver.Tx
		err error
	)
	if beginner, ok := c.conn.(driver.ConnBeginTx); ok {
		tx, err = beginner.BeginTx(ctx, opts)
	} else {
		if ctx != nil {
			select {
			case <-ctx.Done():
				err = ctx.Err()
			default:
			}
		}
		if err == nil {
			tx, err = c.conn.Begin()
		}
	}
	if err == driver.ErrSkip {
		return nil, err
	}
	if err != nil {
		finishInstrumentSpan(span, err)
		return nil, err
	}
	finishInstrumentSpan(span, nil)
	return &tracedTx{tx: tx, tracer: c.tracer, system: c.system, ctx: ctx}, nil
}

// ExecContext 直接执行 SQL；底层不支持时返回 driver.ErrSkip 以便 database/sql 回退
func (c *tracedConn) ExecContext(ctx context.Context, query string, args []driver.NamedValue) (driver.Result, error) {
	execer, ok := c.conn.(driver.ExecerContext)
	if !ok {
		return nil, driver.ErrSkip
	}

	_, span := newInstrumentSpan(ctx, c.tracer, dbSpanName(c.system, query))
	if span != nil {
		span.Tag = query
	}

	result, err := execer.ExecContext(ctx, query, args)
	if err == driver.ErrSkip {
		// 交由 database/sql 回退到预编译路径，不记录片段
		return nil, err
	}
	finishInstrumentSpan(span, err)
	return result, err
}

// QueryContext 直接查询；底层不支持时返回 driver.ErrSkip 以便 database/sql 回退
func (c *tracedConn) QueryContext(ctx context.Context, query string, args []driver.NamedValue) (driver.Rows, error) {
	queryer, ok := c.conn.(driver.QueryerContext)
	if !ok {
		return nil, driver.ErrSkip
	}

	_, span := newInstrumentSpan(ctx, c.tracer, dbSpanName(c.system, query))
	if span != nil {
		span.Tag = query
	}

	rows, err := queryer.QueryContext(ctx, query, args)
	if err == driver.ErrSkip {
		return nil, err
	}
	finishInstrumentSpan(span, err)
	return rows, err
}

// Ping 探活；底层不支持时视为成功
func (c *tracedConn) Ping(ctx context.Context) error {
	pinger, ok := c.conn.(driver.Pinger)
	if !ok {
		return nil
	}

	_, span := newInstrumentSpan(ctx, c.tracer, dbSpanNameAction(c.system, "ping", ""))
	err := pinger.Ping(ctx)
	finishInstrumentSpan(span, err)
	return err
}

// ResetSession 重置会话；底层不支持时视为无需重置
func (c *tracedConn) ResetSession(ctx context.Context) error {
	resetter, ok := c.conn.(driver.SessionResetter)
	if !ok {
		return nil
	}
	return resetter.ResetSession(ctx)
}

// IsValid 判断连接是否有效；底层不支持时视为有效
func (c *tracedConn) IsValid() bool {
	validator, ok := c.conn.(driver.Validator)
	if !ok {
		return true
	}
	return validator.IsValid()
}

// CheckNamedValue 转发底层连接级命名参数检查；底层不支持时交回默认转换
func (c *tracedConn) CheckNamedValue(value *driver.NamedValue) error {
	checker, ok := c.conn.(driver.NamedValueChecker)
	if !ok {
		return driver.ErrSkip
	}
	return checker.CheckNamedValue(value)
}

// tracedStmt 语句包装基类，仅实现 driver.Stmt 的标准方法。
// 上下文相关接口按底层语句的实际能力动态附加，避免无条件实现导致 database/sql
// 走上“返回 ErrSkip”的错误路径。
type tracedStmt struct {
	stmt   driver.Stmt
	tracer *Tracer
	system string
	query  string
	ctx    context.Context
}

// tracedStmtMarker 用于识别已包装的语句
type tracedStmtMarker interface {
	markTracedStmt()
}

func (*tracedStmt) markTracedStmt() {}

func wrapStmt(ctx context.Context, stmt driver.Stmt, tracer *Tracer, system, query string) driver.Stmt {
	if stmt == nil {
		return nil
	}
	if _, ok := stmt.(tracedStmtMarker); ok {
		return stmt
	}

	base := &tracedStmt{stmt: stmt, tracer: tracer, system: system, query: query, ctx: ctx}
	_, execOK := stmt.(driver.StmtExecContext)
	_, queryOK := stmt.(driver.StmtQueryContext)
	switch {
	case execOK && queryOK:
		return &tracedStmtExecQuery{tracedStmt: base}
	case execOK:
		return &tracedStmtExec{tracedStmt: base}
	case queryOK:
		return &tracedStmtQuery{tracedStmt: base}
	default:
		return base
	}
}

// Close 关闭语句
func (s *tracedStmt) Close() error {
	return s.stmt.Close()
}

// NumInput 返回参数个数
func (s *tracedStmt) NumInput() int {
	return s.stmt.NumInput()
}

// Exec 执行语句
func (s *tracedStmt) Exec(args []driver.Value) (driver.Result, error) {
	_, span := newInstrumentSpan(s.context(), s.tracer, dbSpanName(s.system, s.query))
	if span != nil {
		span.Tag = s.query
	}
	result, err := s.stmt.Exec(args)
	finishInstrumentSpan(span, err)
	return result, err
}

// Query 查询语句
func (s *tracedStmt) Query(args []driver.Value) (driver.Rows, error) {
	_, span := newInstrumentSpan(s.context(), s.tracer, dbSpanName(s.system, s.query))
	if span != nil {
		span.Tag = s.query
	}
	rows, err := s.stmt.Query(args)
	finishInstrumentSpan(span, err)
	return rows, err
}

// context 返回预编译时上下文，缺失时使用后台上下文
func (s *tracedStmt) context() context.Context {
	if s.ctx != nil {
		return s.ctx
	}
	return context.Background()
}

// ExecContext 执行语句；底层不支持时返回 driver.ErrSkip
func (s *tracedStmtExec) ExecContext(ctx context.Context, args []driver.NamedValue) (driver.Result, error) {
	return s.tracedStmt.execContext(ctx, args)
}

// QueryContext 查询语句；底层不支持时返回 driver.ErrSkip
func (s *tracedStmtQuery) QueryContext(ctx context.Context, args []driver.NamedValue) (driver.Rows, error) {
	return s.tracedStmt.queryContext(ctx, args)
}

// tracedStmtExec 支持 StmtExecContext 的语句包装
type tracedStmtExec struct {
	*tracedStmt
}

// tracedStmtQuery 支持 StmtQueryContext 的语句包装
type tracedStmtQuery struct {
	*tracedStmt
}

// tracedStmtExecQuery 同时支持 StmtExecContext 与 StmtQueryContext 的语句包装
type tracedStmtExecQuery struct {
	*tracedStmt
}

// ExecContext 执行语句
func (s *tracedStmtExecQuery) ExecContext(ctx context.Context, args []driver.NamedValue) (driver.Result, error) {
	return s.tracedStmt.execContext(ctx, args)
}

// QueryContext 查询语句
func (s *tracedStmtExecQuery) QueryContext(ctx context.Context, args []driver.NamedValue) (driver.Rows, error) {
	return s.tracedStmt.queryContext(ctx, args)
}

func (s *tracedStmt) execContext(ctx context.Context, args []driver.NamedValue) (driver.Result, error) {
	execer, ok := s.stmt.(driver.StmtExecContext)
	if !ok {
		return nil, driver.ErrSkip
	}

	_, span := newInstrumentSpan(ctx, s.tracer, dbSpanName(s.system, s.query))
	if span != nil {
		span.Tag = s.query
	}
	result, err := execer.ExecContext(ctx, args)
	if err == driver.ErrSkip {
		return nil, err
	}
	finishInstrumentSpan(span, err)
	return result, err
}

func (s *tracedStmt) queryContext(ctx context.Context, args []driver.NamedValue) (driver.Rows, error) {
	queryer, ok := s.stmt.(driver.StmtQueryContext)
	if !ok {
		return nil, driver.ErrSkip
	}

	_, span := newInstrumentSpan(ctx, s.tracer, dbSpanName(s.system, s.query))
	if span != nil {
		span.Tag = s.query
	}
	rows, err := queryer.QueryContext(ctx, args)
	if err == driver.ErrSkip {
		return nil, err
	}
	finishInstrumentSpan(span, err)
	return rows, err
}

// CheckNamedValue 转发底层命名参数检查；底层不支持时交回默认转换
func (s *tracedStmt) CheckNamedValue(value *driver.NamedValue) error {
	checker, ok := s.stmt.(driver.NamedValueChecker)
	if !ok {
		return driver.ErrSkip
	}
	return checker.CheckNamedValue(value)
}

// ColumnConverter 转发底层列转换器；底层不支持时使用默认转换器
func (s *tracedStmt) ColumnConverter(index int) driver.ValueConverter {
	converter, ok := s.stmt.(driver.ColumnConverter)
	if !ok {
		return driver.DefaultParameterConverter
	}
	return converter.ColumnConverter(index)
}

// tracedTx 事务包装
type tracedTx struct {
	tx     driver.Tx
	tracer *Tracer
	system string
	ctx    context.Context
}

// Commit 提交事务
func (t *tracedTx) Commit() error {
	_, span := newInstrumentSpan(t.ctx, t.tracer, dbSpanNameAction(t.system, "commit", ""))
	err := t.tx.Commit()
	finishInstrumentSpan(span, err)
	return err
}

// Rollback 回滚事务
func (t *tracedTx) Rollback() error {
	_, span := newInstrumentSpan(t.ctx, t.tracer, dbSpanNameAction(t.system, "rollback", ""))
	err := t.tx.Rollback()
	finishInstrumentSpan(span, err)
	return err
}

// ========== SQL 解析 ==========

var (
	sqlTablePattern    = regexp.MustCompile(`(?i)\b(?:from|into|update|join|drop\s+table|truncate\s+table)\s+([\w\.\"` + "`" + `\[\]]+)`)
	shardSuffixPattern = regexp.MustCompile(`^(.+?)[_\-\.](?:19|20)\d{2}(?:0[1-9]|1[0-2])?(?:0[1-9]|[12]\d|3[01])?$`)
)

// dbSpanName 生成 db:{system}:{action}:{tables} 片段名称
func dbSpanName(system, query string) string {
	if system == "" {
		system = "unknown"
	}
	return dbSpanNameAction(system, sqlAction(query), strings.Join(sqlTables(query), ","))
}

// dbSpanNameAction 生成指定动作的片段名称，tables 为空时省略表名段
func dbSpanNameAction(system, action, tables string) string {
	if system == "" {
		system = "unknown"
	}
	if action == "" {
		action = "exec"
	}
	name := "db:" + system + ":" + action
	if tables != "" {
		name += ":" + tables
	}
	return name
}

// sqlAction 从 SQL 文本提取动作，取首关键字，WITH 视为 select
func sqlAction(query string) string {
	text := strings.TrimSpace(query)
	for text != "" {
		switch {
		case strings.HasPrefix(text, "--"):
			if index := strings.IndexByte(text, '\n'); index >= 0 {
				text = strings.TrimSpace(text[index+1:])
				continue
			}
			return "exec"
		case strings.HasPrefix(text, "/*"):
			if index := strings.Index(text, "*/"); index >= 0 {
				text = strings.TrimSpace(text[index+2:])
				continue
			}
			return "exec"
		}
		break
	}

	end := 0
	for end < len(text) {
		c := text[end]
		if (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' {
			end++
			continue
		}
		break
	}
	if end == 0 {
		return "exec"
	}

	action := strings.ToLower(text[:end])
	if action == "with" {
		return "select"
	}
	return action
}

// sqlTables 提取 SQL 中涉及的表名，去重并去掉日期分表后缀
func sqlTables(query string) []string {
	matches := sqlTablePattern.FindAllStringSubmatch(query, -1)
	if len(matches) == 0 {
		return nil
	}

	tables := make([]string, 0, len(matches))
	seen := make(map[string]struct{}, len(matches))
	for _, match := range matches {
		name := normalizeTableName(match[1])
		if name == "" {
			continue
		}
		key := strings.ToLower(name)
		if _, ok := seen[key]; ok {
			continue
		}
		seen[key] = struct{}{}
		tables = append(tables, name)
	}
	return tables
}

// normalizeTableName 去掉引号与库名前缀，并裁剪日期分表后缀
func normalizeTableName(name string) string {
	name = strings.TrimSpace(name)
	name = strings.Trim(name, "`\"[]")
	if index := strings.LastIndexByte(name, '.'); index >= 0 {
		name = name[index+1:]
	}
	name = strings.Trim(name, "`\"[]")
	if name == "" {
		return ""
	}
	if match := shardSuffixPattern.FindStringSubmatch(name); match != nil {
		name = match[1]
	}
	return name
}

// detectDBSystem 从驱动类型推断数据库类型，用于 db:{system} 片段前缀
func detectDBSystem(d driver.Driver) string {
	if d == nil {
		return "unknown"
	}

	full := strings.ToLower(fmt.Sprintf("%T", d))
	switch {
	case strings.Contains(full, "sqlite"):
		return "sqlite"
	case strings.Contains(full, "mysql"):
		return "mysql"
	case strings.Contains(full, "pgx"), strings.Contains(full, "postgres"), strings.Contains(full, "lib/pq"), strings.Contains(full, "libpq"):
		return "postgres"
	case strings.Contains(full, "mssql"), strings.Contains(full, "sqlserver"):
		return "sqlserver"
	case strings.Contains(full, "oracle"), strings.Contains(full, "godror"), strings.Contains(full, "oci"):
		return "oracle"
	case strings.Contains(full, "clickhouse"):
		return "clickhouse"
	case strings.Contains(full, "kingbase"):
		return "kingbase"
	case strings.Contains(full, "dameng"):
		return "dm"
	}

	name := strings.TrimPrefix(fmt.Sprintf("%T", d), "*")
	if index := strings.LastIndexAny(name, "./"); index >= 0 {
		name = name[index+1:]
	}
	name = strings.ToLower(name)
	name = strings.TrimSuffix(name, "driver")
	name = strings.TrimSuffix(name, "sql")
	if name == "" {
		return "unknown"
	}
	return name
}
