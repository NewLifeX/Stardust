"""星尘监控追踪器

提供 APM 监控的接入能力。
"""

import gzip
import json
import os
import socket
import threading
import time
from typing import Any, Callable, Dict, List, Optional

import requests

from .context import current_span as _current_context_span
from .context import pop_span, push_span
from .trace_context import new_span_id, new_trace_id


class Span:
    """追踪片段

    作为上下文管理器使用时，会自动压入/恢复当前追踪上下文，
    使其间创建的 Span 自动成为子 Span。
    """

    def __init__(
        self,
        name: str,
        tracer: "StardustTracer",
        parent_id: str = "",
        trace_id: Optional[str] = None,
    ):
        active = _current_context_span()
        self.id = new_span_id()
        self.parent_id = parent_id or (active.id if active is not None else "")
        self.trace_id = (
            trace_id
            or (active.trace_id if active is not None else "")
            or tracer._current_trace_id()
            or new_trace_id()
        )
        self.name = name
        self.start_time = int(time.time() * 1000)
        self.end_time = 0
        self.tag = ""
        self.error = ""
        self._tracer = tracer
        self._token = None
        self._finished = False

    def set_error(self, ex: Exception) -> None:
        """设置错误信息"""
        self.error = str(ex)

    def finish(self) -> None:
        """结束片段（幂等，重复调用不会重复统计）"""
        if self._finished:
            return
        self._finished = True
        self.end_time = int(time.time() * 1000)
        try:
            self._tracer._finish_span(self)
        except Exception:
            # 监控自身异常不得影响业务
            pass

    def __enter__(self) -> "Span":
        self._token = push_span(self)
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        token = self._token
        self._token = None
        if token is not None:
            try:
                pop_span(token)
            except (ValueError, RuntimeError):
                pass
        if exc_val:
            self.set_error(exc_val)
        self.finish()
        return False

    def to_dict(self) -> Dict[str, Any]:
        return {
            "Id": self.id,
            "ParentId": self.parent_id,
            "TraceId": self.trace_id,
            "StartTime": self.start_time,
            "EndTime": self.end_time,
            "Tag": self.tag,
            "Error": self.error,
        }


class SpanBuilder:
    """追踪构建器，按操作名聚合统计"""

    def __init__(self, name: str, max_samples: int = 1, max_errors: int = 10):
        self.name = name
        self.start_time = int(time.time() * 1000)
        self.end_time = 0
        self.total = 0
        self.errors = 0
        self.cost = 0
        self.max_cost = 0
        self.min_cost = 0
        self.samples: List[Span] = []
        self.error_samples: List[Span] = []
        self._max_samples = max_samples
        self._max_errors = max_errors
        self._lock = threading.Lock()

    def add_span(self, span: Span) -> None:
        """添加一个完成的 Span"""
        elapsed = span.end_time - span.start_time

        with self._lock:
            self.total += 1
            self.cost += elapsed
            if self.max_cost == 0 or elapsed > self.max_cost:
                self.max_cost = elapsed
            if self.min_cost == 0 or elapsed < self.min_cost:
                self.min_cost = elapsed

            if span.error:
                self.errors += 1
                if len(self.error_samples) < self._max_errors:
                    self.error_samples.append(span)
            else:
                if len(self.samples) < self._max_samples:
                    self.samples.append(span)

            self.end_time = int(time.time() * 1000)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "Name": self.name,
            "StartTime": self.start_time,
            "EndTime": self.end_time,
            "Total": self.total,
            "Errors": self.errors,
            "Cost": self.cost,
            "MaxCost": self.max_cost,
            "MinCost": self.min_cost,
            "Samples": [s.to_dict() for s in self.samples],
            "ErrorSamples": [s.to_dict() for s in self.error_samples],
        }


