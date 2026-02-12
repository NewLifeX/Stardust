"""星尘配置中心客户端

提供配置拉取和自动刷新功能。
"""

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
        self._next_version = 0
        self._next_publish = ""
        self._update_time = ""
        self._source_ip = ""
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
                        self._next_version = result.get("NextVersion", 0)
                        self._next_publish = result.get("NextPublish", "")
                        self._update_time = result.get("UpdateTime", "")
                        self._source_ip = result.get("SourceIP", "")

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
                elif new_version == self._version:
                    # 版本未变化，更新其他信息
                    self._next_version = result.get("NextVersion", 0)
                    self._next_publish = result.get("NextPublish", "")
                    self._source_ip = result.get("SourceIP", "")
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

    @property
    def next_version(self) -> int:
        """下一个配置版本"""
        return self._next_version

    @property
    def source_ip(self) -> str:
        """来源IP地址"""
        return self._source_ip
