"""自动埋点测试"""

import asyncio
import sqlite3
import sys
import threading
import types
import unittest
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import requests

from stardust_sdk import StardustClient, StardustTracer
from stardust_sdk.instrument import is_installed, uninstall
from stardust_sdk.instrument import db as db_instrument
from stardust_sdk.instrument.web import StardustASGIMiddleware, StardustWSGIMiddleware
from stardust_sdk.trace_context import parse_traceparent

TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736"
SPAN_ID = "00f067aa0ba902b7"
TRACEPARENT = "00-%s-%s-01" % (TRACE_ID, SPAN_ID)


class RecordingTracer(StardustTracer):
    """记录完成的 Span，便于断言"""

    def __init__(self):
        super().__init__(
            server="http://127.0.0.1:1",
            app_id="TestApp",
            client_id="test@1",
            auto_instrument=False,
        )
        self.spans = []

    def _finish_span(self, span):
        self.spans.append(span)
        super()._finish_span(span)

    def find_span(self, name):
        for span in reversed(self.spans):
            if span.name == name:
                return span
        return None

    def clear(self):
        self.spans = []


class _Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def _respond(self):
        status = 500 if self.path.startswith("/error") else 200
        body = b"ok"
        self.send_response(status)
        self.send_header("Content-Type", "text/plain")
        self.send_header("Content-Length", str(len(body)))
        self.send_header(
            "X-Stardust-Traceparent", self.headers.get("traceparent", "")
        )
        self.end_headers()
        self.wfile.write(body)

    do_GET = _respond
    do_POST = _respond

    def log_message(self, fmt, *args):
        pass


class InstrumentTestCase(unittest.TestCase):
    """自动埋点测试基类"""

    @classmethod
    def setUpClass(cls):
        cls.httpd = ThreadingHTTPServer(("127.0.0.1", 0), _Handler)
        cls.port = cls.httpd.server_address[1]
        cls.base_url = "http://127.0.0.1:%d" % cls.port
        cls.server_thread = threading.Thread(target=cls.httpd.serve_forever, daemon=True)
        cls.server_thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.httpd.shutdown()
        cls.httpd.server_close()

    def setUp(self):
        uninstall()
        db_instrument.unpatch()
        self.tracer = RecordingTracer()

    def tearDown(self):
        uninstall()
        sys.modules.pop("redis", None)

    def session(self):
        session = requests.Session()
        session.trust_env = False
        return session


