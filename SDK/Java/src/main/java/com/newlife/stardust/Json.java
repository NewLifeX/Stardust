package com.newlife.stardust;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Minimal, dependency-free JSON parser and writer.
 *
 * <p>Parsed values are represented as {@link Map}, {@link List}, {@link String},
 * {@link Long}, {@link Double}, {@link Boolean} or {@code null}.</p>
 */
public final class Json {

    private static final char[] HEX = "0123456789abcdef".toCharArray();
    private static final int MAX_DEPTH = 512;

    private Json() {
    }

    /**
     * Parses a JSON document.
     *
     * @param text JSON text
     * @return parsed value
     * @throws IllegalArgumentException if the input is malformed
     */
    public static Object parse(String text) {
        if (text == null) throw new IllegalArgumentException("JSON input is null");

        return new Parser(text).parse();
    }

    /**
     * Parses a JSON object.
     *
     * @param text JSON text
     * @return parsed object
     * @throws IllegalArgumentException if the input is malformed or not an object
     */
    @SuppressWarnings("unchecked")
    public static Map<String, Object> parseObject(String text) {
        final Object value = parse(text);
        if (!(value instanceof Map)) throw new IllegalArgumentException("JSON root must be an object");

        return (Map<String, Object>) value;
    }

    /**
     * Serializes a value to JSON.
     *
     * @param value value to serialize
     * @return JSON text
     * @throws IllegalArgumentException if the value contains an unsupported type
     */
    public static String stringify(Object value) {
        final StringBuilder sb = new StringBuilder(128);
        write(value, sb);

        return sb.toString();
    }

    private static void write(Object value, StringBuilder sb) {
        if (value == null) {
            sb.append("null");
            return;
        }
        if (value instanceof String || value instanceof Character) {
            writeString(value.toString(), sb);
            return;
        }
        if (value instanceof Boolean) {
            sb.append(((Boolean) value).booleanValue() ? "true" : "false");
            return;
        }
        if (value instanceof Map) {
            writeObject((Map<?, ?>) value, sb);
            return;
        }
        if (value instanceof Iterable) {
            writeArray(((Iterable<?>) value).iterator(), sb);
            return;
        }
        if (value instanceof Double || value instanceof Float) {
            final double d = ((Number) value).doubleValue();
            if (Double.isNaN(d) || Double.isInfinite(d)) throw new IllegalArgumentException("NaN and Infinity are not valid JSON numbers");

            sb.append(Double.toString(d));
            return;
        }
        if (value instanceof Number) {
            sb.append(value.toString());
            return;
        }
        if (value.getClass().isArray()) {
            final int length = java.lang.reflect.Array.getLength(value);
            sb.append('[');
            for (int i = 0; i < length; i++) {
                if (i > 0) sb.append(',');
                write(java.lang.reflect.Array.get(value, i), sb);
            }
            sb.append(']');
            return;
        }

        throw new IllegalArgumentException("Unsupported JSON value type: " + value.getClass().getName());
    }

    private static void writeObject(Map<?, ?> map, StringBuilder sb) {
        sb.append('{');
        boolean first = true;
        for (Map.Entry<?, ?> entry : map.entrySet()) {
            if (!first) sb.append(',');
            first = false;
            writeString(String.valueOf(entry.getKey()), sb);
            sb.append(':');
            write(entry.getValue(), sb);
        }
        sb.append('}');
    }

    private static void writeArray(java.util.Iterator<?> iterator, StringBuilder sb) {
        sb.append('[');
        boolean first = true;
        while (iterator.hasNext()) {
            if (!first) sb.append(',');
            first = false;
            write(iterator.next(), sb);
        }
        sb.append(']');
    }

    private static void writeString(String value, StringBuilder sb) {
        sb.append('"');
        for (int i = 0; i < value.length(); i++) {
            final char c = value.charAt(i);
            switch (c) {
                case '"':
                    sb.append("\\\"");
                    break;
                case '\\':
                    sb.append("\\\\");
                    break;
                case '\b':
                    sb.append("\\b");
                    break;
                case '\f':
                    sb.append("\\f");
                    break;
                case '\n':
                    sb.append("\\n");
                    break;
                case '\r':
                    sb.append("\\r");
                    break;
                case '\t':
                    sb.append("\\t");
                    break;
                default:
                    if (c < 0x20 || c == '\u2028' || c == '\u2029') {
                        sb.append("\\u");
                        sb.append(HEX[(c >> 12) & 0x0F]);
                        sb.append(HEX[(c >> 8) & 0x0F]);
                        sb.append(HEX[(c >> 4) & 0x0F]);
                        sb.append(HEX[c & 0x0F]);
                    } else {
                        sb.append(c);
                    }
                    break;
            }
        }
        sb.append('"');
    }

    private static final class Parser {
        private final String text;
        private int pos;
        private int depth;

        Parser(String text) {
            this.text = text;
        }

        Object parse() {
            if (text.length() > 0 && text.charAt(0) == '\uFEFF') pos = 1;
            skipWhitespace();
            if (pos >= text.length()) throw error("unexpected end of input");

            final Object value = parseValue();
            skipWhitespace();
            if (pos != text.length()) throw error("unexpected trailing characters");

            return value;
        }

        private Object parseValue() {
            skipWhitespace();
            if (pos >= text.length()) throw error("unexpected end of input");

            final char c = text.charAt(pos);
            switch (c) {
                case '{':
                    return parseObject();
                case '[':
                    return parseArray();
                case '"':
                    return parseString();
                case 't':
                    expectLiteral("true");
                    return Boolean.TRUE;
                case 'f':
                    expectLiteral("false");
                    return Boolean.FALSE;
                case 'n':
                    expectLiteral("null");
                    return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return parseNumber();
                    throw error("unexpected character '" + c + "'");
            }
        }

