# 星尘监控 Python SDK

适用于 Python 3.7+，提供星尘 APM 监控的接入能力。

## 安装依赖

```bash
pip install requests
```

## 快速开始

```python
from stardust_tracer import StardustTracer

tracer = StardustTracer(
    server="http://star.example.com:6600",
    app_id="MyApp",
    secret="MySecret"
)

# 启动追踪器（自动登录、心跳、定时上报）
tracer.start()

# 手动埋点
with tracer.new_span("业务操作A") as span:
    span.tag = "参数信息"
    do_something()

# 或使用装饰器
@tracer.trace("数据库查询")
def query_users():
    pass

# 程序退出时
tracer.stop()
```

## 完整代码

```python
"""星尘监控 Python SDK"""

import gzip
import json
import os
import socket
import threading
import time
import uuid
from contextlib import contextmanager

import requests


class Span:
    """追踪片段"""

    def __init__(self, name, tracer, parent_id=""):
        self.id = uuid.uuid4().hex[:16]
        self.parent_id = parent_id
        self.trace_id = tracer._current_trace_id() or uuid.uuid4().hex
        self.name = name
        self.start_time = int(time.time() * 1000)
        self.end_time = 0
        self.tag = ""
        self.error = ""
        self._tracer = tracer

    def set_error(self, ex):
        """设置错误信息"""
        self.error = str(ex)

    def finish(self):
        """结束片段"""
        self.end_time = int(time.time() * 1000)
        self._tracer._finish_span(self)

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        if exc_val:
            self.set_error(exc_val)
        self.finish()
        return False

    def to_dict(self):
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

    def __init__(self, name, max_samples=1, max_errors=10):
        self.name = name
        self.start_time = int(time.time() * 1000)
        self.end_time = 0
        self.total = 0
        self.errors = 0
        self.cost = 0
        self.max_cost = 0
        self.min_cost = 0
        self.samples = []
        self.error_samples = []
        self._max_samples = max_samples
        self._max_errors = max_errors
        self._lock = threading.Lock()

    def add_span(self, span):
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

    def to_dict(self):
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

    def __init__(self, server, app_id, secret="", client_id=None):
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
        self.excludes = []
        self.enable_meter = True

        # 构建器
        self._builders = {}
        self._builders_lock = threading.Lock()

        # 线程控制
        self._running = False
        self._report_thread = None
        self._ping_thread = None

        # Trace 上下文（线程局部存储）
        self._local = threading.local()

    def start(self):
        """启动追踪器"""
        self._login()
        self._running = True

        self._report_thread = threading.Thread(target=self._report_loop, daemon=True)
        self._report_thread.start()

        self._ping_thread = threading.Thread(target=self._ping_loop, daemon=True)
        self._ping_thread.start()

    def stop(self):
        """停止追踪器"""
        self._running = False
        self._flush()

    def new_span(self, name, parent_id=""):
        """创建新的追踪片段"""
        return Span(name, self, parent_id)

    def trace(self, name):
        """装饰器，自动追踪函数调用"""
        def decorator(func):
            def wrapper(*args, **kwargs):
                with self.new_span(name):
                    return func(*args, **kwargs)
            return wrapper
        return decorator

    def _current_trace_id(self):
        return getattr(self._local, "trace_id", None)

    def _finish_span(self, span):
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
            span.tag = span.tag[:self.max_tag_length]

        with self._builders_lock:
            builder = self._builders.get(span.name)
            if builder is None:
                builder = SpanBuilder(span.name, self.max_samples, self.max_errors)
                self._builders[span.name] = builder
            builder.add_span(span)

    def _login(self):
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

    def _ping(self):
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

    def _report(self, builders_data):
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

    def _apply_response(self, result):
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

    def _flush(self):
        """刷新当前周期的数据"""
        with self._builders_lock:
            if not self._builders:
                return
            builders_data = [b.to_dict() for b in self._builders.values() if b.total > 0]
            self._builders.clear()

        if builders_data:
            self._report(builders_data)

    def _report_loop(self):
        """定时上报线程"""
        while self._running:
            time.sleep(self.period)
            try:
                self._flush()
            except Exception as ex:
                print(f"[Stardust] Report loop error: {ex}")

    def _ping_loop(self):
        """心跳线程"""
        while self._running:
            time.sleep(30)
            try:
                # 令牌过期则重新登录
                if time.time() > self._token_expire - 600:
                    self._ping()
                else:
                    self._ping()
            except Exception as ex:
                print(f"[Stardust] Ping loop error: {ex}")

    @staticmethod
    def _get_local_ip():
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]
            s.close()
            return ip
        except Exception:
            return "127.0.0.1"
```

## Django 中间件集成

```python
import time
from stardust_tracer import StardustTracer

# 全局初始化
tracer = StardustTracer("http://star.example.com:6600", "MyDjangoApp", "secret")
tracer.start()


class StardustMiddleware:
    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        name = f"{request.method} {request.path}"
        with tracer.new_span(name) as span:
            span.tag = request.get_full_path()
            try:
                response = self.get_response(request)
                if response.status_code >= 400:
                    span.set_error(f"HTTP {response.status_code}")
                return response
            except Exception as ex:
                span.set_error(ex)
                raise
```

## Flask 集成

```python
from flask import Flask, request, g
from stardust_tracer import StardustTracer

app = Flask(__name__)
tracer = StardustTracer("http://star.example.com:6600", "MyFlaskApp", "secret")
tracer.start()


@app.before_request
def before_request():
    name = f"{request.method} {request.path}"
    span = tracer.new_span(name)
    span.tag = request.full_path
    g.stardust_span = span


@app.after_request
def after_request(response):
    span = g.pop("stardust_span", None)
    if span:
        if response.status_code >= 400:
            span.set_error(f"HTTP {response.status_code}")
        span.finish()
    return response
```
