package com.newlife.stardust;

import java.util.List;
import java.util.Map;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class SpanBuilderTest {

    @Test
    @DisplayName("累计总数、错误数、耗时与最大最小耗时")
    void aggregatesTotalsAndCosts() {
        final SpanBuilder builder = new SpanBuilder("order.create", 10, 10);

        builder.add(span(1000L, 1010L, null));
        builder.add(span(2000L, 2030L, null));
        builder.add(span(3000L, 3020L, "boom"));

        assertEquals(3L, builder.getTotal());
        assertEquals(1L, builder.getErrors());
        assertEquals(60L, builder.getCost());
        assertEquals(30L, builder.getMaxCost());
        assertEquals(10L, builder.getMinCost());
        assertEquals(1000L, builder.getStartTime());
        assertEquals(3020L, builder.getEndTime());
        assertEquals(2, builder.getSamples().size());
        assertEquals(1, builder.getErrorSamples().size());
        assertEquals("boom", builder.getErrorSamples().get(0).getError());
    }

    @Test
    @DisplayName("正常样本与异常样本分别受上限约束")
    void sampleLimits() {
        final SpanBuilder builder = new SpanBuilder("op", 2, 2);

        for (int i = 0; i < 5; i++) {
            builder.add(span(1000L + i * 10L, 1005L + i * 10L, null));
        }
        for (int i = 0; i < 3; i++) {
            builder.add(span(2000L + i * 10L, 2010L + i * 10L, "error-" + i));
        }

        assertEquals(8L, builder.getTotal());
        assertEquals(3L, builder.getErrors());
        assertEquals(2, builder.getSamples().size());
        assertEquals(2, builder.getErrorSamples().size());

        builder.setMaxSamples(1);
        builder.setMaxErrors(0);
        assertEquals(1, builder.getSamples().size());
        assertEquals(0, builder.getErrorSamples().size());
    }

    @Test
    @DisplayName("toMap 输出服务端字段与样例子段")
    @SuppressWarnings("unchecked")
    void mapsToServerFormat() {
        final SpanBuilder builder = new SpanBuilder("op", 2, 2);
        builder.add(span(1000L, 1010L, null));
        builder.add(span(2000L, 2030L, "failed"));

        final Map<String, Object> map = builder.toMap();
        assertEquals("op", map.get("Name"));
        assertEquals(2L, ((Number) map.get("Total")).longValue());
        assertEquals(1L, ((Number) map.get("Errors")).longValue());
        assertEquals(40L, ((Number) map.get("Cost")).longValue());
        assertEquals(30L, ((Number) map.get("MaxCost")).longValue());
        assertEquals(10L, ((Number) map.get("MinCost")).longValue());

        final List<Map<String, Object>> samples = (List<Map<String, Object>>) map.get("Samples");
        final List<Map<String, Object>> errors = (List<Map<String, Object>>) map.get("ErrorSamples");
        assertEquals(1, samples.size());
        assertEquals(1, errors.size());

        final Map<String, Object> sample = samples.get(0);
        assertNotNull(sample.get("Id"));
        assertNotNull(sample.get("TraceId"));
        assertEquals(1000L, ((Number) sample.get("StartTime")).longValue());
        assertEquals(1010L, ((Number) sample.get("EndTime")).longValue());
        assertEquals("", sample.get("Error"));
        assertEquals("failed", errors.get(0).get("Error"));
        assertTrue(sample.containsKey("ParentId"));
    }

    private static Span span(long start, long end, String error) {
        final Span span = new Span(null, "id-" + start, null, "trace", "op", start);
        if (error != null) span.setError(error);
        span.finishAt(end);

        return span;
    }
}
