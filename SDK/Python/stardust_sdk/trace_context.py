"""W3C Trace Context（traceparent）工具

跨服务、跨语言链路统一使用 W3C Trace Context 标准头：

    traceparent: 00-<trace-id>-<span-id>-<flags>

- trace-id：32 位小写十六进制；为兼容 .NET 历史实现，解析时允许更短的非空十六进制串
- span-id：16 位小写十六进制
- flags：01 表示采样。SDK 不做头部采样，出站统一写 01

本模块只负责解析与格式化，不依赖 SDK 的其他模块。
"""

import re
import uuid
from typing import Any, Mapping, NamedTuple, Optional

TRACE_FLAG_SAMPLED = 0x01

_TRACE_ID_PATTERN = re.compile(r"^[0-9a-f]{1,32}$")
_SPAN_ID_PATTERN = re.compile(r"^[0-9a-f]{16}$")
_VERSION_PATTERN = re.compile(r"^[0-9a-f]{2}$")
_FLAGS_PATTERN = re.compile(r"^[0-9a-f]{2}$")

_ZERO_TRACE_ID = "0" * 32
_ZERO_SPAN_ID = "0" * 16


class TraceParent(NamedTuple):
    """解析后的 traceparent 信息"""

    trace_id: str
    span_id: str
    flags: int = TRACE_FLAG_SAMPLED
    version: str = "00"

    def __str__(self) -> str:
        return format_traceparent(self.trace_id, self.span_id, self.flags, self.version)


def new_trace_id() -> str:
    """生成 32 位小写十六进制 TraceId"""
    while True:
        value = uuid.uuid4().hex
        if value != _ZERO_TRACE_ID:
            return value


def new_span_id() -> str:
    """生成 16 位小写十六进制 SpanId"""
    while True:
        value = uuid.uuid4().hex[:16]
        if value != _ZERO_SPAN_ID:
            return value


def _normalize(value: Any) -> Optional[str]:
    """把 bytes/str 统一为去除首尾空白的字符串"""
    if value is None:
        return None
    if isinstance(value, bytes):
        try:
            value = value.decode("ascii")
        except Exception:
            return None
    if not isinstance(value, str):
        return None
    return value.strip()


def is_valid_trace_id(value: Any) -> bool:
    """判断是否为可接受的 TraceId（32 位以内非空十六进制）"""
    text = _normalize(value)
    if not text:
        return False
    text = text.lower()
    return bool(_TRACE_ID_PATTERN.match(text)) and text.strip("0") != ""


def is_valid_span_id(value: Any) -> bool:
    """判断是否为可接受的 SpanId（16 位非零十六进制）"""
    text = _normalize(value)
    if not text:
        return False
    text = text.lower()
    return bool(_SPAN_ID_PATTERN.match(text)) and text.strip("0") != ""


def parse_traceparent(value: Any) -> Optional[TraceParent]:
    """解析 traceparent 头

    Args:
        value: traceparent 头字符串或字节串

    Returns:
        解析成功时返回 TraceParent，缺失或非法时返回 None
    """
    text = _normalize(value)
    if not text:
        return None

    parts = text.split("-")
    if len(parts) < 4:
        return None

    version, trace_id, span_id, flags = parts[:4]
    version = version.lower()
    if version == "00" and len(parts) != 4:
        return None
    trace_id = trace_id.lower()
    span_id = span_id.lower()
    flags = flags.lower()

    if not _VERSION_PATTERN.match(version) or version == "ff":
        return None
    if not _TRACE_ID_PATTERN.match(trace_id) or trace_id.strip("0") == "":
        return None
    if not _SPAN_ID_PATTERN.match(span_id) or span_id.strip("0") == "":
        return None
    if not _FLAGS_PATTERN.match(flags):
        return None

    return TraceParent(trace_id, span_id, int(flags, 16), version)


def format_traceparent(
    trace_id: Any,
    span_id: Any,
    flags: Any = TRACE_FLAG_SAMPLED,
    version: str = "00",
) -> str:
    """格式化 traceparent 头

    较短的 TraceId 会原样保留，以兼容 .NET 历史实现并保持链路关联；SpanId 必须为 16 位。

    Args:
        trace_id: TraceId，非空十六进制串
        span_id: SpanId，非空十六进制串
        flags: 采样标志，int（如 1）或两位十六进制字符串
        version: 版本号，默认 00

    Returns:
        标准的 traceparent 字符串

    Raises:
        ValueError: 参数不是合法的十六进制串
    """
    text_trace = _normalize(trace_id)
    text_span = _normalize(span_id)
    if not text_trace or not is_valid_trace_id(text_trace):
        raise ValueError("invalid trace id: %r" % (trace_id,))
    if not text_span or not is_valid_span_id(text_span):
        raise ValueError("invalid span id: %r" % (span_id,))

    if isinstance(flags, str):
        text_flags = flags.strip().lower() or "01"
        flag_value = int(text_flags, 16)
    else:
        flag_value = int(flags) & 0xFF

    text_version = _normalize(version) or "00"
    if not _VERSION_PATTERN.match(text_version.lower()):
        raise ValueError("invalid version: %r" % (version,))

    return "%s-%s-%s-%02x" % (
        text_version.lower(),
        text_trace.lower(),
        text_span.lower(),
        flag_value,
    )


def find_traceparent(headers: Any) -> Optional[TraceParent]:
    """从请求头中查找并解析 traceparent

    支持三种输入：
    - 大小写不敏感的映射（dict 等），同时识别 HTTP_TRACEPARENT（WSGI/Django）
    - 二元组序列（ASGI [(b"traceparent", b"00-...")]）
    - None / 空

    Args:
        headers: 请求头集合

    Returns:
        解析成功时返回 TraceParent，否则返回 None
    """
    if not headers:
        return None

    try:
        if isinstance(headers, Mapping):
            for key in headers:
                if not isinstance(key, str):
                    continue
                name = key.lower()
                if name in ("traceparent", "http_traceparent"):
                    parsed = parse_traceparent(headers[key])
                    if parsed is not None:
                        return parsed
            return None

        for item in headers:  # type: ignore[union-attr]
            if not item:
                continue
            key, value = item[0], item[1]
            name = _normalize(key)
            if name is not None and name.lower() in ("traceparent", "http_traceparent"):
                parsed = parse_traceparent(value)
                if parsed is not None:
                    return parsed
    except Exception:
        return None

    return None