class StardustTracer:
    """星尘监控追踪器"""

    def __init__(
        self,
        server: str,
        app_id: str,
        secret: str = "",
        client_id: Optional[str] = None,
        auto_instrument: bool = True,
    ):
        self.server = server.rstrip("/")
        self.app_id = app_id
        self.app_name = app_id
        self.secret = secret
        self.client_id = client_id or f"{self._get_local_ip()}@{os.getpid()}"

        # 令牌
        self._token = ""
        self._token_expire = 0

        # 采样参数（从服务端动态获取）
        self.period = 60
        self.max_samples = 1
        self.max_errors = 10
        self.timeout = 5000
        self.max_tag_length = 1024
        self.excludes: List[str] = []
        self.enable_meter = True

        # 构建器
        self._builders: Dict[str, SpanBuilder] = {}
        self._builders_lock = threading.Lock()

        # 线程控制
        self._running = False
        self._report_thread: Optional[threading.Thread] = None
        self._ping_thread: Optional[threading.Thread] = None

        # 自动埋点
        self._instrumented = False
        if auto_instrument:
            self.instrument()

    def start(self) -> None:
        """启动追踪器（登录与后台上报；自动埋点在构造时已完成）"""
        self._login()
        self._running = True

        self._report_thread = threading.Thread(target=self._report_loop, daemon=True)
        self._report_thread.start()

        self._ping_thread = threading.Thread(target=self._ping_loop, daemon=True)
        self._ping_thread.start()

    def stop(self) -> None:
        """停止追踪器"""
        self._running = False
        self._flush()

    def instrument(self) -> None:
        """安装自动埋点（requests/urllib、Web 框架、DB-API、Redis）"""
        try:
            from .instrument import install

            install(self)
            self._instrumented = True
        except Exception as ex:
            print(f"[Stardust] Instrument failed: {ex}")

    def uninstrument(self) -> None:
        """卸载自动埋点，恢复原始实现"""
        try:
            from .instrument import uninstall

            uninstall()
        except Exception as ex:
            print(f"[Stardust] Uninstrument failed: {ex}")
        self._instrumented = False

    @property
    def current_span(self) -> Optional[Span]:
        """获取当前上下文中的活动 Span"""
        return _current_context_span()

    def new_span(
        self,
        name: str,
        parent_id: str = "",
        trace_id: Optional[str] = None,
    ) -> Span:
        """创建新的追踪片段

        Args:
            name: 操作名
            parent_id: 父 SpanId，为空时自动使用当前活动 Span
            trace_id: TraceId，为空时自动使用当前链路或新建

        Returns:
            新建的 Span
        """
        return Span(name, self, parent_id, trace_id)

    def trace(self, name: str) -> Callable:
        """装饰器，自动追踪函数调用"""

        def decorator(func: Callable) -> Callable:
            def wrapper(*args, **kwargs):
                with self.new_span(name):
                    return func(*args, **kwargs)

            return wrapper

        return decorator

    def _current_trace_id(self) -> Optional[str]:
        span = _current_context_span()
        return span.trace_id if span is not None else None

    def _finish_span(self, span: Span) -> None:
        """完成一个 Span，加入对应的 Builder"""
        # 排除自身调用
        if span.name in ("/Trace/Report", "/Trace/ReportRaw"):
            return
        # 排除服务端指定的操作
        for exc in self.excludes:
            if exc and exc.lower() in span.name.lower():
                return

        # 截断 Tag
        if span.tag and len(span.tag) > self.max_tag_length:
            span.tag = span.tag[: self.max_tag_length]

        with self._builders_lock:
            builder = self._builders.get(span.name)
            if builder is None:
                builder = SpanBuilder(span.name, self.max_samples, self.max_errors)
                self._builders[span.name] = builder
            builder.add_span(span)

    def _login(self) -> None:
        """登录获取令牌"""
        url = f"{self.server}/App/Login"
        payload = {
            "AppId": self.app_id,
            "Secret": self.secret,
            "ClientId": self.client_id,
            "AppName": self.app_name,
        }
        try:
            resp = requests.post(url, json=payload, timeout=10)
            data = resp.json()
            if data.get("code") == 0 and data.get("data"):
                result = data["data"]
                self._token = result.get("Token", "")
                expire = result.get("Expire", 7200)
                self._token_expire = time.time() + expire
                if result.get("Code"):
                    self.app_id = result["Code"]
                if result.get("Secret"):
                    self.secret = result["Secret"]
        except Exception as ex:
            print(f"[Stardust] Login failed: {ex}")

    def _ping(self) -> None:
        """心跳保活"""
        url = f"{self.server}/App/Ping?Token={self._token}"
        payload = {
            "Id": os.getpid(),
            "Name": self.app_name,
            "Time": int(time.time() * 1000),
        }
        try:
            resp = requests.post(url, json=payload, timeout=10)
            data = resp.json()
            if data.get("code") == 0 and data.get("data"):
                result = data["data"]
                # 刷新令牌
                new_token = result.get("Token")
                if new_token:
                    self._token = new_token
        except Exception as ex:
            print(f"[Stardust] Ping failed: {ex}")

    def _report(self, builders_data: List[Dict[str, Any]]) -> None:
        """上报监控数据"""
        payload = {
            "AppId": self.app_id,
            "AppName": self.app_name,
            "ClientId": self.client_id,
            "Builders": builders_data,
        }
        body = json.dumps(payload, ensure_ascii=False)

        try:
            if len(body) > 1024:
                # 压缩上传
                url = f"{self.server}/Trace/ReportRaw?Token={self._token}"
                compressed = gzip.compress(body.encode("utf-8"))
                resp = requests.post(
                    url,
                    data=compressed,
                    headers={"Content-Type": "application/x-gzip"},
                    timeout=10,
                )
            else:
                url = f"{self.server}/Trace/Report?Token={self._token}"
                resp = requests.post(url, json=payload, timeout=10)

            data = resp.json()
            if data.get("code") == 0 and data.get("data"):
                self._apply_response(data["data"])
        except Exception as ex:
            print(f"[Stardust] Report failed: {ex}")

    def _apply_response(self, result: Dict[str, Any]) -> None:
        """应用服务端返回的采样参数"""
        if result.get("Period", 0) > 0:
            self.period = result["Period"]
        if result.get("MaxSamples", 0) > 0:
            self.max_samples = result["MaxSamples"]
        if result.get("MaxErrors", 0) > 0:
            self.max_errors = result["MaxErrors"]
        if result.get("Timeout", 0) > 0:
            self.timeout = result["Timeout"]
        if result.get("MaxTagLength", 0) > 0:
            self.max_tag_length = result["MaxTagLength"]
        if result.get("Excludes"):
            self.excludes = result["Excludes"]

    def _flush(self) -> None:
        """刷新当前周期的数据"""
        with self._builders_lock:
            if not self._builders:
                return
            builders_data = [
                b.to_dict() for b in self._builders.values() if b.total > 0
            ]
            self._builders.clear()

        if builders_data:
            self._report(builders_data)

    def _report_loop(self) -> None:
        """定时上报线程"""
        while self._running:
            time.sleep(self.period)
            try:
                self._flush()
            except Exception as ex:
                print(f"[Stardust] Report loop error: {ex}")

    def _ping_loop(self) -> None:
        """心跳线程"""
        while self._running:
            time.sleep(30)
            try:
                self._ping()
            except Exception as ex:
                print(f"[Stardust] Ping loop error: {ex}")

    @staticmethod
    def _get_local_ip() -> str:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]
            s.close()
            return ip
        except Exception:
            return "127.0.0.1"
