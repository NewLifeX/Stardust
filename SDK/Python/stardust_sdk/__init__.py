"""星尘监控 Python SDK

提供 APM 监控和配置中心功能，并默认启用自动埋点。
"""

__version__ = "1.0.0"

from .tracer import StardustTracer, Span
from .context import current_span, span_stack
from .trace_context import (
    TraceParent,
    find_traceparent,
    format_traceparent,
    new_span_id,
    new_trace_id,
    parse_traceparent,
)
from .config_client import ConfigClient
from .client import StardustClient
from .instrument import install, uninstall
from .instrument.web import StardustASGIMiddleware, StardustWSGIMiddleware


def init(
    server: str,
    app_id: str,
    secret: str = "",
    scope: str = "",
    auto_instrument: bool = True,
    **kwargs
) -> StardustClient:
    """创建并启动星尘客户端（一行接入）。

    Args:
        server: 星尘服务器地址
        app_id: 应用标识
        secret: 应用密钥
        scope: 配置作用域
        auto_instrument: 是否启用自动埋点
        **kwargs: 透传给 StardustClient 的其他参数

    Returns:
        已启动的 StardustClient
    """
    client = StardustClient(
        server=server,
        app_id=app_id,
        secret=secret,
        scope=scope,
        auto_instrument=auto_instrument,
        **kwargs
    )
    client.start()
    return client


__all__ = [
    "StardustTracer",
    "Span",
    "ConfigClient",
    "StardustClient",
    "init",
    "StardustWSGIMiddleware",
    "StardustASGIMiddleware",
    "TraceParent",
    "current_span",
    "span_stack",
    "parse_traceparent",
    "format_traceparent",
    "find_traceparent",
    "new_trace_id",
    "new_span_id",
    "install",
    "uninstall",
]
