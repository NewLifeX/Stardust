package stardust

import (
	"strings"
	"testing"
)

func TestParseTraceparentValid(t *testing.T) {
	const header = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"

	context, ok := ParseTraceparent(header)
	if !ok {
		t.Fatalf("ParseTraceparent(%q) should succeed", header)
	}
	if context.TraceID != "4bf92f3577b34da6a3ce929d0e0e4736" {
		t.Errorf("unexpected TraceID: %s", context.TraceID)
	}
	if context.SpanID != "00f067aa0ba902b7" {
		t.Errorf("unexpected SpanID: %s", context.SpanID)
	}
	if !context.Sampled {
		t.Error("flags=01 should be sampled")
	}
}

func TestParseTraceparentUppercaseAndSpaces(t *testing.T) {
	context, ok := ParseTraceparent("  00-4BF92F3577B34DA6A3CE929D0E0E4736-00F067AA0BA902B7-00  ")
	if !ok {
		t.Fatal("uppercase traceparent should be accepted")
	}
	if context.TraceID != "4bf92f3577b34da6a3ce929d0e0e4736" {
		t.Errorf("TraceID should be lowercased, got %s", context.TraceID)
	}
	if context.SpanID != "00f067aa0ba902b7" {
		t.Errorf("SpanID should be lowercased, got %s", context.SpanID)
	}
	if context.Sampled {
		t.Error("flags=00 should not be sampled")
	}
}

// .NET 历史实现的 TraceId 可能短于 32 位，入口必须兼容
func TestParseTraceparentAcceptsShortTraceID(t *testing.T) {
	context, ok := ParseTraceparent("00-0123456789abcdef-00f067aa0ba902b7-01")
	if !ok {
		t.Fatal("short trace id should be accepted for .NET compatibility")
	}
	if context.TraceID != "0123456789abcdef" {
		t.Errorf("unexpected TraceID: %s", context.TraceID)
	}
}

func TestParseTraceparentFutureVersion(t *testing.T) {
	context, ok := ParseTraceparent("01-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01-extra")
	if !ok {
		t.Fatal("future version with extra fields should be accepted")
	}
	if context.TraceID != "4bf92f3577b34da6a3ce929d0e0e4736" {
		t.Errorf("unexpected TraceID: %s", context.TraceID)
	}
}

func TestParseTraceparentInvalid(t *testing.T) {
	tests := []struct {
		name   string
		header string
	}{
		{"empty", ""},
		{"missing parts", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7"},
		{"version ff", "ff-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"},
		{"version 00 extra fields", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01-extra"},
		{"bad version", "0g-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"},
		{"trace id non hex", "00-zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz-00f067aa0ba902b7-01"},
		{"trace id all zero", "00-00000000000000000000000000000000-00f067aa0ba902b7-01"},
		{"trace id too long", "00-" + strings.Repeat("a", 65) + "-00f067aa0ba902b7-01"},
		{"span id short", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa-01"},
		{"span id all zero", "00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01"},
		{"bad flags", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-zz"},
		{"short flags", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-1"},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if _, ok := ParseTraceparent(tt.header); ok {
				t.Errorf("ParseTraceparent(%q) should fail", tt.header)
			}
		})
	}
}

func TestFormatTraceparent(t *testing.T) {
	context := &TraceContext{
		TraceID: "4BF92F3577B34DA6A3CE929D0E0E4736",
		SpanID:  "00F067AA0BA902B7",
		Sampled: true,
	}
	got := FormatTraceparent(context)
	want := "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
	if got != want {
		t.Errorf("FormatTraceparent = %q, want %q", got, want)
	}

	roundTrip, ok := ParseTraceparent(got)
	if !ok {
		t.Fatal("formatted traceparent should parse")
	}
	if roundTrip.TraceID != strings.ToLower(context.TraceID) || roundTrip.SpanID != strings.ToLower(context.SpanID) {
		t.Error("round trip mismatch")
	}
}

func TestFormatTraceparentInvalid(t *testing.T) {
	tests := []*TraceContext{
		nil,
		{},
		{TraceID: "abc", SpanID: ""},
		{TraceID: "zz", SpanID: "00f067aa0ba902b7"},
		{TraceID: "4bf92f3577b34da6a3ce929d0e0e4736", SpanID: "00f067aa"},
		{TraceID: strings.Repeat("a", 65), SpanID: "00f067aa0ba902b7"},
	}

	for _, context := range tests {
		if got := FormatTraceparent(context); got != "" {
			t.Errorf("FormatTraceparent(%+v) = %q, want empty", context, got)
		}
	}
}

func TestNewTraceIDAndSpanID(t *testing.T) {
	seen := make(map[string]struct{})
	for i := 0; i < 100; i++ {
		traceID := NewTraceID()
		if len(traceID) != 32 {
			t.Fatalf("TraceID length = %d, want 32 (%s)", len(traceID), traceID)
		}
		if !isHexString(traceID) || strings.ToLower(traceID) != traceID {
			t.Fatalf("TraceID should be lowercase hex: %s", traceID)
		}
		if isZeroHex(traceID) {
			t.Fatal("TraceID should not be all zero")
		}
		if _, ok := seen[traceID]; ok {
			t.Fatalf("duplicate TraceID: %s", traceID)
		}
		seen[traceID] = struct{}{}

		spanID := NewSpanID()
		if len(spanID) != 16 {
			t.Fatalf("SpanID length = %d, want 16 (%s)", len(spanID), spanID)
		}
		if !isHexString(spanID) || strings.ToLower(spanID) != spanID {
			t.Fatalf("SpanID should be lowercase hex: %s", spanID)
		}
	}
}

func TestNewSpanUses32HexTraceID(t *testing.T) {
	tracer := NewTracer("http://localhost:6600", "TestApp", "TestSecret")
	t.Cleanup(func() { tracer.SetAutoInstrument(false) })

	span := tracer.NewSpan("op", "")
	if len(span.TraceID) != 32 {
		t.Errorf("TraceID length = %d, want 32", len(span.TraceID))
	}
	if len(span.ID) != 16 {
		t.Errorf("Span ID length = %d, want 16", len(span.ID))
	}
}

func TestContextSpanHelpers(t *testing.T) {
	tracer := NewTracer("http://localhost:6600", "TestApp", "TestSecret")
	t.Cleanup(func() { tracer.SetAutoInstrument(false) })

	parent := tracer.NewSpan("parent", "")
	ctx := ContextWithSpan(nil, parent)
	if SpanFromContext(ctx) != parent {
		t.Fatal("SpanFromContext should return the stored span")
	}
	if SpanFromContext(nil) != nil {
		t.Fatal("SpanFromContext(nil) should be nil")
	}

	childCtx, child := tracer.NewSpanFromContext(ctx, "child")
	if child == nil {
		t.Fatal("NewSpanFromContext returned nil span")
	}
	if child.ParentID != parent.ID {
		t.Errorf("ParentID = %s, want %s", child.ParentID, parent.ID)
	}
	if child.TraceID != parent.TraceID {
		t.Errorf("child TraceID = %s, want %s", child.TraceID, parent.TraceID)
	}
	if SpanFromContext(childCtx) != child {
		t.Error("child span should be stored in context")
	}

	grandChild := NewChildSpan(child, "grand")
	if grandChild.ParentID != child.ID || grandChild.TraceID != parent.TraceID {
		t.Error("NewChildSpan should reuse trace and set parent")
	}
	if NewChildSpan(nil, "x") != nil {
		t.Error("NewChildSpan(nil) should be nil")
	}
}