class TestHttpInstrumentation(InstrumentTestCase):
    """HTTP 客户端自动埋点"""

    def test_requests_injects_traceparent(self):
        self.tracer.instrument()
        response = self.session().get(self.base_url + "/hello?q=1")

        self.assertEqual(response.status_code, 200)
        parsed = parse_traceparent(response.headers.get("X-Stardust-Traceparent"))
        self.assertIsNotNone(parsed)
        self.assertEqual(len(parsed.trace_id), 32)
        self.assertEqual(len(parsed.span_id), 16)

        span = self.tracer.find_span("GET 127.0.0.1:%d/hello" % self.port)
        self.assertIsNotNone(span)
        self.assertEqual(span.tag, self.base_url + "/hello?q=1")
        self.assertEqual(span.error, "")

    def test_requests_keeps_existing_traceparent(self):
        self.tracer.instrument()
        response = self.session().get(
            self.base_url + "/existing", headers={"traceparent": "00-abc-def-01"}
        )
        self.assertEqual(
            response.headers.get("X-Stardust-Traceparent"), "00-abc-def-01"
        )

    def test_requests_marks_5xx_error(self):
        self.tracer.instrument()
        response = self.session().get(self.base_url + "/error")
        self.assertEqual(response.status_code, 500)

        span = self.tracer.find_span("GET 127.0.0.1:%d/error" % self.port)
        self.assertIsNotNone(span)
        self.assertIn("500", span.error)

    def test_requests_records_network_error(self):
        self.tracer.instrument()
        with self.assertRaises(requests.RequestException):
            self.session().get("http://127.0.0.1:1/unreachable", timeout=0.5)

        span = self.tracer.find_span("GET 127.0.0.1:1/unreachable")
        self.assertIsNotNone(span)
        self.assertNotEqual(span.error, "")

    def test_self_requests_are_not_traced(self):
        self.tracer.instrument()
        before = len(self.tracer.spans)

        for path in ("/App/Login", "/App/Ping", "/Trace/Report", "/Trace/ReportRaw"):
            response = self.session().get(self.base_url + path)
            self.assertEqual(
                response.headers.get("X-Stardust-Traceparent"),
                "",
                "traceparent should not be injected for %s" % path,
            )

        self.assertEqual(len(self.tracer.spans), before)

    def test_urllib_injects_traceparent(self):
        self.tracer.instrument()
        response = urllib.request.urlopen(self.base_url + "/urllib")
        try:
            self.assertEqual(response.status, 200)
            parsed = parse_traceparent(response.headers.get("X-Stardust-Traceparent"))
            self.assertIsNotNone(parsed)
        finally:
            response.close()

        self.assertIsNotNone(
            self.tracer.find_span("GET 127.0.0.1:%d/urllib" % self.port)
        )

    def test_urllib_keeps_existing_traceparent(self):
        self.tracer.instrument()
        request = urllib.request.Request(
            self.base_url + "/urllib-existing",
            headers={"Traceparent": "00-abc-def-01"},
        )
        response = urllib.request.urlopen(request)
        try:
            self.assertEqual(
                response.headers.get("X-Stardust-Traceparent"), "00-abc-def-01"
            )
        finally:
            response.close()

    def test_requests_to_wsgi_propagates_context(self):
        from wsgiref.simple_server import WSGIRequestHandler, make_server

        class QuietHandler(WSGIRequestHandler):
            def log_message(self, fmt, *args):
                pass

        def application(environ, start_response):
            start_response("200 OK", [("Content-Length", "2")])
            return [b"ok"]

        wrapped = StardustWSGIMiddleware(application, tracer=self.tracer)
        server = make_server("127.0.0.1", 0, wrapped, handler_class=QuietHandler)
        port = server.server_port
        thread = threading.Thread(target=server.handle_request, daemon=True)
        thread.start()

        self.tracer.instrument()
        try:
            response = self.session().get("http://127.0.0.1:%d/e2e" % port)
            self.assertEqual(response.status_code, 200)
        finally:
            thread.join(timeout=10)
            server.server_close()

        client_span = self.tracer.find_span("GET 127.0.0.1:%d/e2e" % port)
        server_span = self.tracer.find_span("GET /e2e")
        self.assertIsNotNone(client_span)
        self.assertIsNotNone(server_span)
        self.assertEqual(server_span.trace_id, client_span.trace_id)
        self.assertEqual(server_span.parent_id, client_span.id)

    def test_uninstall_restores_requests(self):
        self.tracer.instrument()
        self.assertTrue(is_installed())
        patched = requests.sessions.Session.send
        self.assertTrue(getattr(patched, "__stardust_instrumented__", False))

        uninstall()
        self.assertFalse(is_installed())
        self.assertFalse(
            getattr(requests.sessions.Session.send, "__stardust_instrumented__", False)
        )


