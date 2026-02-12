"""Flask 集成示例

在 Flask 应用中集成星尘监控和配置中心。
"""

from flask import Flask, request, g, jsonify
from stardust_sdk import StardustClient
import time

# 创建 Flask 应用
app = Flask(__name__)

# 创建星尘客户端
stardust_client = StardustClient(
    server="http://localhost:6600",
    app_id="MyFlaskApp",
    secret="",
    scope="prod",
)
stardust_client.start()


# 请求前钩子 - 开始追踪
@app.before_request
def before_request():
    name = f"{request.method} {request.path}"
    span = stardust_client.new_span(name)
    span.tag = request.full_path
    g.stardust_span = span


# 请求后钩子 - 结束追踪
@app.after_request
def after_request(response):
    span = g.pop("stardust_span", None)
    if span:
        if response.status_code >= 400:
            span.set_error(Exception(f"HTTP {response.status_code}"))
        span.finish()
    return response


# 错误处理
@app.errorhandler(Exception)
def handle_exception(e):
    span = g.pop("stardust_span", None)
    if span:
        span.set_error(e)
        span.finish()
    return jsonify({"error": str(e)}), 500


# 从配置中心加载配置
app.config["DEBUG"] = stardust_client.get_config_bool("debug", False)
app.config["SECRET_KEY"] = stardust_client.get_config(
    "secret_key", "default-secret-key"
)


# 示例路由
@app.route("/")
def index():
    return jsonify(
        {
            "app": "Flask Demo",
            "config_version": stardust_client.config_version,
            "message": "Hello from Stardust!",
        }
    )


@app.route("/api/users")
def get_users():
    # 手动追踪数据库查询
    with stardust_client.new_span("查询用户列表") as span:
        span.tag = "从数据库查询所有用户"
        time.sleep(0.1)  # 模拟数据库查询
        users = [{"id": 1, "name": "User1"}, {"id": 2, "name": "User2"}]

    return jsonify(users)


@app.route("/api/config")
def get_config():
    """获取当前配置"""
    configs = stardust_client.get_all_configs()
    return jsonify(
        {
            "version": stardust_client.config_version,
            "configs": configs,
        }
    )


@app.route("/api/error")
def error_example():
    """错误示例"""
    raise ValueError("这是一个测试错误")


# 配置变更回调
def on_config_changed(configs):
    print(f"配置已更新，共 {len(configs)} 项")
    # 可以在这里重新加载需要动态更新的配置
    app.config["DEBUG"] = stardust_client.get_config_bool("debug", False)


stardust_client.on_config_change(on_config_changed)


if __name__ == "__main__":
    print("Flask 应用启动中...")
    print(f"调试模式: {app.config['DEBUG']}")
    print(f"配置版本: {stardust_client.config_version}")

    app.run(host="0.0.0.0", port=5000)
