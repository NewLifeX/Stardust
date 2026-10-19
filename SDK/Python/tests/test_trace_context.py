"""W3C traceparent 解析与格式化测试"""

import unittest

from stardust_sdk.trace_context import (
    TRACE_FLAG_SAMPLED,
    TraceParent,
    find_traceparent,
    format_traceparent,
    is_valid_span_id,
    is_valid_trace_id,
    new_span_id,
    new_trace_id,
    parse_traceparent,
)

TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736"
SPAN_ID = "00f067aa0ba902b7"
TRACEPARENT = "00-%s-%s-01" % (TRACE_ID, SPAN_ID)


class TestParseTraceparent(unittest.TestCase):
    """解析 traceparent"""

    def test_parse_valid(self):
        parsed = parse_traceparent(TRACEPARENT)
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.trace_id, TRACE_ID)
        self.assertEqual(parsed.span_id, SPAN_ID)
        self.assertEqual(parsed.flags, TRACE_FLAG_SAMPLED)
        self.assertEqual(parsed.version, "00")

    def test_parse_bytes(self):
        parsed = parse_traceparent(TRACEPARENT.encode("ascii"))
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.trace_id, TRACE_ID)

    def test_parse_uppercase(self):
        parsed = parse_traceparent(TRACEPARENT.upper())
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.trace_id, TRACE_ID)
        self.assertEqual(parsed.span_id, SPAN_ID)

    def test_parse_short_trace_id_for_dotnet(self):
        parsed = parse_traceparent("00-1234abcd-1a2b3c4d5e6f7a8b-01")
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.trace_id, "1234abcd")
        self.assertEqual(parsed.span_id, "1a2b3c4d5e6f7a8b")

    def test_parse_future_version_with_extensions(self):
        parsed = parse_traceparent("01-%s-%s-01-extra" % (TRACE_ID, SPAN_ID))
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.version, "01")
        self.assertEqual(parsed.trace_id, TRACE_ID)
        self.assertEqual(parsed.span_id, SPAN_ID)

    def test_parse_invalid(self):
        cases = [
            "",
            None,
            "invalid",
            "00-%s-%s" % (TRACE_ID, SPAN_ID),
            "00-%s-%s-01-extra" % (TRACE_ID, SPAN_ID),
            "ff-%s-%s-01" % (TRACE_ID, SPAN_ID),
            "00-%s-%s-zz" % (TRACE_ID, SPAN_ID),
            "00-%s-%s-01" % ("0" * 32, SPAN_ID),
            "00-%s-%s-01" % (TRACE_ID, "0" * 16),
            "00-%s-%s-01" % ("xyz", SPAN_ID),
            "00-%s-%s-01" % (TRACE_ID, "xyz"),
        ]
        for value in cases:
            self.assertIsNone(parse_traceparent(value), "should reject %r" % (value,))

    def test_trace_parent_str(self):
        parsed = TraceParent(TRACE_ID, SPAN_ID, 1, "00")
        self.assertEqual(str(parsed), TRACEPARENT)


class TestFormatTraceparent(unittest.TestCase):
    """格式化 traceparent"""

    def test_format_round_trip(self):
        formatted = format_traceparent(TRACE_ID, SPAN_ID)
        self.assertEqual(formatted, TRACEPARENT)
        parsed = parse_traceparent(formatted)
        self.assertEqual(parsed.trace_id, TRACE_ID)

    def test_format_keeps_short_trace_id(self):
        formatted = format_traceparent("abc", "0123456789abcdef")
        self.assertEqual(formatted, "00-abc-0123456789abcdef-01")

    def test_format_rejects_short_span_id(self):
        with self.assertRaises(ValueError):
            format_traceparent(TRACE_ID, "def")

    def test_format_flags(self):
        self.assertTrue(format_traceparent(TRACE_ID, SPAN_ID, 0).endswith("-00"))
        self.assertTrue(format_traceparent(TRACE_ID, SPAN_ID, "01").endswith("-01"))

    def test_format_invalid(self):
        with self.assertRaises(ValueError):
            format_traceparent("", SPAN_ID)
        with self.assertRaises(ValueError):
            format_traceparent(TRACE_ID, "xyz")

    def test_generated_ids(self):
        trace_id = new_trace_id()
        span_id = new_span_id()
        self.assertEqual(len(trace_id), 32)
        self.assertEqual(len(span_id), 16)
        self.assertTrue(is_valid_trace_id(trace_id))
        self.assertTrue(is_valid_span_id(span_id))
        self.assertNotEqual(trace_id, new_trace_id())


class TestFindTraceparent(unittest.TestCase):
    """从请求头中查找 traceparent"""

    def test_find_in_mapping(self):
        parsed = find_traceparent({"TraceParent": TRACEPARENT})
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.trace_id, TRACE_ID)

    def test_find_wsgi_header(self):
        parsed = find_traceparent({"HTTP_TRACEPARENT": TRACEPARENT})
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.span_id, SPAN_ID)

    def test_find_in_asgi_headers(self):
        parsed = find_traceparent([(b"host", b"example.com"), (b"traceparent", TRACEPARENT.encode())])
        self.assertIsNotNone(parsed)
        self.assertEqual(parsed.trace_id, TRACE_ID)

    def test_find_missing(self):
        self.assertIsNone(find_traceparent({}))
        self.assertIsNone(find_traceparent([]))
        self.assertIsNone(find_traceparent(None))
        self.assertIsNone(find_traceparent({"host": "example.com"}))


if __name__ == "__main__":
    unittest.main()
