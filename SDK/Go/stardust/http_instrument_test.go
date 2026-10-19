package stardust

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

// newInstrumentTestTracer 创建测试追踪器，并在测试结束后卸载默认传输埋点
func newInstrumentTestTracer(t *testing.T) *Tracer {
	t.Helper()

	tracer := NewTracer("http://127.0.0.1:1", "TestApp", "TestSecret")
	t.Cleanup(func() {
		tracer.SetAutoInstrument(false)
		SetDefaultTracer(nil)
	})
	return tracer
}

func findBuilder(tracer *Tracer, name string) *SpanBuilder {
	tracer.mu.Lock()
	defer tracer.mu.Unlock()
	return tracer.builders[name]
}

func builderTotal(tracer *Tracer, name string) int {
	builder := findBuilder(tracer, name)
	if builder == nil {
		return 0
	}
	return builder.Total
}

func TestOutgoingHTTPInjectsTraceparent(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	headerCh := make(chan string, 1)
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		headerCh <- r.Header.Get(TraceparentHeader)
		w.WriteHeader(http.StatusNoContent)
	}))
	defer server.Close()

	parent := tracer.NewSpan("parent", "")
	ctx := ContextWithSpan(context.Background(), parent)

	client := tracer.WrapClient(&http.Client{})
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, server.URL+"/api/orders?page=1", nil)
	if err != nil {
		t.Fatalf("NewRequestWithContext failed: %v", err)
	}

	resp, err := client.Do(req)
	if err != nil {
		t.Fatalf("request failed: %v", err)
	}
	resp.Body.Close()

	if resp.StatusCode != http.StatusNoContent {
		t.Errorf("status = %d, want 204", resp.StatusCode)
	}

	traceContext, ok := ParseTraceparent(<-headerCh)
	if !ok {
		t.Fatal("outgoing request should carry a valid traceparent")
	}
	if traceContext.TraceID != parent.TraceID {
		t.Errorf("trace id = %s, want %s", traceContext.TraceID, parent.TraceID)
	}
	if traceContext.SpanID == parent.ID {
		t.Error("traceparent span id should be the child span id")
	}

	name := http.MethodGet + " " + strings.TrimPrefix(server.URL, "http://") + "/api/orders"
	builder := findBuilder(tracer, name)
	if builder == nil {
		t.Fatalf("span %q was not recorded", name)
	}
	if builder.Total != 1 {
		t.Errorf("span total = %d, want 1", builder.Total)
	}
	sample := builder.Samples[0]
	if sample.ParentID != parent.ID {
		t.Errorf("span parent = %s, want %s", sample.ParentID, parent.ID)
	}
	if sample.TraceID != parent.TraceID {
		t.Errorf("span trace = %s, want %s", sample.TraceID, parent.TraceID)
	}
	if sample.Tag != server.URL+"/api/orders?page=1" {
		t.Errorf("span tag = %q, want full URL", sample.Tag)
	}
}

func TestOutgoingHTTPKeepsCallerTraceparent(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	const callerHeader = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
	headerCh := make(chan string, 1)
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		headerCh <- r.Header.Get(TraceparentHeader)
	}))
	defer server.Close()

	req, _ := http.NewRequest(http.MethodGet, server.URL+"/downstream", nil)
	req.Header.Set(TraceparentHeader, callerHeader)

	resp, err := tracer.WrapClient(&http.Client{}).Do(req)
	if err != nil {
		t.Fatalf("request failed: %v", err)
	}
	resp.Body.Close()

	if got := <-headerCh; got != callerHeader {
		t.Errorf("traceparent = %q, want caller value %q", got, callerHeader)
	}
}

func TestOutgoingHTTPErrorStatuses(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.Error(w, "boom", http.StatusInternalServerError)
	}))
	defer server.Close()

	req, _ := http.NewRequest(http.MethodPost, server.URL+"/fail", nil)
	parent := tracer.NewSpan("parent", "")
	req = req.WithContext(ContextWithSpan(context.Background(), parent))

	resp, err := tracer.WrapClient(nil).Do(req)
	if err != nil {
		t.Fatalf("request failed: %v", err)
	}
	resp.Body.Close()

	name := http.MethodPost + " " + strings.TrimPrefix(server.URL, "http://") + "/fail"
	builder := findBuilder(tracer, name)
	if builder == nil {
		t.Fatalf("span %q was not recorded", name)
	}
	if builder.Errors != 1 {
		t.Errorf("expected 1 error span for HTTP 500, got %d", builder.Errors)
	}
}

