# 星尘监控 Python SDK

适用于 Python 3.7+，提供星尘 APM 监控和配置中心的接入能力。

## 功能特性

- **APM 监控**：自动追踪应用性能，记录调用链、错误、性能指标
- **配置中心**：从星尘配置中心拉取配置，支持动态刷新和变更通知
- **简单易用**：几行代码即可接入，支持装饰器和上下文管理器
- **Web 框架集成**：提供 Django、Flask 等框架的集成示例

## 安装

**方式一：直接安装 SDK 包**

```bash
# 从源码安装
cd SDK/Python
pip install -e .
```

**方式二：仅安装依赖**

```bash
pip install requests
```

## 快速开始

### 1. 统一客户端（推荐）

同时使用 APM 监控和配置中心：

```python
from stardust_sdk import StardustClient

# 创建客户端（同时启用 APM 和配置中心）
client = StardustClient(
    server="http://star.example.com:6600",
    app_id="MyApp",
    secret="MySecret",
    scope="prod"  # 配置作用域：dev/test/prod
)

# 启动客户端
client.start()

# 使用 APM 监控
with client.new_span("业务操作") as span:
    span.tag = "参数信息"
    do_something()

# 使用配置中心
db_host = client.get_config("database.host", "localhost")
db_port = client.get_config_int("database.port", 3306)
debug = client.get_config_bool("debug", False)

# 监听配置变更
def on_config_changed(configs):
    print(f"配置已更新: {configs}")

client.on_config_change(on_config_changed)

# 程序退出时
client.stop()
```

### 2. 仅使用 APM 监控

```python
from stardust_sdk import StardustTracer

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

### 3. 仅使用配置中心

```python
from stardust_sdk import ConfigClient

config = ConfigClient(
    server="http://star.example.com:6600",
    app_id="MyApp",
    secret="MySecret",
    scope="prod"
)

# 启动配置客户端
config.start()

# 获取配置
api_url = config.get("api.url", "http://localhost:8080")
timeout = config.get_int("api.timeout", 30)
enabled = config.get_bool("feature.enabled", False)

# 监听配置变更
config.on_change(lambda cfg: print(f"配置更新: {cfg}"))

# 程序退出时
config.stop()
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

## 配置中心客户端代码

