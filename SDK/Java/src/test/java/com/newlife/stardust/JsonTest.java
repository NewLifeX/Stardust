package com.newlife.stardust;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class JsonTest {

    @Test
    @DisplayName("嵌套对象与数组往返序列化")
    void nestedRoundTrip() {
        final Map<String, Object> root = new LinkedHashMap<String, Object>();
        root.put("name", "星尘");
        root.put("enabled", Boolean.TRUE);
        root.put("count", Long.valueOf(42));
        root.put("ratio", Double.valueOf(3.5));
        root.put("nothing", null);

        final List<Object> items = new ArrayList<Object>();
        items.add("a");
        items.add(Long.valueOf(1));
        items.add(Boolean.FALSE);
        items.add(null);
        final Map<String, Object> child = new LinkedHashMap<String, Object>();
        child.put("id", Long.valueOf(7));
        child.put("tags", Arrays.asList("x", "y"));
        items.add(child);
        root.put("items", items);

        final String json = Json.stringify(root);
        final Map<String, Object> parsed = Json.parseObject(json);

        assertEquals("星尘", parsed.get("name"));
        assertEquals(Boolean.TRUE, parsed.get("enabled"));
        assertEquals(42L, ((Number) parsed.get("count")).longValue());
        assertEquals(3.5d, ((Number) parsed.get("ratio")).doubleValue(), 0.000001d);
        assertNull(parsed.get("nothing"));

        final List<?> parsedItems = (List<?>) parsed.get("items");
        assertEquals(5, parsedItems.size());
        assertEquals("a", parsedItems.get(0));
        assertEquals(1L, ((Number) parsedItems.get(1)).longValue());
        assertEquals(Boolean.FALSE, parsedItems.get(2));
        assertNull(parsedItems.get(3));

        final Map<?, ?> parsedChild = (Map<?, ?>) parsedItems.get(4);
        assertEquals(7L, ((Number) parsedChild.get("id")).longValue());
        assertEquals(Arrays.asList("x", "y"), parsedChild.get("tags"));
        assertEquals(json, Json.stringify(parsed));
    }

    @Test
    @DisplayName("字符串转义与控制字符")
    void escaping() {
        final String value = "quote\" slash\\ newline\n tab\t control\u0001 中文 \u2028";
        final String json = Json.stringify(value);

        assertTrue(json.contains("\\\""), json);
        assertTrue(json.contains("\\\\"), json);
        assertTrue(json.contains("\\n"), json);
        assertTrue(json.contains("\\t"), json);
        assertTrue(json.contains("\\u0001"), json);
        assertTrue(json.contains("\\u2028"), json);
        assertEquals(value, Json.parse(json));
        assertEquals("中文", Json.parse("\"\\u4e2d\\u6587\""));
    }

    @Test
    @DisplayName("整数、浮点数、布尔与 null")
    void numbersAndLiterals() {
        assertEquals(9007199254740993L, ((Number) Json.parse("9007199254740993")).longValue());
        assertEquals(-12L, ((Number) Json.parse("-12")).longValue());
        assertEquals(1250.0d, ((Number) Json.parse("1.25e3")).doubleValue(), 0.000001d);
        assertEquals(1.0d, ((Number) Json.parse("1.0")).doubleValue(), 0.000001d);
        assertEquals(Boolean.TRUE, Json.parse("true"));
        assertEquals(Boolean.FALSE, Json.parse("false"));
        assertNull(Json.parse("null"));
        assertEquals("{}", Json.stringify(Json.parse(" { } ")));
        assertEquals("[]", Json.stringify(Json.parse("\n[]\r\n")));
    }

    @Test
    @DisplayName("非法输入抛出带位置的异常")
    void malformedInput() {
        assertMalformed("{bad}");
        assertMalformed("{\"a\":1,}");
        assertMalformed("[\"a\"");
        assertMalformed("\"unterminated");
        assertMalformed("01");
        assertMalformed("{} extra");
        assertMalformed("{\"a\": }");
        assertMalformed("[1,]");
        assertMalformed("{\"a\" 1}");
        assertMalformed("\"bad \\q escape\"");
        assertThrows(IllegalArgumentException.class, new org.junit.jupiter.api.function.Executable() {
            @Override
            public void execute() {
                Json.parse(null);
            }
        });
    }

    private static void assertMalformed(String text) {
        final IllegalArgumentException error = assertThrows(IllegalArgumentException.class, new org.junit.jupiter.api.function.Executable() {
            @Override
            public void execute() {
                Json.parse(text);
            }
        });
        assertTrue(error.getMessage().contains("position"), error.getMessage());
    }
}
