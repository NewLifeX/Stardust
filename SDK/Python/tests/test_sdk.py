"""Python SDK 单元测试

测试 APM 监控和配置中心功能。
"""

import unittest
import time
from unittest.mock import Mock, patch, MagicMock
from stardust_sdk import StardustTracer, ConfigClient, StardustClient, Span


class TestSpan(unittest.TestCase):
    """测试 Span 类"""

    def setUp(self):
        self.tracer = Mock()
        self.tracer._current_trace_id.return_value = "test_trace_id"
        self.tracer._finish_span = Mock()

    def test_span_creation(self):
        """测试创建 Span"""
        span = Span("test_operation", self.tracer)
        self.assertEqual(span.name, "test_operation")
        self.assertIsNotNone(span.id)
        self.assertEqual(span.trace_id, "test_trace_id")
        self.assertEqual(span.tag, "")
        self.assertEqual(span.error, "")

    def test_span_context_manager(self):
        """测试 Span 作为上下文管理器"""
        with Span("test_op", self.tracer) as span:
            span.tag = "test_tag"
            time.sleep(0.001)  # 确保有时间差

        self.assertGreater(span.end_time, span.start_time)
        self.tracer._finish_span.assert_called_once()

    def test_span_error_handling(self):
        """测试 Span 错误处理"""
        try:
            with Span("test_op", self.tracer) as span:
                raise ValueError("test error")
        except ValueError:
            pass

        self.assertEqual(span.error, "test error")
        self.tracer._finish_span.assert_called_once()


class TestStardustTracer(unittest.TestCase):
    """测试 StardustTracer 类"""

    @patch("stardust_sdk.tracer.requests.post")
    def test_login(self, mock_post):
        """测试登录"""
        mock_response = Mock()
        mock_response.json.return_value = {
            "code": 0,
            "data": {
                "Token": "test_token",
                "Expire": 7200,
                "Code": "TestApp",
                "Secret": "test_secret",
            },
        }
        mock_post.return_value = mock_response

        tracer = StardustTracer(
            server="http://localhost:6600",
            app_id="TestApp",
            secret="test_secret",
        )
        tracer._login()

        self.assertEqual(tracer._token, "test_token")
        self.assertEqual(tracer.app_id, "TestApp")
        mock_post.assert_called_once()

    def test_span_builder(self):
        """测试 SpanBuilder"""
        from stardust_sdk.tracer import SpanBuilder

        builder = SpanBuilder("test_op", max_samples=2, max_errors=2)

        # 添加正常 span
        span1 = Mock()
        span1.start_time = 1000
        span1.end_time = 1100
        span1.error = ""
        builder.add_span(span1)

        self.assertEqual(builder.total, 1)
        self.assertEqual(builder.errors, 0)
        self.assertEqual(builder.cost, 100)

        # 添加错误 span
        span2 = Mock()
        span2.start_time = 2000
        span2.end_time = 2200
        span2.error = "error message"
        builder.add_span(span2)

        self.assertEqual(builder.total, 2)
        self.assertEqual(builder.errors, 1)
        self.assertEqual(builder.cost, 300)


class TestConfigClient(unittest.TestCase):
    """测试 ConfigClient 类"""

    @patch("stardust_sdk.config_client.requests.post")
    def test_login(self, mock_post):
        """测试配置客户端登录"""
        mock_response = Mock()
        mock_response.json.return_value = {
            "code": 0,
            "data": {
                "Token": "test_token",
                "Expire": 7200,
            },
        }
        mock_post.return_value = mock_response

        client = ConfigClient(
            server="http://localhost:6600",
            app_id="TestApp",
            secret="test_secret",
        )
        client._login()

        self.assertEqual(client._token, "test_token")

    @patch("stardust_sdk.config_client.requests.post")
    def test_load_configs(self, mock_post):
        """测试加载配置"""
        # Mock 登录
        mock_login_response = Mock()
        mock_login_response.json.return_value = {
            "code": 0,
            "data": {"Token": "test_token"},
        }

        # Mock 配置
        mock_config_response = Mock()
        mock_config_response.json.return_value = {
            "code": 0,
            "data": {
                "Version": 1,
                "Configs": {
                    "database.host": "localhost",
                    "database.port": "3306",
                    "debug": "true",
                },
            },
        }

        mock_post.side_effect = [mock_login_response, mock_config_response]

        client = ConfigClient(
            server="http://localhost:6600",
            app_id="TestApp",
            secret="test_secret",
        )
        client._login()
        client._load_configs()

        self.assertEqual(client._version, 1)
        self.assertEqual(client.get("database.host"), "localhost")
        self.assertEqual(client.get_int("database.port"), 3306)
        self.assertEqual(client.get_bool("debug"), True)

    def test_config_type_conversion(self):
        """测试配置类型转换"""
        client = ConfigClient(
            server="http://localhost:6600",
            app_id="TestApp",
        )

        # 手动设置配置
        with client._configs_lock:
            client._configs = {
                "int_value": "123",
                "bool_true": "true",
                "bool_false": "false",
                "bool_one": "1",
                "bool_zero": "0",
            }

        self.assertEqual(client.get_int("int_value"), 123)
        self.assertEqual(client.get_bool("bool_true"), True)
        self.assertEqual(client.get_bool("bool_false"), False)
        self.assertEqual(client.get_bool("bool_one"), True)
        self.assertEqual(client.get_bool("bool_zero"), False)


class TestStardustClient(unittest.TestCase):
    """测试 StardustClient 统一客户端"""

    def test_client_initialization(self):
        """测试客户端初始化"""
        client = StardustClient(
            server="http://localhost:6600",
            app_id="TestApp",
            secret="test_secret",
            enable_trace=True,
            enable_config=True,
        )

        self.assertIsNotNone(client._tracer)
        self.assertIsNotNone(client._config)

    def test_client_disabled_features(self):
        """测试禁用功能"""
        client = StardustClient(
            server="http://localhost:6600",
            app_id="TestApp",
            enable_trace=False,
            enable_config=False,
        )

        self.assertIsNone(client._tracer)
        self.assertIsNone(client._config)

        with self.assertRaises(RuntimeError):
            client.new_span("test")

        with self.assertRaises(RuntimeError):
            client.get_config("test")


if __name__ == "__main__":
    unittest.main()
