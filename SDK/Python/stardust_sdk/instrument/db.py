"""DB-API 数据库自动埋点

通过代理 Connection / Cursor 实现，无需修改业务代码：

- sqlite3：标准库，自动安装
- pymysql、psycopg2：已安装时自动导入并安装，缺失时静默跳过

Span 名称 ``db:{system}:{action}:{tables}``，Tag 为 SQL 文本。
驱动异常会原样抛出，返回值保持不变。
"""

import importlib
import re
import threading
from typing import Any

from . import get_tracer, is_suppressed

_lock = threading.RLock()
_originals = {}

_TARGETS = (
    ("sqlite3", "sqlite"),
    ("pymysql", "mysql"),
    ("psycopg2", "postgresql"),
)

_TABLE_PATTERNS = (
    re.compile(r"\bfrom\s+([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\bjoin\s+([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\binsert\s+into\s+([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\bupdate\s+([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\bdelete\s+from\s+([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\bdrop\s+table\s+(?:if\s+exists\s+)?([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\btruncate\s+table\s+([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\bcreate\s+table\s+(?:if\s+not\s+exists\s+)?([\w\.\"`\[\]]+)", re.IGNORECASE),
    re.compile(r"\balter\s+table\s+([\w\.\"`\[\]]+)", re.IGNORECASE),
)
_ACTION_PATTERN = re.compile(r"^\s*(?:/\*.*?\*/\s*)*([a-zA-Z]+)", re.DOTALL)
_DATE_SUFFIX_PATTERN = re.compile(r"_\d{4}(?:\d{2}(?:\d{2})?)?$")

_ACTIONS = {
    "select",
    "insert",
    "update",
    "delete",
    "replace",
    "merge",
    "create",
    "alter",
    "drop",
    "truncate",
    "call",
    "exec",
    "execute",
    "with",
}


def patch() -> None:
    """安装数据库埋点（幂等，缺失的驱动静默跳过）"""
    with _lock:
        for module_name, system in _TARGETS:
            try:
                module = importlib.import_module(module_name)
                original = getattr(module, "connect", None)
                if original is None or getattr(
                    original, "__stardust_instrumented__", False
                ):
                    continue

                module.connect = _make_connect(original, system)
                _originals[module_name] = (module, original)
            except Exception:
                continue


def unpatch() -> None:
    """卸载数据库埋点，恢复原始 connect"""
    with _lock:
        for module_name, (module, original) in list(_originals.items()):
            try:
                if getattr(getattr(module, "connect", None), "__stardust_instrumented__", False):
                    module.connect = original
            except Exception:
                pass
        _originals.clear()


def _make_connect(original: Any, system: str) -> Any:
    def connect(*args, **kwargs):
        return _ConnectionProxy(original(*args, **kwargs), system)

    connect.__stardust_instrumented__ = True
    connect.__wrapped__ = original
    return connect


def _sql_text(sql: Any) -> str:
    if isinstance(sql, str):
        return sql
    try:
        return str(sql)
    except Exception:
        return ""


def _action(sql: str) -> str:
    match = _ACTION_PATTERN.match(sql or "")
    if not match:
        return "exec"
    word = match.group(1).lower()
    return word if word in _ACTIONS else "exec"


def _clean_table(name: str) -> str:
    table = name.strip().strip("\"'`[]")
    if "." in table:
        table = table.split(".")[-1]
    table = table.strip("\"'`[]")
    table = _DATE_SUFFIX_PATTERN.sub("", table)
    return table


def _tables(sql: str) -> str:
    found = []
    for pattern in _TABLE_PATTERNS:
        for match in pattern.finditer(sql or ""):
            table = _clean_table(match.group(1))
            if table and table.lower() not in ("select", "where", "set", "values"):
                if table not in found:
                    found.append(table)
    return ",".join(found)


def _span_name(system: str, sql: Any) -> str:
    text = _sql_text(sql)
    tables = _tables(text)
    action = _action(text)
    if tables:
        return "db:%s:%s:%s" % (system, action, tables)
    return "db:%s:%s" % (system, action)


_OWN_ATTRS = frozenset(("_connection", "_cursor", "_tracer", "_system"))


class _CursorProxy:
    """DB-API Cursor 代理"""

    def __init__(self, cursor: Any, system: str):
        object.__setattr__(self, "_cursor", cursor)
        object.__setattr__(self, "_system", system)

    def execute(self, sql, parameters=None):
        if parameters is None:
            result = self._trace(sql, lambda: self._cursor.execute(sql))
        else:
            result = self._trace(sql, lambda: self._cursor.execute(sql, parameters))
        return self if result is self._cursor else result

    def executemany(self, sql, seq_of_parameters):
        result = self._trace(
            sql, lambda: self._cursor.executemany(sql, seq_of_parameters)
        )
        return self if result is self._cursor else result

    def _trace(self, sql, call):
        tracer = get_tracer()
        if tracer is None or is_suppressed():
            return call()
        try:
            span = tracer.new_span(_span_name(self._system, sql))
        except Exception:
            return call()
        span.tag = _sql_text(sql)
        with span:
            return call()

    def __iter__(self):
        return iter(self._cursor)

    def __enter__(self):
        enter = getattr(self._cursor, "__enter__", None)
        if enter is not None:
            enter()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        exit_method = getattr(self._cursor, "__exit__", None)
        if exit_method is not None:
            return exit_method(exc_type, exc_val, exc_tb)
        return False

    def __getattr__(self, name):
        return getattr(object.__getattribute__(self, "_cursor"), name)

    def __setattr__(self, name, value):
        if name in _OWN_ATTRS:
            object.__setattr__(self, name, value)
        else:
            setattr(self._cursor, name, value)


class _ConnectionProxy:
    """DB-API Connection 代理"""

    def __init__(self, connection: Any, system: str):
        object.__setattr__(self, "_connection", connection)
        object.__setattr__(self, "_system", system)

    def cursor(self, *args, **kwargs):
        return _CursorProxy(self._connection.cursor(*args, **kwargs), self._system)

    def execute(self, sql, parameters=None):
        def call():
            if parameters is None:
                return self._connection.execute(sql)
            return self._connection.execute(sql, parameters)

        return self._wrap_result(self._trace(sql, call))

    def executemany(self, sql, seq_of_parameters):
        def call():
            return self._connection.executemany(sql, seq_of_parameters)

        return self._wrap_result(self._trace(sql, call))

    def _trace(self, sql, call):
        tracer = get_tracer()
        if tracer is None or is_suppressed():
            return call()
        try:
            span = tracer.new_span(_span_name(self._system, sql))
        except Exception:
            return call()
        span.tag = _sql_text(sql)
        with span:
            return call()

    def _wrap_result(self, result):
        if result is not None and hasattr(result, "fetchone"):
            return _CursorProxy(result, self._system)
        return result

    def __enter__(self):
        enter = getattr(self._connection, "__enter__", None)
        if enter is not None:
            enter()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        exit_method = getattr(self._connection, "__exit__", None)
        if exit_method is not None:
            return exit_method(exc_type, exc_val, exc_tb)
        return False

    def __getattr__(self, name):
        return getattr(object.__getattribute__(self, "_connection"), name)

    def __setattr__(self, name, value):
        if name in _OWN_ATTRS:
            object.__setattr__(self, name, value)
        else:
            setattr(self._connection, name, value)