        private Map<String, Object> parseObject() {
            enter();
            pos++; // '{'
            final Map<String, Object> map = new LinkedHashMap<String, Object>();
            skipWhitespace();
            if (peek() == '}') {
                pos++;
                depth--;
                return map;
            }

            while (true) {
                skipWhitespace();
                if (peek() != '"') throw error("expected object key string");
                final String key = parseString();
                skipWhitespace();
                if (peek() != ':') throw error("expected ':' after object key");
                pos++;
                map.put(key, parseValue());
                skipWhitespace();
                final char c = peek();
                if (c == ',') {
                    pos++;
                    continue;
                }
                if (c == '}') {
                    pos++;
                    depth--;
                    return map;
                }
                throw error("expected ',' or '}' in object");
            }
        }

        private List<Object> parseArray() {
            enter();
            pos++; // '['
            final List<Object> list = new ArrayList<Object>();
            skipWhitespace();
            if (peek() == ']') {
                pos++;
                depth--;
                return list;
            }

            while (true) {
                list.add(parseValue());
                skipWhitespace();
                final char c = peek();
                if (c == ',') {
                    pos++;
                    continue;
                }
                if (c == ']') {
                    pos++;
                    depth--;
                    return list;
                }
                throw error("expected ',' or ']' in array");
            }
        }

        private String parseString() {
            pos++; // opening quote
            final StringBuilder sb = new StringBuilder();
            while (true) {
                if (pos >= text.length()) throw error("unterminated string");
                final char c = text.charAt(pos++);
                if (c == '"') return sb.toString();
                if (c == '\\') {
                    if (pos >= text.length()) throw error("unterminated escape sequence");
                    final char esc = text.charAt(pos++);
                    switch (esc) {
                        case '"':
                            sb.append('"');
                            break;
                        case '\\':
                            sb.append('\\');
                            break;
                        case '/':
                            sb.append('/');
                            break;
                        case 'b':
                            sb.append('\b');
                            break;
                        case 'f':
                            sb.append('\f');
                            break;
                        case 'n':
                            sb.append('\n');
                            break;
                        case 'r':
                            sb.append('\r');
                            break;
                        case 't':
                            sb.append('\t');
                            break;
                        case 'u':
                            sb.append(parseUnicode());
                            break;
                        default:
                            throw error("invalid escape sequence '\\" + esc + "'");
                    }
                } else if (c < 0x20) {
                    throw error("unescaped control character in string");
                } else {
                    sb.append(c);
                }
            }
        }

        private char parseUnicode() {
            if (pos + 4 > text.length()) throw error("incomplete unicode escape");
            int value = 0;
            for (int i = 0; i < 4; i++) {
                final char c = text.charAt(pos++);
                final int digit;
                if (c >= '0' && c <= '9') digit = c - '0';
                else if (c >= 'a' && c <= 'f') digit = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') digit = c - 'A' + 10;
                else throw error("invalid unicode escape");
                value = (value << 4) | digit;
            }

            return (char) value;
        }

        private Object parseNumber() {
            final int start = pos;
            if (peek() == '-') pos++;

            if (pos >= text.length()) throw error("incomplete number");
            final char first = text.charAt(pos);
            if (first == '0') {
                pos++;
                if (pos < text.length() && isDigit(text.charAt(pos))) throw error("leading zeros are not allowed");
            } else if (first >= '1' && first <= '9') {
                while (pos < text.length() && isDigit(text.charAt(pos))) pos++;
            } else {
                throw error("invalid number");
            }

            boolean isDouble = false;
            if (pos < text.length() && text.charAt(pos) == '.') {
                isDouble = true;
                pos++;
                if (pos >= text.length() || !isDigit(text.charAt(pos))) throw error("missing fraction digits");
                while (pos < text.length() && isDigit(text.charAt(pos))) pos++;
            }
            if (pos < text.length() && (text.charAt(pos) == 'e' || text.charAt(pos) == 'E')) {
                isDouble = true;
                pos++;
                if (pos < text.length() && (text.charAt(pos) == '+' || text.charAt(pos) == '-')) pos++;
                if (pos >= text.length() || !isDigit(text.charAt(pos))) throw error("missing exponent digits");
                while (pos < text.length() && isDigit(text.charAt(pos))) pos++;
            }

            final String number = text.substring(start, pos);
            try {
                if (!isDouble) {
                    try {
                        return Long.valueOf(number);
                    } catch (NumberFormatException overflow) {
                        return Double.valueOf(number);
                    }
                }

                return Double.valueOf(number);
            } catch (NumberFormatException e) {
                throw error("invalid number '" + number + "'");
            }
        }

        private void expectLiteral(String literal) {
            if (!text.startsWith(literal, pos)) throw error("expected '" + literal + "'");
            pos += literal.length();
        }

        private char peek() {
            if (pos >= text.length()) throw error("unexpected end of input");

            return text.charAt(pos);
        }

        private void skipWhitespace() {
            while (pos < text.length()) {
                final char c = text.charAt(pos);
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') pos++;
                else break;
            }
        }

        private void enter() {
            if (++depth > MAX_DEPTH) throw error("maximum nesting depth exceeded");
        }

        private IllegalArgumentException error(String message) {
            return new IllegalArgumentException("Malformed JSON at position " + pos + ": " + message);
        }

        private static boolean isDigit(char c) {
            return c >= '0' && c <= '9';
        }
    }
}