func TestDefaultTransportAutoInstrumentation(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	if _, ok := http.DefaultTransport.(*tracedRoundTripper); !ok {
		t.Fatal("NewTracer should install instrumentation on http.DefaultTransport")
	}

	// 重复安装不应叠加包装
	first := tracer.InstrumentDefaultTransport()
	second := tracer.InstrumentDefaultTransport()
	if first != second {
		t.Error("InstrumentDefaultTransport should be idempotent")
	}
	if http.DefaultTransport != first {
		t.Error("http.DefaultTransport should be the installed wrapper")
	}

	headerCh := make(chan string, 1)
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		headerCh <- r.Header.Get(TraceparentHeader)
	}))
	defer server.Close()

	// 未包装的普通客户端也会走默认传输自动埋点
	resp, err := (&http.Client{}).Get(server.URL + "/auto")
	if err != nil {
		t.Fatalf("request failed: %v", err)
	}
	resp.Body.Close()

	traceContext, ok := ParseTraceparent(<-headerCh)
	if !ok {
		t.Fatal("default transport should inject traceparent")
	}
	if len(traceContext.TraceID) != 32 {
		t.Errorf("generated trace id length = %d, want 32", len(traceContext.TraceID))
	}

	name := http.MethodGet + " " + strings.TrimPrefix(server.URL, "http://") + "/auto"
	if total := builderTotal(tracer, name); total != 1 {
		t.Errorf("span total = %d, want 1", total)
	}
}

func TestSetAutoInstrumentDisablesDefaultTransport(t *testing.T) {
	tracer := NewTracer("http://127.0.0.1:1", "TestApp", "TestSecret")
	tracer.SetAutoInstrument(false)

	original := http.DefaultTransport
	t.Cleanup(func() {
		tracer.SetAutoInstrument(false)
		http.DefaultTransport = original
		SetDefaultTracer(nil)
	})

	tracer.SetAutoInstrument(true)
	if http.DefaultTransport == original {
		t.Error("SetAutoInstrument(true) should install instrumentation")
	}

	tracer.SetAutoInstrument(false)
	if http.DefaultTransport != original {
		t.Error("SetAutoInstrument(false) should restore original transport")
	}
}

func TestIncomingHandlerTraceContext(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	const traceID = "4bf92f3577b34da6a3ce929d0e0e4736"
	const parentID = "00f067aa0ba902b7"

	var seen *Span
	handler := tracer.WrapHandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		seen = SpanFromContext(r.Context())
		w.WriteHeader(http.StatusCreated)
	})

	req := httptest.NewRequest(http.MethodGet, "/hello?name=go", nil)
	req.Header.Set(TraceparentHeader, "00-"+traceID+"-"+parentID+"-01")
	recorder := httptest.NewRecorder()
	handler(recorder, req)

	if recorder.Code != http.StatusCreated {
		t.Errorf("status = %d, want 201", recorder.Code)
	}
	if seen == nil {
		t.Fatal("handler should receive a span in request context")
	}
	if seen.TraceID != traceID {
		t.Errorf("trace id = %s, want %s", seen.TraceID, traceID)
	}
	if seen.ParentID != parentID {
		t.Errorf("parent id = %s, want %s", seen.ParentID, parentID)
	}

	builder := findBuilder(tracer, "GET /hello")
	if builder == nil {
		t.Fatal("server span GET /hello was not recorded")
	}
	if builder.Total != 1 {
		t.Errorf("span total = %d, want 1", builder.Total)
	}
	if builder.Samples[0].TraceID != traceID {
		t.Errorf("sample trace id = %s, want %s", builder.Samples[0].TraceID, traceID)
	}
	if builder.Samples[0].Tag != "GET /hello?name=go" {
		t.Errorf("span tag = %q, want raw request URI", builder.Samples[0].Tag)
	}
}

func TestIncomingHandlerServerError(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	handler := tracer.WrapHandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.Error(w, "failed", http.StatusInternalServerError)
	})

	req := httptest.NewRequest(http.MethodPost, "/orders", nil)
	recorder := httptest.NewRecorder()
	handler(recorder, req)

	if recorder.Code != http.StatusInternalServerError {
		t.Errorf("status = %d, want 500", recorder.Code)
	}
	builder := findBuilder(tracer, "POST /orders")
	if builder == nil {
		t.Fatal("server span POST /orders was not recorded")
	}
	if builder.Errors != 1 {
		t.Errorf("expected 1 error span for 5xx, got %d", builder.Errors)
	}
}

func TestIncomingHandlerPanic(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	handler := tracer.WrapHandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		panic("boom")
	})

	req := httptest.NewRequest(http.MethodGet, "/panic", nil)
	recorder := httptest.NewRecorder()

	func() {
		defer func() {
			if recovered := recover(); recovered == nil {
				t.Error("panic should be re-thrown after recording")
			}
		}()
		handler(recorder, req)
	}()

	builder := findBuilder(tracer, "GET /panic")
	if builder == nil {
		t.Fatal("server span GET /panic was not recorded")
	}
	if builder.Errors != 1 {
		t.Errorf("expected 1 error span after panic, got %d", builder.Errors)
	}
	if !strings.Contains(builder.ErrorSamples[0].Error, "panic") {
		t.Errorf("error should mention panic, got %q", builder.ErrorSamples[0].Error)
	}
}

func TestIncomingHandlerDeduplicatesNestedWrapping(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	count := 0
	inner := tracer.WrapHandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		count++
	})
	outer := tracer.WrapHandler(inner)

	req := httptest.NewRequest(http.MethodGet, "/nested", nil)
	recorder := httptest.NewRecorder()
	outer.ServeHTTP(recorder, req)

	if count != 1 {
		t.Errorf("handler called %d times, want 1", count)
	}
	if total := builderTotal(tracer, "GET /nested"); total != 1 {
		t.Errorf("span total = %d, want 1 (入口 Span 去重)", total)
	}
}