```python
"""星尘配置中心客户端"""

import json
import threading
import time
from typing import Any, Callable, Dict, Optional

import requests


class ConfigClient:
    """星尘配置中心客户端"""

    def __init__(
        self,
        server: str,
        app_id: str,
        secret: str = "",
        client_id: Optional[str] = None,
        scope: str = "",
    ):
        self.server = server.rstrip("/")
        self.app_id = app_id
        self.secret = secret
        self.client_id = client_id or ""
        self.scope = scope

        # 令牌
        self._token = ""
        self._token_expire = 0

        # 配置信息
        self._configs: Dict[str, str] = {}
        self._version = 0
        self._configs_lock = threading.Lock()

        # 刷新间隔（秒）
        self.refresh_interval = 60

        # 变更回调
        self._change_callbacks: list[Callable[[Dict[str, str]], None]] = []

        # 线程控制
        self._running = False
        self._refresh_thread: Optional[threading.Thread] = None

    def start(self) -> None:
        """启动配置客户端"""
        self._login()
        self._load_configs()
        self._running = True

        self._refresh_thread = threading.Thread(
            target=self._refresh_loop, daemon=True
        )
        self._refresh_thread.start()

    def stop(self) -> None:
        """停止配置客户端"""
        self._running = False

    def get(self, key: str, default: str = "") -> str:
        """获取配置项"""
        with self._configs_lock:
            return self._configs.get(key, default)

    def get_int(self, key: str, default: int = 0) -> int:
        """获取整型配置"""
        value = self.get(key)
        if not value:
            return default
        try:
            return int(value)
        except ValueError:
            return default

    def get_bool(self, key: str, default: bool = False) -> bool:
        """获取布尔配置"""
        value = self.get(key, "").lower()
        if value in ("true", "1", "yes", "on"):
            return True
        elif value in ("false", "0", "no", "off"):
            return False
        return default

    def get_all(self) -> Dict[str, str]:
        """获取所有配置"""
        with self._configs_lock:
            return self._configs.copy()

    def on_change(self, callback: Callable[[Dict[str, str]], None]) -> None:
        """注册配置变更回调"""
        self._change_callbacks.append(callback)

    def _login(self) -> None:
        """登录获取令牌"""
        url = f"{self.server}/App/Login"
        payload = {
            "AppId": self.app_id,
            "Secret": self.secret,
            "ClientId": self.client_id,
            "AppName": self.app_id,
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
            print(f"[Stardust] Config login failed: {ex}")

    def _load_configs(self) -> None:
        """加载配置"""
        url = f"{self.server}/Config/GetAll?Token={self._token}"
        payload = {
            "AppId": self.app_id,
            "Secret": self.secret,
            "ClientId": self.client_id,
            "Scope": self.scope,
            "Version": self._version,
        }
        try:
            resp = requests.post(url, json=payload, timeout=10)
            data = resp.json()
            if data.get("code") == 0 and data.get("data"):
                result = data["data"]
                new_version = result.get("Version", 0)
                new_configs = result.get("Configs", {})

                # 版本有变化，更新配置
                if new_version > self._version and new_configs:
                    with self._configs_lock:
                        old_configs = self._configs.copy()
                        self._configs = new_configs
                        self._version = new_version

                    # 触发变更回调
                    if old_configs != new_configs:
                        for callback in self._change_callbacks:
                            try:
                                callback(new_configs)
                            except Exception as ex:
                                print(f"[Stardust] Config change callback error: {ex}")

                    print(
                        f"[Stardust] Config updated to version {new_version}, "
                        f"keys: {list(new_configs.keys())}"
                    )
        except Exception as ex:
            print(f"[Stardust] Config load failed: {ex}")

    def _refresh_loop(self) -> None:
        """定时刷新配置"""
        while self._running:
            time.sleep(self.refresh_interval)
            try:
                self._load_configs()
            except Exception as ex:
                print(f"[Stardust] Config refresh error: {ex}")

    @property
    def version(self) -> int:
        """当前配置版本"""
        return self._version
```

## 统一客户端代码

```python
"""星尘统一客户端"""

from typing import Callable, Dict, Optional

from .config_client import ConfigClient
from .tracer import Span, StardustTracer


class StardustClient:
    """星尘统一客户端，集成 APM 监控和配置中心"""

    def __init__(
        self,
        server: str,
        app_id: str,
        secret: str = "",
        client_id: Optional[str] = None,
        scope: str = "",
        enable_trace: bool = True,
        enable_config: bool = True,
    ):
        self.server = server
        self.app_id = app_id
        self.secret = secret

        # APM 监控
        self._tracer: Optional[StardustTracer] = None
        if enable_trace:
            self._tracer = StardustTracer(
                server=server,
                app_id=app_id,
                secret=secret,
                client_id=client_id,
            )

        # 配置中心
        self._config: Optional[ConfigClient] = None
        if enable_config:
            self._config = ConfigClient(
                server=server,
                app_id=app_id,
                secret=secret,
                client_id=client_id,
                scope=scope,
            )

    def start(self) -> None:
        """启动客户端"""
        if self._tracer:
            self._tracer.start()
        if self._config:
            self._config.start()

    def stop(self) -> None:
        """停止客户端"""
        if self._tracer:
            self._tracer.stop()
        if self._config:
            self._config.stop()

    def new_span(self, name: str, parent_id: str = "") -> Span:
        """创建追踪片段"""
        if not self._tracer:
            raise RuntimeError("Tracer is not enabled")
        return self._tracer.new_span(name, parent_id)

    def trace(self, name: str) -> Callable:
        """追踪装饰器"""
        if not self._tracer:
            raise RuntimeError("Tracer is not enabled")
        return self._tracer.trace(name)

    def get_config(self, key: str, default: str = "") -> str:
        """获取配置项"""
        if not self._config:
            raise RuntimeError("Config client is not enabled")
        return self._config.get(key, default)

    def get_config_int(self, key: str, default: int = 0) -> int:
        """获取整型配置"""
        if not self._config:
            raise RuntimeError("Config client is not enabled")
        return self._config.get_int(key, default)

    def get_config_bool(self, key: str, default: bool = False) -> bool:
        """获取布尔配置"""
        if not self._config:
            raise RuntimeError("Config client is not enabled")
        return self._config.get_bool(key, default)

    def get_all_configs(self) -> Dict[str, str]:
        """获取所有配置"""
        if not self._config:
            raise RuntimeError("Config client is not enabled")
        return self._config.get_all()

    def on_config_change(self, callback: Callable[[Dict[str, str]], None]) -> None:
        """注册配置变更回调"""
        if not self._config:
            raise RuntimeError("Config client is not enabled")
        self._config.on_change(callback)

    @property
    def config_version(self) -> int:
        """获取当前配置版本"""
        if not self._config:
            return 0
        return self._config.version
```

