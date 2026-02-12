"""星尘监控追踪器

提供 APM 监控的接入能力。
"""

import gzip
import json
import os
import socket
import threading
import time
import uuid
from contextlib import contextmanager
from typing import Any, Callable, Dict, List, Optional

import requests


class Span:
    """追踪片段"""

    def __init__(self, name: str, tracer: "StardustTracer", parent_id: str = ""):
        self.id = uuid.uuid4().hex[:16]
        self.parent_id = parent_id
        self.trace_id = tracer._current_trace_id() or uuid.uuid4().hex
        self.name = name
        self.start_time = int(time.time() * 1000)
        self.end_time = 0
        self.tag = ""
        self.error = ""
        self._tracer = tracer

    def set_error(self, ex: Exception) -> None:
        """设置错误信息"""
        self.error = str(ex)

    def finish(self) -> None:
        """结束片段"""
        self.end_time = int(time.time() * 1000)
        self._tracer._finish_span(self)

    def __enter__(self) -> "Span":
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
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

        # Trace 上下文（线程局部存储）
        self._local = threading.local()

    def start(self) -> None:
        """启动追踪器"""
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

    def new_span(self, name: str, parent_id: str = "") -> Span:
        """创建新的追踪片段"""
        return Span(name, self, parent_id)

    def trace(self, name: str) -> Callable:
        """装饰器，自动追踪函数调用"""

        def decorator(func: Callable) -> Callable:
            def wrapper(*args, **kwargs):
                with self.new_span(name):
                    return func(*args, **kwargs)

            return wrapper

        return decorator

    def _current_trace_id(self) -> Optional[str]:
        return getattr(self._local, "trace_id", None)

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
