"""Django 集成示例

在 Django 项目中集成星尘监控和配置中心。
"""

# middleware.py
# 在你的 Django 项目中创建 middleware.py 文件

from stardust_sdk import StardustClient

# 全局初始化（在应用启动时只初始化一次）
stardust_client = StardustClient(
    server="http://localhost:6600",
    app_id="MyDjangoApp",
    secret="",  # 首次连接可以为空
    scope="prod",
)
stardust_client.start()


class StardustMiddleware:
    """星尘监控中间件"""

    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        # 创建追踪片段
        name = f"{request.method} {request.path}"
        with stardust_client.new_span(name) as span:
            span.tag = request.get_full_path()

            try:
                response = self.get_response(request)

                # 记录 HTTP 错误
                if response.status_code >= 400:
                    span.set_error(Exception(f"HTTP {response.status_code}"))

                return response
            except Exception as ex:
                span.set_error(ex)
                raise


# settings.py
# 在 Django settings.py 中添加中间件

MIDDLEWARE = [
    "django.middleware.security.SecurityMiddleware",
    "django.contrib.sessions.middleware.SessionMiddleware",
    "myapp.middleware.StardustMiddleware",  # 添加星尘中间件
    # ... 其他中间件
]

# 从配置中心读取配置
# from myapp.middleware import stardust_client
#
# DEBUG = stardust_client.get_config_bool("debug", False)
#
# DATABASES = {
#     'default': {
#         'ENGINE': 'django.db.backends.mysql',
#         'NAME': stardust_client.get_config("database.name", "mydb"),
#         'USER': stardust_client.get_config("database.user", "root"),
#         'PASSWORD': stardust_client.get_config("database.password", ""),
#         'HOST': stardust_client.get_config("database.host", "localhost"),
#         'PORT': stardust_client.get_config_int("database.port", 3306),
#     }
# }


# views.py
# 在视图中使用配置和手动追踪

# from myapp.middleware import stardust_client
#
# def my_view(request):
#     # 读取配置
#     api_url = stardust_client.get_config("api.url", "http://localhost:8080")
#
#     # 手动追踪
#     with stardust_client.new_span("调用外部API") as span:
#         span.tag = f"URL: {api_url}"
#         # 调用 API
#         pass
#
#     return JsonResponse({"status": "ok"})


# 监听配置变更（可选）
def on_config_changed(configs):
    """配置变更时的回调"""
    print(f"配置已更新: {configs.keys()}")
    # 这里可以重新加载某些配置


stardust_client.on_config_change(on_config_changed)
