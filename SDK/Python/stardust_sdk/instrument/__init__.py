"""自动埋点安装器

统一安装/卸载 HTTP 客户端、Web 入口、数据库与 Redis 自动埋点。
所有安装动作都是幂等的，缺失的第三方模块会被静默跳过。
"""

import threading
from contextlib import contextmanager
from contextvars import ContextVar
from typing import Any, Iterator, List
from urllib.parse import urlsplit

# 星尘服务端自身接口，不创建 Span，也不注入 traceparent
SELF_PATH_PREFIXES = ("/App/", "/Trace/")

_lock = threading.RLock()
_tracer = None
_installed = False

_suppress = ContextVar("stardust_suppress_tracing", default=0)


def get_tracer() -> Any:
    """获取当前全局追踪器，未安装时返回 None"""
    return _tracer


def is_installed() -> bool:
    """是否已安装自动埋点"""
    return _installed


def _modules() -> List[Any]:
    from . import db, http, redis, web

    return [http, web, db, redis]


def install(tracer: Any) -> None:
    """安装自动埋点

    Args:
        tracer: 追踪器实例，埋点执行时通过全局引用获取
    """
    global _tracer, _installed
    with _lock:
        _tracer = tracer
        if _installed:
            return
        _installed = True
        for module in _modules():
            try:
                module.patch()
            except Exception:
                pass


def uninstall() -> None:
    """卸载自动埋点，恢复原始实现"""
    global _tracer, _installed
    with _lock:
        _tracer = None
        if not _installed:
            return
        _installed = False
        for module in reversed(_modules()):
            try:
                module.unpatch()
            except Exception:
                pass


def is_suppressed() -> bool:
    """当前上下文是否禁止埋点"""
    return _suppress.get() > 0


@contextmanager
def suppress_tracing() -> Iterator[None]:
    """在代码块内禁止创建自动埋点 Span"""
    token = _suppress.set(_suppress.get() + 1)
    try:
        yield
    finally:
        _suppress.reset(token)


def is_self_request(url: Any) -> bool:
    """判断 URL 是否指向星尘服务端自身接口（/App/、/Trace/）

    Args:
        url: 完整 URL 或路径

    Returns:
        命中自身接口时返回 True
    """
    if not url or not isinstance(url, str):
        return False

    try:
        path = urlsplit(url).path or ""
    except Exception:
        return False

    if not path.startswith("/"):
        path = "/" + path

    return any(path.startswith(prefix) for prefix in SELF_PATH_PREFIXES)


__all__ = [
    "SELF_PATH_PREFIXES",
    "get_tracer",
    "install",
    "is_installed",
    "is_self_request",
    "is_suppressed",
    "suppress_tracing",
    "uninstall",
]