class TestWebInstrumentation(InstrumentTestCase):
    """Web 入口自动埋点"""

    @staticmethod
    def app_ok(environ, start_response):
        start_response("200 OK", [("Content-Type", "text/plain")])
        return [b"ok"]

    @staticmethod
    def app_error(environ, start_response):
        start_response("500 Internal Server Error", [("Content-Type", "text/plain")])
        return [b"boom"]

    def environ(self, path="/orders/1"):
        return {
            "REQUEST_METHOD": "GET",
            "PATH_INFO": path,
            "QUERY_STRING": "a=1",
            "HTTP_HOST": "example.com",
            "wsgi.url_scheme": "http",
            "HTTP_TRACEPARENT": TRACEPARENT,
        }

    def test_wsgi_extracts_incoming_context(self):
        middleware = StardustWSGIMiddleware(self.app_ok, tracer=self.tracer)
        statuses = []

        def start_response(status, headers, exc_info=None):
            statuses.append(status)

        body = b"".join(middleware(self.environ(), start_response))

        self.assertEqual(body, b"ok")
        self.assertEqual(statuses, ["200 OK"])

        span = self.tracer.find_span("GET /orders/1")
        self.assertIsNotNone(span)
        self.assertEqual(span.trace_id, TRACE_ID)
        self.assertEqual(span.parent_id, SPAN_ID)
        self.assertEqual(span.tag, "GET http://example.com/orders/1?a=1")
        self.assertEqual(span.error, "")

    def test_wsgi_generates_context_without_traceparent(self):
        middleware = StardustWSGIMiddleware(self.app_ok, tracer=self.tracer)
        environ = self.environ("/no-context")
        environ.pop("HTTP_TRACEPARENT")

        list(middleware(environ, lambda status, headers, exc_info=None: None))

        span = self.tracer.find_span("GET /no-context")
        self.assertIsNotNone(span)
        self.assertEqual(len(span.trace_id), 32)
        self.assertEqual(span.parent_id, "")

    def test_wsgi_marks_5xx(self):
        middleware = StardustWSGIMiddleware(self.app_error, tracer=self.tracer)
        list(middleware(self.environ("/fail"), lambda status, headers, exc_info=None: None))

        span = self.tracer.find_span("GET /fail")
        self.assertIsNotNone(span)
        self.assertIn("500", span.error)

    def test_wsgi_marks_exception(self):
        def app_boom(environ, start_response):
            raise RuntimeError("boom")

        middleware = StardustWSGIMiddleware(app_boom, tracer=self.tracer)
        with self.assertRaises(RuntimeError):
            middleware(self.environ("/boom"), lambda status, headers, exc_info=None: None)

        span = self.tracer.find_span("GET /boom")
        self.assertIsNotNone(span)
        self.assertIn("boom", span.error)

    def test_nested_middleware_only_creates_one_span(self):
        inner = StardustWSGIMiddleware(self.app_ok, tracer=self.tracer)
        outer = StardustWSGIMiddleware(inner, tracer=self.tracer)

        list(outer(self.environ("/dedupe"), lambda status, headers, exc_info=None: None))

        count = len([s for s in self.tracer.spans if s.name == "GET /dedupe"])
        self.assertEqual(count, 1)

    def test_asgi_extracts_incoming_context(self):
        async def application(scope, receive, send):
            await send({"type": "http.response.start", "status": 200, "headers": []})
            await send({"type": "http.response.body", "body": b"ok"})

        middleware = StardustASGIMiddleware(application, tracer=self.tracer)
        sent = []

        async def send(message):
            sent.append(message)

        scope = {
            "type": "http",
            "method": "GET",
            "path": "/asgi/1",
            "scheme": "http",
            "query_string": b"a=1",
            "headers": [(b"host", b"example.com"), (b"traceparent", TRACEPARENT.encode())],
        }

        asyncio.run(middleware(scope, lambda: None, send))

        self.assertEqual(sent[0]["status"], 200)
        span = self.tracer.find_span("GET /asgi/1")
        self.assertIsNotNone(span)
        self.assertEqual(span.trace_id, TRACE_ID)
        self.assertEqual(span.parent_id, SPAN_ID)
        self.assertEqual(span.tag, "GET http://example.com/asgi/1?a=1")

    def test_asgi_marks_5xx(self):
        async def application(scope, receive, send):
            await send({"type": "http.response.start", "status": 503, "headers": []})
            await send({"type": "http.response.body", "body": b""})

        middleware = StardustASGIMiddleware(application, tracer=self.tracer)

        async def send(message):
            pass

        scope = {"type": "http", "method": "GET", "path": "/asgi/fail", "headers": []}
        asyncio.run(middleware(scope, lambda: None, send))

        span = self.tracer.find_span("GET /asgi/fail")
        self.assertIsNotNone(span)
        self.assertIn("503", span.error)

    def test_flask_auto_patch(self):
        module = types.ModuleType("flask")

        class Flask:
            def wsgi_app(self, environ, start_response):
                start_response("200 OK", [])
                return [b"ok"]

        module.Flask = Flask
        sys.modules["flask"] = module
        self.addCleanup(sys.modules.pop, "flask", None)

        self.tracer.instrument()
        self.assertTrue(getattr(Flask.wsgi_app, "__stardust_instrumented__", False))

        app = Flask()
        body = b"".join(
            app.wsgi_app(self.environ("/flask-auto"), lambda status, headers, exc_info=None: None)
        )
        self.assertEqual(body, b"ok")
        self.assertIsNotNone(self.tracer.find_span("GET /flask-auto"))

        uninstall()
        self.assertFalse(getattr(Flask.wsgi_app, "__stardust_instrumented__", False))

    def test_starlette_auto_patch(self):
        module = types.ModuleType("starlette.applications")

        class Starlette:
            async def __call__(self, scope, receive, send):
                await send({"type": "http.response.start", "status": 200, "headers": []})
                await send({"type": "http.response.body", "body": b"ok"})

        module.Starlette = Starlette
        sys.modules["starlette.applications"] = module
        self.addCleanup(sys.modules.pop, "starlette.applications", None)

        self.tracer.instrument()
        self.assertTrue(getattr(Starlette.__call__, "__stardust_instrumented__", False))

        sent = []

        async def send(message):
            sent.append(message)

        scope = {
            "type": "http",
            "method": "GET",
            "path": "/starlette-auto",
            "headers": [(b"host", b"example.com"), (b"traceparent", TRACEPARENT.encode())],
        }
        asyncio.run(Starlette()(scope, lambda: None, send))

        self.assertEqual(sent[0]["status"], 200)
        span = self.tracer.find_span("GET /starlette-auto")
        self.assertIsNotNone(span)
        self.assertEqual(span.trace_id, TRACE_ID)

        uninstall()
        self.assertFalse(getattr(Starlette.__call__, "__stardust_instrumented__", False))


