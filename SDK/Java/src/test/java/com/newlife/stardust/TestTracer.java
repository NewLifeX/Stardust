package com.newlife.stardust;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * Test tracer that records created spans without starting network tasks.
 */
final class TestTracer extends StardustTracer {

    final List<Span> spans = Collections.synchronizedList(new ArrayList<Span>());

    TestTracer() {
        super("http://127.0.0.1:1", "test-app", "secret");
    }

    @Override
    public Span newSpan(String name) {
        return record(super.newSpan(name));
    }

    @Override
    public Span newSpan(String name, String parentId) {
        return record(super.newSpan(name, parentId));
    }

    @Override
    public Span newSpan(String name, TraceContext context) {
        return record(super.newSpan(name, context));
    }

    Span last() {
        return spans.isEmpty() ? null : spans.get(spans.size() - 1);
    }

    private Span record(Span span) {
        if (!span.isNoop()) spans.add(span);

        return span;
    }
}
