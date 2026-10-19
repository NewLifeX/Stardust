package com.newlife.stardust;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.UnsupportedEncodingException;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.Charset;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.zip.GZIPOutputStream;

/**
 * Small HTTP transport built on {@link HttpURLConnection}.
 *
 * <p>All Stardust APIs return an envelope of the form
 * {@code {"code":0,"data":{...}}}. Non-zero codes are converted to
 * {@link StardustException}; HTTP 401 is reported as code 401 so callers can re-login.</p>
 */
final class HttpTransport {

    private static final Charset UTF8 = Charset.forName("UTF-8");
    private static final int TIMEOUT_MS = 10000;
    private static final int MAX_ERROR_TEXT = 300;

    private final String server;

    HttpTransport(String server) {
        if (server == null || server.trim().length() == 0) throw new IllegalArgumentException("server is required");

        String value = server.trim();
        while (value.endsWith("/")) {
            value = value.substring(0, value.length() - 1);
        }
        if (value.length() == 0) throw new IllegalArgumentException("server is required");

        this.server = value;
    }

    String getServer() {
        return server;
    }

    Map<String, Object> post(String path, String token, String json, boolean gzip) {
        HttpURLConnection connection = null;
        try {
            final byte[] body = gzip ? gzip(json.getBytes(UTF8)) : json.getBytes(UTF8);
            final String url = buildUrl(path, token);

            connection = (HttpURLConnection) new URL(url).openConnection();
            connection.setRequestMethod("POST");
            connection.setConnectTimeout(TIMEOUT_MS);
            connection.setReadTimeout(TIMEOUT_MS);
            connection.setDoOutput(true);
            connection.setDoInput(true);
            connection.setUseCaches(false);
            connection.setInstanceFollowRedirects(false);
            connection.setRequestProperty("Content-Type", gzip ? "application/x-gzip" : "application/json");
            connection.setRequestProperty("Accept", "application/json");
            connection.setRequestProperty("Content-Length", String.valueOf(body.length));
            connection.setFixedLengthStreamingMode(body.length);

            final OutputStream output = connection.getOutputStream();
            try {
                output.write(body);
                output.flush();
            } finally {
                output.close();
            }

            final int status = connection.getResponseCode();
            final InputStream input = status >= 400 ? connection.getErrorStream() : connection.getInputStream();
            final String text = readAll(input);

            if (status == 401) throw new StardustException(401, messageFrom(text, "Unauthorized"));
            if (status < 200 || status >= 300) throw new StardustException(status, messageFrom(text, "HTTP " + status));
            if (text.length() == 0) return new LinkedHashMap<String, Object>();

            final Object parsed;
            try {
                parsed = Json.parse(text);
            } catch (IllegalArgumentException e) {
                throw new StardustException(-1, "Invalid JSON response: " + e.getMessage(), e);
            }
            if (!(parsed instanceof Map)) throw new StardustException(-1, "Invalid response: expected a JSON object");

            final Map<?, ?> envelope = (Map<?, ?>) parsed;
            final Object codeValue = envelope.get("code");
            final int code = codeOf(codeValue);
            if (code != 0) {
                final String message = messageFrom(envelope.get("message"), "Server returned code " + code);
                throw new StardustException(code, message);
            }

            final Object data = envelope.get("data");
            if (data == null) return new LinkedHashMap<String, Object>();
            if (!(data instanceof Map)) throw new StardustException(-1, "Invalid response: 'data' must be a JSON object");

            @SuppressWarnings("unchecked")
            final Map<String, Object> result = (Map<String, Object>) data;
            return result;
        } catch (StardustException e) {
            throw e;
        } catch (IOException e) {
            throw new StardustException(-1, "HTTP request failed: " + e.getMessage(), e);
        } finally {
            if (connection != null) connection.disconnect();
        }
    }

    private String buildUrl(String path, String token) {
        final StringBuilder sb = new StringBuilder(server);
        if (path != null && path.length() > 0) {
            if (!path.startsWith("/")) sb.append('/');
            sb.append(path);
        }
        if (token != null && token.length() > 0) {
            sb.append(sb.indexOf("?") >= 0 ? '&' : '?');
            sb.append("Token=");
            try {
                sb.append(URLEncoder.encode(token, "UTF-8"));
            } catch (UnsupportedEncodingException e) {
                sb.append(token);
            }
        }

        return sb.toString();
    }

    private static byte[] gzip(byte[] data) throws IOException {
        final ByteArrayOutputStream buffer = new ByteArrayOutputStream(Math.max(64, data.length / 2));
        final GZIPOutputStream gzip = new GZIPOutputStream(buffer);
        try {
            gzip.write(data);
            gzip.finish();
        } finally {
            gzip.close();
        }

        return buffer.toByteArray();
    }

    private static String readAll(InputStream input) throws IOException {
        if (input == null) return "";

        final ByteArrayOutputStream buffer = new ByteArrayOutputStream();
        try {
            final byte[] chunk = new byte[4096];
            int count;
            while ((count = input.read(chunk)) >= 0) {
                buffer.write(chunk, 0, count);
            }
        } finally {
            input.close();
        }

        return new String(buffer.toByteArray(), UTF8);
    }

    private static int codeOf(Object value) {
        if (value instanceof Number) return ((Number) value).intValue();
        if (value == null) return 0;
        try {
            return Integer.parseInt(value.toString().trim());
        } catch (NumberFormatException e) {
            return -1;
        }
    }

    private static String messageFrom(Object value, String fallback) {
        if (value instanceof Map) {
            final Object message = ((Map<?, ?>) value).get("message");
            if (message != null) return message.toString();
        }
        if (value instanceof String) {
            final String text = ((String) value).trim();
            if (text.length() > 0) return text.length() > MAX_ERROR_TEXT ? text.substring(0, MAX_ERROR_TEXT) : text;
        }

        return fallback;
    }
}