class TestDbInstrumentation(InstrumentTestCase):
    """数据库自动埋点"""

    def test_sqlite_spans(self):
        self.tracer.instrument()
        connection = sqlite3.connect(":memory:")
        connection.row_factory = sqlite3.Row
        try:
            connection.execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT)")
            connection.execute("INSERT INTO users (name) VALUES (?)", ("alice",))

            cursor = connection.cursor()
            cursor.executemany(
                "INSERT INTO users (name) VALUES (?)", [("bob",), ("carol",)]
            )
            cursor.execute("SELECT * FROM users ORDER BY id")
            rows = cursor.fetchall()
            self.assertEqual(len(rows), 3)
            self.assertEqual(rows[0]["name"], "alice")

            self.assertIsNotNone(self.tracer.find_span("db:sqlite:create:users"))
            self.assertIsNotNone(self.tracer.find_span("db:sqlite:insert:users"))
            select_span = self.tracer.find_span("db:sqlite:select:users")
            self.assertIsNotNone(select_span)
            self.assertIn("SELECT", select_span.tag)

            connection.execute("CREATE TABLE users_20240101 (id INTEGER PRIMARY KEY)")
            connection.execute("SELECT * FROM users_20240101")
            self.assertIsNotNone(self.tracer.find_span("db:sqlite:select:users"))

            with self.assertRaises(sqlite3.OperationalError):
                connection.execute("SELECT * FROM missing_table")
            error_span = self.tracer.find_span("db:sqlite:select:missing_table")
            self.assertIsNotNone(error_span)
            self.assertNotEqual(error_span.error, "")
        finally:
            connection.close()

    def test_connect_uninstall_restores(self):
        original = sqlite3.connect
        self.tracer.instrument()
        self.assertIsNot(sqlite3.connect, original)

        uninstall()
        self.assertIs(sqlite3.connect, original)


