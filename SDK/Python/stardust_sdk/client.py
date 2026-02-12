"""星尘统一客户端

同时提供 APM 监控和配置中心功能。
"""

import os
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
        """
        初始化星尘客户端

        Args:
            server: 星尘服务器地址，如 http://star.example.com:6600
            app_id: 应用标识
            secret: 应用密钥
            client_id: 客户端标识，默认为 IP@PID
            scope: 配置作用域，如 dev/test/prod
            enable_trace: 是否启用 APM 监控
            enable_config: 是否启用配置中心
        """
        self.server = server
        self.app_id = app_id
        self.secret = secret
        self.client_id = client_id
        self.scope = scope

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

    # APM 监控相关方法
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

    @property
    def tracer(self) -> Optional[StardustTracer]:
        """获取追踪器实例"""
        return self._tracer

    # 配置中心相关方法
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
    def config(self) -> Optional[ConfigClient]:
        """获取配置客户端实例"""
        return self._config

    @property
    def config_version(self) -> int:
        """获取当前配置版本"""
        if not self._config:
            return 0
        return self._config.version
