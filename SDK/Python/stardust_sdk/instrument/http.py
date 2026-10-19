"""HTTP 客户端自动埋点

- requests：替换 Session.send，覆盖 requests 的全部高层 API
- urllib：替换 urllib.request.urlopen

Span 名称 `METHOD host/path`，Tag 为完整 URL，4xx/5xx 与网络异常记为错误。
所有采集逻辑自身异常都不会影响原始请求。
"""

import threading
from typing import Any
from urllib.parse import urlsplit

from ..trace_context import format_traceparent
from . import get_tracer, is_self_request, is_suppressed

_lock = threading.RLock()
_original_send = None
_original_urlopen = None


def patch() -> None:
    """安装 HTTP 客户端埋点（幂等）"""
    with _lock:
        try:
            _patch_requests()
        except Exception:
            pass
        try:
            _patch_urllib()
        except Exception:
            pass


def unpatch() -> None:
    """卸载 HTTP 客户端埋点"""
    global _original_send, _original_urlopen
    with _lock:
        if _original_send is not None:
            try:
                import requests.sessions as sessions

                if getattr(sessions.Session.send, "__stardust_instrumented__", False):
                    sessions.Session.send = _original_send
            except Exception:
                pass
            _original_send = None

        if _original_urlopen is not None:
            try:
                import urllib.request as urllib_request

                if getattr(urllib_request.urlopen, "__stardust_instrumented__", False):
                    urllib_request.urlopen = _original_urlopen
            except Exception:
                pass
            _original_urlopen = None


def _has_header(headers: Any, name: str) -> bool:
    try:
        for key in headers:
            if isinstance(key, str) and key.lower() == name:
                return True
    except Exception:
        return True
    return False


def _urllib_has_header(request: Any, name: str) -> bool:
    """urllib Request.has_header 对普通头是大小写敏感的，这里统一按小写判断"""
    for headers in (
        getattr(request, "headers", None),
        getattr(request, "unredirected_hdrs", None),
    ):
        if headers and _has_header(headers, name):
            return True
    return False


def _span_name(method: str, url: str) -> str:
    parsed = urlsplit(url)
    host = parsed.netloc.rsplit("@", 1)[-1]
    return "%s %s%s" % (
        (method or "GET").upper(),
        host,
        parsed.path or "/",
    )


def _finish_with_status(span: Any, status: Any) -> None:
    try:
        code = int(status or 0)
    except Exception:
        return
    if code >= 400:
        span.set_error("HTTP %d" % code)


def _patch_requests() -> None:
    global _original_send
    import requests.sessions as sessions

    current = sessions.Session.send
    if getattr(current, "__stardust_instrumented__", False):
        return

    _original_send = current

    def send(self, request, **kwargs):
        tracer = get_tracer()
        url = getattr(request, "url", "") or ""
        if tracer is None or is_suppressed() or is_self_request(url):
            return current(self, request, **kwargs)

        method = getattr(request, "method", None) or "GET"
        try:
            span = tracer.new_span(_span_name(method, url))
        except Exception:
            return current(self, request, **kwargs)

        span.tag = url
        try:
            headers = getattr(request, "headers", None)
            if headers is not None and not _has_header(headers, "traceparent"):
                headers["traceparent"] = format_traceparent(span.trace_id, span.id)
        except Exception:
            pass

        with span:
            response = current(self, request, **kwargs)
            _finish_with_status(span, getattr(response, "status_code", None))
            return response

    send.__stardust_instrumented__ = True
    send.__wrapped__ = current
    sessions.Session.send = send


def _is_http_url(url: Any) -> bool:
    try:
        return urlsplit(url).scheme.lower() in ("http", "https")
    except Exception:
        return False


def _patch_urllib() -> None:
    global _original_urlopen
    import urllib.request as urllib_request

    current = urllib_request.urlopen
    if getattr(current, "__stardust_instrumented__", False):
        return

    _original_urlopen = current

    def urlopen(url, data=None, *args, **kwargs):
        tracer = get_tracer()
        request = url if isinstance(url, urllib_request.Request) else None
        full_url = request.full_url if request is not None else url

        if (
            tracer is None
            or is_suppressed()
            or not isinstance(full_url, str)
            or not _is_http_url(full_url)
            or is_self_request(full_url)
        ):
            return current(url, data, *args, **kwargs)

        method = (
            request.get_method()
            if request is not None
            else ("POST" if data is not None else "GET")
        )
        try:
            span = tracer.new_span(_span_name(method, full_url))
        except Exception:
            return current(url, data, *args, **kwargs)

        span.tag = full_url
        call_url, call_data = url, data
        try:
            if request is not None:
                if not _urllib_has_header(request, "traceparent"):
                    request.add_unredirected_header(
                        "traceparent", format_traceparent(span.trace_id, span.id)
                    )
            else:
                wrapped = urllib_request.Request(full_url, data=data)
                wrapped.add_unredirected_header(
                    "traceparent", format_traceparent(span.trace_id, span.id)
                )
                call_url, call_data = wrapped, None
        except Exception:
            pass

        with span:
            response = current(call_url, call_data, *args, **kwargs)
            status = getattr(response, "status", None)
            if status is None:
                try:
                    status = response.getcode()
                except Exception:
                    status = None
            _finish_with_status(span, status)
            return response

    urlopen.__stardust_instrumented__ = True
    urlopen.__wrapped__ = current
    urllib_request.urlopen = urlopen
