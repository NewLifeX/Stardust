"""Web 入口自动埋点

提供 WSGI / ASGI 中间件，并在安装时自动识别已经导入的 Flask、Django、Starlette：

- Flask：包装 ``Flask.wsgi_app``
- Django：包装 ``BaseHandler.get_response``
- Starlette：包装 ``Starlette.__call__``

若框架在安装之后才导入，请显式使用 ``StardustWSGIMiddleware`` 或
``StardustASGIMiddleware``。同一请求只产生一个入口 Span（通过上下文标记去重）。
"""

import sys
import threading
from contextvars import ContextVar
from typing import Any, Callable, Optional

from ..trace_context import find_traceparent
from . import get_tracer, is_suppressed

_ENTRY_ENVIRON_KEY = "stardust.entry_span"
_ENTRY_SCOPE_KEY = "stardust.entry_span"

_lock = threading.RLock()
_entry_active = ContextVar("stardust_entry_active", default=0)
_patches = []


def patch() -> None:
    """安装 Web 入口埋点（仅针对已导入的框架，幂等）"""
    with _lock:
        for installer in (_patch_flask, _patch_django, _patch_starlette):
            try:
                installer()
            except Exception:
                pass


def unpatch() -> None:
    """卸载 Web 入口埋点，恢复框架原始方法"""
    with _lock:
        for owner, attr, original in reversed(_patches):
            try:
                if getattr(getattr(owner, attr, None), "__stardust_instrumented__", False):
                    setattr(owner, attr, original)
            except Exception:
                pass
        del _patches[:]


def _status_code(value: Any) -> int:
    if isinstance(value, int):
        return value
    try:
        return int(str(value).split(" ", 1)[0])
    except Exception:
        return 0


def _wsgi_url(environ: Any) -> str:
    try:
        scheme = environ.get("wsgi.url_scheme", "http") or "http"
        host = environ.get("HTTP_HOST", "") or ""
        if not host:
            host = environ.get("SERVER_NAME", "") or ""
            port = environ.get("SERVER_PORT", "") or ""
            if port and not (
                (scheme == "http" and port == "80")
                or (scheme == "https" and port == "443")
            ):
                host = "%s:%s" % (host, port)
        path = environ.get("PATH_INFO", "") or "/"
        query = environ.get("QUERY_STRING", "") or ""
        return "%s://%s%s%s" % (scheme, host, path, ("?" + query) if query else "")
    except Exception:
        return environ.get("PATH_INFO", "/") or "/"


def _asgi_url(scope: Any) -> str:
    try:
        scheme = scope.get("scheme", "http") or "http"
        path = scope.get("path", "") or "/"
        query = scope.get("query_string", b"") or b""
        if isinstance(query, bytes):
            query = query.decode("latin-1")
        host = ""
        for key, value in scope.get("headers") or []:
            name = key.decode("latin-1") if isinstance(key, bytes) else str(key)
            if name.lower() == "host":
                host = value.decode("latin-1") if isinstance(value, bytes) else str(value)
                break
        if not host:
            server = scope.get("server") or ("", 0)
            if server and server[0]:
                host = server[0]
                if len(server) > 1 and server[1] and not (
                    (scheme == "http" and server[1] == 80)
                    or (scheme == "https" and server[1] == 443)
                ):
                    host = "%s:%s" % (host, server[1])
        return "%s://%s%s%s" % (scheme, host, path, ("?" + query) if query else "")
    except Exception:
        return scope.get("path", "/") or "/"


def _new_server_span(tracer: Any, method: str, path: str, full_url: str, trace_parent: Any) -> Any:
    span = tracer.new_span(
        "%s %s" % (method, path),
        parent_id=trace_parent.span_id if trace_parent is not None else "",
        trace_id=trace_parent.trace_id if trace_parent is not None else None,
    )
    span.tag = "%s %s" % (method, full_url)
    return span


class StardustWSGIMiddleware:
    """WSGI 入口中间件

    Args:
        app: WSGI 应用
        tracer: 追踪器，缺省使用全局安装的追踪器
    """

    def __init__(self, app: Callable, tracer: Optional[Any] = None):
        self.app = app
        self.tracer = tracer

    def __call__(self, environ: Any, start_response: Callable) -> Any:
        tracer = self.tracer or get_tracer()
        if tracer is None or is_suppressed() or _entry_active.get() > 0:
            return self.app(environ, start_response)
        return _invoke_wsgi(
            tracer, lambda env, sr: self.app(env, sr), environ, start_response
        )


def _invoke_wsgi(
    tracer: Any,
    app_call: Callable,
    environ: Any,
    start_response: Callable,
) -> Any:
    if environ.get(_ENTRY_ENVIRON_KEY):
        return app_call(environ, start_response)

    method = environ.get("REQUEST_METHOD", "GET") or "GET"
    path = environ.get("PATH_INFO", "") or "/"
    full_url = _wsgi_url(environ)
    trace_parent = find_traceparent(environ)

    try:
        span = _new_server_span(tracer, method, path, full_url, trace_parent)
    except Exception:
        return app_call(environ, start_response)

    status_holder = {}

    def wrapped_start_response(status, headers, exc_info=None):
        status_holder["status"] = status
        return start_response(status, headers, exc_info)

    previous = environ.get(_ENTRY_ENVIRON_KEY)
    environ[_ENTRY_ENVIRON_KEY] = True
    token = _entry_active.set(_entry_active.get() + 1)
    try:
        with span:
            result = app_call(environ, wrapped_start_response)
            code = _status_code(status_holder.get("status"))
            if code >= 500:
                span.set_error("HTTP %d" % code)
            return result
    finally:
        _entry_active.reset(token)
        if previous is None:
            environ.pop(_ENTRY_ENVIRON_KEY, None)
        else:
            environ[_ENTRY_ENVIRON_KEY] = previous