func TestSelfRequestsAreSuppressed(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]interface{}{
			"code": 0,
			"data": map[string]interface{}{"Token": "token", "Expire": 7200},
		})
	}))
	defer server.Close()

	tracer := NewTracer(server.URL, "TestApp", "TestSecret")
	t.Cleanup(func() {
		tracer.SetAutoInstrument(false)
		SetDefaultTracer(nil)
	})
	tracer.SetHTTPClient(&http.Client{})

	if _, _, err := tracer.post("/App/Login", "application/json", []byte("{}"), ""); err != nil {
		t.Fatalf("tracer post failed: %v", err)
	}

	config := NewConfigClient(server.URL, "TestApp", "TestSecret")
	config.client = &http.Client{}
	config.login()

	tracer.mu.Lock()
	defer tracer.mu.Unlock()
	for name := range tracer.builders {
		if strings.HasPrefix(name, http.MethodPost+" ") {
			t.Errorf("self request should not be traced, got span %q", name)
		}
	}
}

func TestIncomingToOutgoingPropagation(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	headerCh := make(chan string, 1)
	downstream := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		headerCh <- r.Header.Get(TraceparentHeader)
	}))
	defer downstream.Close()

	const traceID = "4bf92f3577b34da6a3ce929d0e0e4736"
	const upstreamSpanID = "00f067aa0ba902b7"

	var serverSpan *Span
	handler := tracer.WrapHandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		serverSpan = SpanFromContext(r.Context())

		req, err := http.NewRequestWithContext(r.Context(), http.MethodGet, downstream.URL+"/child", nil)
		if err != nil {
			t.Errorf("NewRequestWithContext failed: %v", err)
			return
		}
		resp, err := http.DefaultClient.Do(req)
		if err != nil {
			t.Errorf("downstream request failed: %v", err)
			return
		}
		resp.Body.Close()
	})

	req := httptest.NewRequest(http.MethodGet, "/parent", nil)
	req.Header.Set(TraceparentHeader, "00-"+traceID+"-"+upstreamSpanID+"-01")
	handler(httptest.NewRecorder(), req)

	childContext, ok := ParseTraceparent(<-headerCh)
	if !ok {
		t.Fatal("downstream request should carry traceparent")
	}
	if childContext.TraceID != traceID {
		t.Errorf("downstream trace id = %s, want %s", childContext.TraceID, traceID)
	}
	if serverSpan == nil {
		t.Fatal("handler should have a server span")
	}
	if childContext.SpanID == serverSpan.ID {
		t.Error("downstream span should be a child, not the server span itself")
	}

	name := http.MethodGet + " " + strings.TrimPrefix(downstream.URL, "http://") + "/child"
	builder := findBuilder(tracer, name)
	if builder == nil {
		t.Fatalf("downstream span %q was not recorded", name)
	}
	if builder.Samples[0].TraceID != traceID {
		t.Errorf("downstream span trace = %s, want %s", builder.Samples[0].TraceID, traceID)
	}
	if builder.Samples[0].ParentID != serverSpan.ID {
		t.Errorf("downstream span parent = %s, want server span %s", builder.Samples[0].ParentID, serverSpan.ID)
	}
}

func TestWrapClientIdempotent(t *testing.T) {
	tracer := newInstrumentTestTracer(t)

	client := tracer.WrapClient(&http.Client{})
	transport := client.Transport
	tracer.WrapClient(client)
	if client.Transport != transport {
		t.Error("WrapClient should not double-wrap an instrumented client")
	}
}

func TestInstrumentationNilSafety(t *testing.T) {
	var nilTracer *Tracer

	client := nilTracer.WrapClient(nil)
	if client == nil || client.Transport == nil {
		t.Fatal("nil tracer/client should still return a usable client")
	}
	if nilTracer.WrapRoundTripper(nil) == nil {
		t.Fatal("WrapRoundTripper(nil) should not return nil")
	}
	if nilTracer.NewClient() == nil {
		t.Fatal("NewClient should not return nil")
	}

	handler := WrapHandler(nil)
	if handler == nil {
		t.Fatal("WrapHandler(nil) should return a handler")
	}
	recorder := httptest.NewRecorder()
	handler.ServeHTTP(recorder, httptest.NewRequest(http.MethodGet, "/", nil))
	if recorder.Code != http.StatusNotFound {
		t.Errorf("nil handler should produce 404, got %d", recorder.Code)
	}

	handlerFunc := WrapHandlerFunc(nil)
	if handlerFunc == nil {
		t.Fatal("WrapHandlerFunc(nil) should return a handler func")
	}
	recorder = httptest.NewRecorder()
	handlerFunc(recorder, httptest.NewRequest(http.MethodGet, "/", nil))
	if recorder.Code != http.StatusNotFound {
		t.Errorf("nil handler func should produce 404, got %d", recorder.Code)
	}
}
