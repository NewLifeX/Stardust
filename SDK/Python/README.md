# 星尘监控 Python SDK

适用于 Python 3.7+，提供星尘 APM 监控和配置中心的接入能力。

## 功能特性

- **APM 监控**：自动追踪应用性能，记录调用链、错误、性能指标
- **自动埋点**：默认开启，自动采集 Web 入口、出站 HTTP、数据库与 Redis 调用
- **链路上下文**：遵循 W3C `traceparent`，可与 .NET 及其它语言 SDK 互通
- **配置中心**：从星尘配置中心拉取配置，支持动态刷新
- **简单易用**：几行代码即可接入，支持装饰器和上下文管理器
- **Web 框架集成**：自动识别 Flask/Django/Starlette，另提供 WSGI/ASGI 中间件

## 安装

```bash
pip install newlifex-stardust
```

或直接从源码安装：

```bash
cd SDK/Python
pip install -e .
```

## 快速开始

### 一行接入（推荐）

```python
from stardust_sdk import init

client = init("http://star.example.com:6600", "MyApp", "MySecret")
# 自动启用 APM、自动埋点和配置中心
```

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

## 自动埋点（默认开启）

创建 `StardustClient` / `StardustTracer` 时自动安装，无需修改业务代码。

### 支持的框架与库

| 类型 | 自动识别/支持 | Span 名称 | 说明 |
|------|---------------|-----------|------|
| 出站 HTTP | `requests`、`urllib` | `METHOD host/path` | Tag 为完整 URL，4xx/5xx 与网络异常记为错误 |
| Web 入口 | Flask（`Flask.wsgi_app`）、Django（`BaseHandler.get_response`）、Starlette（`Starlette.__call__`） | `METHOD path` | 读取入站 `traceparent`，5xx/异常记为错误 |
| Web 入口（显式） | `StardustWSGIMiddleware`、`StardustASGIMiddleware` | `METHOD path` | 框架在安装之后才导入时使用 |
| 数据库 | DB-API：`sqlite3`、`pymysql`、`psycopg2` | `db:{system}:{action}:{tables}` | Tag 为 SQL 文本，自动识别表名并去除日期分表后缀 |
| Redis | `redis.Redis.execute_command`（同步客户端） | `redis:{COMMAND}` | Tag 为参数摘要 |

### 链路上下文

入站请求读取标准 W3C 头，复用上游 TraceId 并将上游 SpanId 作为父 Span：

```text
traceparent: 00-<32位十六进制trace-id>-<16位十六进制span-id>-01
```

- 兼容 .NET 历史实现：入站 TraceId 可以短于 32 位，出站统一生成/输出 32 位。
- 出站请求若已显式设置 `traceparent`，SDK 不覆盖。
- 星尘服务端自身接口（`/App/`、`/Trace/`）不会创建 Span，也不会注入 `traceparent`。

### 关闭或重新启用

```python
# 创建时关闭
client = StardustClient(..., auto_instrument=False)

# 运行时卸载 / 重新安装
client.uninstrument()
client.instrument()

# 使用 StardustTracer 时
tracer = StardustTracer(..., auto_instrument=False)
tracer.instrument()
```

### 显式 Web 中间件

若框架是在 SDK 安装之后才导入（自动识别不会生效），使用中间件接入：

```python
from stardust_sdk import StardustWSGIMiddleware, StardustASGIMiddleware

# WSGI（Flask 实例本身就是 WSGI 应用）
app.wsgi_app = StardustWSGIMiddleware(app.wsgi_app)

# ASGI（Starlette/FastAPI）
app = StardustASGIMiddleware(app)
```

同一请求只会产生一个入口 Span：显式中间件与框架自动埋点通过请求标记去重。

### 限制

- 自动识别只针对**安装时已导入**的 Web 框架；之后导入的框架请使用显式中间件。
- 自动埋点在构造时执行一次；之后才安装/导入的库需重新调用 `instrument()`。
- 已通过 `from ... import connect/urlopen` 提前绑定的函数引用不会被替换。
- Redis 仅覆盖同步客户端 `redis.Redis`，异步客户端需手动埋点。
- 数据库仅覆盖 DB-API 驱动层，不理解 ORM 语义；SQL 表名为启发式解析。
- WSGI 的 Span 在应用返回迭代器时结束，流式响应的实际传输时间不计入耗时。
- 上下文基于 `contextvars`，新建线程不会自动继承当前 Span。
- SDK 不做头部采样，出站统一写 `flags=01`，最终采样由星尘服务端决定。

## Web 框架集成

> 已启用自动埋点（默认）时，Flask/Django/Starlette 无需再写下面的手动中间件；示例保留用于自定义需求，或在 `auto_instrument=False` 时手工接入。

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
    auto_instrument=True,  # 是否启用自动埋点（默认开启）
)
```

**APM 方法**：
- `new_span(name, parent_id="", trace_id=None)` - 创建追踪片段
- `trace(name)` - 追踪装饰器
- `instrument()` / `uninstrument()` - 安装/卸载自动埋点
- `current_span` - 当前活动 Span
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
- `start()` - 启动追踪器（登录与后台上报）
- `stop()` - 停止追踪器
- `new_span(name, parent_id="", trace_id=None)` - 创建追踪片段
- `trace(name)` - 装饰器
- `instrument()` / `uninstrument()` - 安装/卸载自动埋点

**属性**：
- `current_span` - 当前活动 Span
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
