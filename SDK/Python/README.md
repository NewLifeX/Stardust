# 星尘监控 Python SDK

适用于 Python 3.7+，提供星尘 APM 监控和配置中心的接入能力。

## 功能特性

- **APM 监控**：自动追踪应用性能，记录调用链、错误、性能指标
- **配置中心**：从星尘配置中心拉取配置，支持动态刷新
- **简单易用**：几行代码即可接入，支持装饰器和上下文管理器
- **Web 框架集成**：提供 Django、Flask 等框架的集成示例

## 安装

```bash
pip install stardust-sdk
```

或直接从源码安装：

```bash
cd SDK/Python
pip install -e .
```

## 快速开始

### 1. 基础使用（APM + 配置）

```python
from stardust_sdk import StardustClient

# 创建客户端（同时启用 APM 和配置中心）
client = StardustClient(
    server="http://star.example.com:6600",
    app_id="MyApp",
    secret="MySecret",
    scope="dev"  # 配置作用域：dev/test/prod
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
debug_mode = client.get_config_bool("debug", False)

# 监听配置变更
def on_config_changed(configs):
    print(f"配置已更新: {configs}")

client.on_config_change(on_config_changed)

# 程序退出时停止
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

tracer.start()

# 手动埋点
with tracer.new_span("业务操作A") as span:
    span.tag = "参数信息"
    do_something()

# 使用装饰器
@tracer.trace("数据库查询")
def query_users():
    pass

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

config.start()

# 获取配置
api_url = config.get("api.url", "http://localhost:8080")
timeout = config.get_int("api.timeout", 30)

# 监听配置变更
config.on_change(lambda cfg: print(f"配置更新: {cfg}"))

config.stop()
```

## Web 框架集成

### Django 中间件

```python
# middleware.py
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
                    span.set_error(f"HTTP {response.status_code}")
                return response
            except Exception as ex:
                span.set_error(ex)
                raise


# settings.py
MIDDLEWARE = [
    'myapp.middleware.StardustMiddleware',
    # ... 其他中间件
]

# 使用配置
from myapp.middleware import client

DEBUG = client.get_config_bool("debug", False)
DATABASE_HOST = client.get_config("database.host", "localhost")
```

### Flask 集成

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
            span.set_error(f"HTTP {response.status_code}")
        span.finish()
    return response


# 使用配置
app.config["DEBUG"] = client.get_config_bool("debug", False)
app.config["SECRET_KEY"] = client.get_config("secret_key")


if __name__ == "__main__":
    app.run()
```

## API 参考

### StardustClient

统一客户端，同时提供 APM 和配置功能。

```python
client = StardustClient(
    server="http://star.example.com:6600",
    app_id="MyApp",
    secret="MySecret",
    client_id=None,  # 可选，默认为 IP@PID
    scope="",        # 配置作用域
    enable_trace=True,   # 是否启用 APM
    enable_config=True,  # 是否启用配置
)
```

**APM 方法**：
- `new_span(name, parent_id="")` - 创建追踪片段
- `trace(name)` - 追踪装饰器
- `tracer` - 获取追踪器实例

**配置方法**：
- `get_config(key, default="")` - 获取字符串配置
- `get_config_int(key, default=0)` - 获取整型配置
- `get_config_bool(key, default=False)` - 获取布尔配置
- `get_all_configs()` - 获取所有配置
- `on_config_change(callback)` - 监听配置变更
- `config` - 获取配置客户端实例
- `config_version` - 获取当前配置版本

### StardustTracer

APM 监控追踪器。

**方法**：
- `start()` - 启动追踪器
- `stop()` - 停止追踪器
- `new_span(name, parent_id="")` - 创建追踪片段
- `trace(name)` - 装饰器

**属性**：
- `period` - 上报周期（秒）
- `max_samples` - 最大采样数
- `max_errors` - 最大错误采样数

### ConfigClient

配置中心客户端。

**方法**：
- `start()` - 启动客户端
- `stop()` - 停止客户端
- `get(key, default="")` - 获取配置
- `get_int(key, default=0)` - 获取整型配置
- `get_bool(key, default=False)` - 获取布尔配置
- `get_all()` - 获取所有配置
- `on_change(callback)` - 注册变更回调

**属性**：
- `version` - 当前配置版本
- `next_version` - 下一个版本
- `refresh_interval` - 刷新间隔（秒）

## 许可证

MIT License

## 相关链接

- [星尘监控文档](https://newlifex.com/blood/stardust_monitor)
- [GitHub 仓库](https://github.com/NewLifeX/Stardust)
- [在线演示](http://star.newlifex.com)