class TestRedisInstrumentation(InstrumentTestCase):
    """Redis 自动埋点"""

    def install_fake_redis(self):
        module = types.ModuleType("redis")

        class FakeRedis:
            def execute_command(self, *args, **options):
                if args[0] in (b"ERR", "ERR"):
                    raise RuntimeError("redis error")
                return "OK"

        module.Redis = FakeRedis
        sys.modules["redis"] = module
        return FakeRedis

    def test_redis_span(self):
        client_cls = self.install_fake_redis()
        self.tracer.instrument()

        client = client_cls()
        self.assertEqual(client.execute_command(b"GET", b"key:1"), "OK")

        span = self.tracer.find_span("redis:GET")
        self.assertIsNotNone(span)
        self.assertEqual(span.tag, "key:1")
        self.assertEqual(span.error, "")

    def test_redis_error(self):
        client_cls = self.install_fake_redis()
        self.tracer.instrument()

        client = client_cls()
        with self.assertRaises(RuntimeError):
            client.execute_command(b"ERR", b"key")

        span = self.tracer.find_span("redis:ERR")
        self.assertIsNotNone(span)
        self.assertIn("redis error", span.error)

    def test_excludes(self):
        client_cls = self.install_fake_redis()
        self.tracer.excludes = ["redis:GET"]
        self.tracer.instrument()

        client_cls().execute_command(b"GET", b"key")

        self.assertIsNone(self.tracer._builders.get("redis:GET"))


class TestInstallerAndContext(InstrumentTestCase):
    """安装器与上下文"""

    def test_auto_instrument_false(self):
        tracer = StardustTracer(
            server="http://127.0.0.1:1",
            app_id="TestApp",
            client_id="test@1",
            auto_instrument=False,
        )
        self.assertFalse(is_installed())

        tracer.instrument()
        self.assertTrue(is_installed())
        tracer.instrument()
        self.assertTrue(is_installed())

        tracer.uninstrument()
        self.assertFalse(is_installed())

    def test_auto_instrument_default_on(self):
        tracer = StardustTracer(
            server="http://127.0.0.1:1",
            app_id="TestApp",
            client_id="test@1",
        )
        try:
            self.assertTrue(is_installed())
        finally:
            tracer.uninstrument()

    def test_client_auto_instrument_false(self):
        client = StardustClient(
            server="http://127.0.0.1:1",
            app_id="TestApp",
            secret="",
            enable_trace=True,
            enable_config=False,
            auto_instrument=False,
        )
        self.assertFalse(is_installed())
        self.assertIsNotNone(client.tracer)

    def test_nested_spans_auto_parent(self):
        self.assertIsNone(self.tracer.current_span)
        with self.tracer.new_span("parent") as parent:
            self.assertIs(self.tracer.current_span, parent)
            with self.tracer.new_span("child") as child:
                self.assertEqual(child.parent_id, parent.id)
                self.assertEqual(child.trace_id, parent.trace_id)
        self.assertIsNone(self.tracer.current_span)

    def test_context_is_thread_isolated(self):
        with self.tracer.new_span("main"):
            seen = []

            def worker():
                seen.append(self.tracer.current_span)

            thread = threading.Thread(target=worker)
            thread.start()
            thread.join()

            self.assertIsNone(seen[0])
            self.assertEqual(self.tracer.current_span.name, "main")

    def test_context_is_asyncio_isolated(self):
        async def child(name):
            with self.tracer.new_span(name):
                await asyncio.sleep(0)
                return self.tracer.current_span.name

        async def main():
            with self.tracer.new_span("root"):
                return await asyncio.gather(child("a"), child("b"))

        results = asyncio.run(main())
        self.assertEqual(results, ["a", "b"])
        self.assertIsNone(self.tracer.current_span)


if __name__ == "__main__":
    unittest.main()