class StardustASGIMiddleware:
    """ASGI 入口中间件

    Args:
        app: ASGI 应用
        tracer: 追踪器，缺省使用全局安装的追踪器
    """

    def __init__(self, app: Callable, tracer: Optional[Any] = None):
        self.app = app
        self.tracer = tracer

    async def __call__(self, scope: Any, receive: Callable, send: Callable) -> Any:
        tracer = self.tracer or get_tracer()
        if (
            tracer is None
            or is_suppressed()
            or scope.get("type") != "http"
            or _entry_active.get() > 0
        ):
            return await self.app(scope, receive, send)
        return await _invoke_asgi(
            tracer, lambda s: self.app(scope, receive, s), scope, send
        )


async def _invoke_asgi(
    tracer: Any,
    app_call: Callable,
    scope: Any,
    send: Callable,
) -> Any:
    if scope.get(_ENTRY_SCOPE_KEY):
        return await app_call(send)

    method = scope.get("method", "GET") or "GET"
    path = scope.get("path", "") or "/"
    full_url = _asgi_url(scope)
    trace_parent = find_traceparent(scope.get("headers"))

    try:
        span = _new_server_span(tracer, method, path, full_url, trace_parent)
    except Exception:
        return await app_call(send)

    status_holder = {}

    async def wrapped_send(message):
        try:
            if message.get("type") == "http.response.start":
                status_holder["status"] = message.get("status")
        except Exception:
            pass
        await send(message)

    previous = scope.get(_ENTRY_SCOPE_KEY)
    scope[_ENTRY_SCOPE_KEY] = True
    token = _entry_active.set(_entry_active.get() + 1)
    try:
        with span:
            result = await app_call(wrapped_send)
            code = _status_code(status_holder.get("status"))
            if code >= 500:
                span.set_error("HTTP %d" % code)
            return result
    finally:
        _entry_active.reset(token)
        if previous is None:
            scope.pop(_ENTRY_SCOPE_KEY, None)
        else:
            scope[_ENTRY_SCOPE_KEY] = previous


def _patch_flask() -> None:
    flask = sys.modules.get("flask")
    if flask is None:
        return
    app_cls = getattr(flask, "Flask", None)
    if app_cls is None:
        return

    original = app_cls.wsgi_app
    if getattr(original, "__stardust_instrumented__", False):
        return

    def wsgi_app(self, environ, start_response):
        tracer = get_tracer()
        if tracer is None or is_suppressed() or _entry_active.get() > 0:
            return original(self, environ, start_response)
        return _invoke_wsgi(
            tracer, lambda env, sr: original(self, env, sr), environ, start_response
        )

    wsgi_app.__stardust_instrumented__ = True
    wsgi_app.__wrapped__ = original
    app_cls.wsgi_app = wsgi_app
    _patches.append((app_cls, "wsgi_app", original))


def _patch_django() -> None:
    if "django" not in sys.modules:
        return

    try:
        from django.core.handlers.base import BaseHandler
    except Exception:
        return

    original = BaseHandler.get_response
    if getattr(original, "__stardust_instrumented__", False):
        return

    def get_response(self, request):
        tracer = get_tracer()
        if tracer is None or is_suppressed() or _entry_active.get() > 0:
            return original(self, request)

        method = getattr(request, "method", "GET") or "GET"
        path = getattr(request, "path", "") or "/"
        meta = getattr(request, "META", None) or {}
        trace_parent = find_traceparent(meta)
        try:
            full_url = request.build_absolute_uri()
        except Exception:
            full_url = path

        try:
            span = _new_server_span(tracer, method, path, full_url, trace_parent)
        except Exception:
            return original(self, request)

        token = _entry_active.set(_entry_active.get() + 1)
        try:
            with span:
                response = original(self, request)
                code = _status_code(getattr(response, "status_code", 0))
                if code >= 500:
                    span.set_error("HTTP %d" % code)
                return response
        finally:
            _entry_active.reset(token)

    get_response.__stardust_instrumented__ = True
    get_response.__wrapped__ = original
    BaseHandler.get_response = get_response
    _patches.append((BaseHandler, "get_response", original))


def _patch_starlette() -> None:
    module = sys.modules.get("starlette.applications")
    if module is None:
        return
    app_cls = getattr(module, "Starlette", None)
    if app_cls is None:
        return

    original = app_cls.__call__
    if getattr(original, "__stardust_instrumented__", False):
        return

    async def __call__(self, scope, receive, send):
        tracer = get_tracer()
        if (
            tracer is None
            or is_suppressed()
            or scope.get("type") != "http"
            or _entry_active.get() > 0
        ):
            return await original(self, scope, receive, send)
        return await _invoke_asgi(
            tracer, lambda s: original(self, scope, receive, s), scope, send
        )

    __call__.__stardust_instrumented__ = True
    __call__.__wrapped__ = original
    app_cls.__call__ = __call__
    _patches.append((app_cls, "__call__", original))
