"""Redis 自动埋点

替换 ``redis.Redis.execute_command``（同步客户端），Span 名称 ``redis:{COMMAND}``，
Tag 为命令参数摘要，客户端异常记为错误。redis 未安装时静默跳过。
"""

import importlib
import sys
import threading
from typing import Any

from . import get_tracer, is_suppressed

_lock = threading.RLock()
_target = None
_original = None

_MAX_ARGS = 16
_MAX_SUMMARY = 512


def patch() -> None:
    """安装 Redis 埋点（幂等）"""
    global _target, _original
    with _lock:
        module = sys.modules.get("redis")
        if module is None:
            try:
                module = importlib.import_module("redis")
            except Exception:
                return

        client_cls = getattr(module, "Redis", None)
        if client_cls is None:
            return

        original = getattr(client_cls, "execute_command", None)
        if original is None or getattr(original, "__stardust_instrumented__", False):
            return

        def execute_command(self, *args, **options):
            tracer = get_tracer()
            if tracer is None or is_suppressed() or not args:
                return original(self, *args, **options)

            command = _command_name(args[0])
            try:
                span = tracer.new_span("redis:%s" % command)
            except Exception:
                return original(self, *args, **options)

            span.tag = _args_summary(args[1:])
            with span:
                return original(self, *args, **options)

        execute_command.__stardust_instrumented__ = True
        execute_command.__wrapped__ = original
        client_cls.execute_command = execute_command
        _target = client_cls
        _original = original


def unpatch() -> None:
    """卸载 Redis 埋点"""
    global _target, _original
    with _lock:
        if _target is not None and _original is not None:
            try:
                if getattr(
                    getattr(_target, "execute_command", None),
                    "__stardust_instrumented__",
                    False,
                ):
                    _target.execute_command = _original
            except Exception:
                pass
        _target = None
        _original = None


def _command_name(value: Any) -> str:
    if isinstance(value, bytes):
        try:
            value = value.decode("ascii", "ignore")
        except Exception:
            value = ""
    text = str(value or "").strip().upper()
    return text.split()[0] if text else "UNKNOWN"


def _decode(value: Any) -> str:
    if isinstance(value, bytes):
        try:
            return value.decode("utf-8", "replace")
        except Exception:
            return repr(value)
    return str(value)


def _args_summary(args: Any) -> str:
    parts = []
    for index, value in enumerate(args):
        if index >= _MAX_ARGS:
            parts.append("...")
            break
        parts.append(_decode(value))
    summary = " ".join(parts)
    if len(summary) > _MAX_SUMMARY:
        summary = summary[:_MAX_SUMMARY]
    return summary