## Django 中间件集成

```python
from stardust_sdk import StardustClient

# 全局初始化
client = StardustClient(
    "http://star.example.com:6600",
    "MyDjangoApp",
    "secret",
    scope="prod"
)
client.start()


class StardustMiddleware:
    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        name = f"{request.method} {request.path}"
        with client.new_span(name) as span:
            span.tag = request.get_full_path()
            try:
                response = self.get_response(request)
                if response.status_code >= 400:
                    span.set_error(Exception(f"HTTP {response.status_code}"))
                return response
            except Exception as ex:
                span.set_error(ex)
                raise


# settings.py 中使用配置
# from myapp.middleware import client
#
# DEBUG = client.get_config_bool("debug", False)
# SECRET_KEY = client.get_config("secret_key")
#
# DATABASES = {
#     'default': {
#         'HOST': client.get_config("database.host", "localhost"),
#         'PORT': client.get_config_int("database.port", 3306),
#     }
# }
```

## Flask 集成

```python
from flask import Flask, request, g
from stardust_sdk import StardustClient

app = Flask(__name__)

# 创建客户端
client = StardustClient(
    "http://star.example.com:6600",
    "MyFlaskApp",
    "secret",
    scope="prod"
)
client.start()


@app.before_request
def before_request():
    name = f"{request.method} {request.path}"
    span = client.new_span(name)
    span.tag = request.full_path
    g.stardust_span = span


@app.after_request
def after_request(response):
    span = g.pop("stardust_span", None)
    if span:
        if response.status_code >= 400:
            span.set_error(Exception(f"HTTP {response.status_code}"))
        span.finish()
    return response


# 使用配置
app.config["DEBUG"] = client.get_config_bool("debug", False)
app.config["SECRET_KEY"] = client.get_config("secret_key")


# 监听配置变更
def on_config_changed(configs):
    app.config["DEBUG"] = client.get_config_bool("debug", False)


client.on_config_change(on_config_changed)
```

## 完整示例项目

完整的示例代码请参考：`SDK/Python/examples/`

- `basic_example.py` - 基础使用示例（APM + 配置）
- `django_example.py` - Django 集成示例
- `flask_example.py` - Flask 集成示例

## 安装和使用

```bash
# 克隆仓库
git clone https://github.com/NewLifeX/Stardust.git
cd Stardust/SDK/Python

# 安装依赖
pip install -r requirements.txt

# 运行示例
python examples/basic_example.py
```

## API 参考

详细 API 文档请参考 SDK 源码和 README.md 文件。

## 相关链接

- [星尘监控文档](https://newlifex.com/blood/stardust_monitor)
- [GitHub 仓库](https://github.com/NewLifeX/Stardust)
- [在线演示](http://star.newlifex.com)
