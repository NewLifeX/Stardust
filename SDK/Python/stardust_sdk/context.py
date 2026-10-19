"""基于 contextvars 的追踪上下文

使用 contextvars 保存当前 Span 栈，天然支持多线程与 asyncio 协程隔离，
替代无法在协程间正确传播的 threading.local。
"""

from contextvars import ContextVar, Token
from typing import Any, Optional, Tuple

_span_stack: ContextVar = ContextVar("stardust_span_stack", default=())


def current_span() -> Optional[Any]:
    """获取当前活动 Span，没有时返回 None"""
    stack = _span_stack.get()
    return stack[-1] if stack else None


def span_stack() -> Tuple[Any, ...]:
    """获取当前 Span 栈（只读元组，栈底在前）"""
    return _span_stack.get()


def push_span(span: Any) -> Token:
    """将 Span 压入当前上下文栈，返回用于恢复的 token"""
    return _span_stack.set(_span_stack.get() + (span,))


def pop_span(token: Token) -> None:
    """按 token 恢复上下文栈

    Args:
        token: push_span 返回的 token
    """
    _span_stack.reset(token)


def current_trace_id() -> Optional[str]:
    """获取当前活动 Span 的 TraceId，没有时返回 None"""
    span = current_span()
    return getattr(span, "trace_id", None) if span is not None else None
