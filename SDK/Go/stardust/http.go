package stardust

import (
	"bufio"
	"fmt"
	"net"
	"net/http"
	"sync"
	"time"
)

var (
	instrumentMu             sync.Mutex
	instrumentedDefault      *tracedRoundTripper
	originalDefaultTransport http.RoundTripper
)

// tracedRoundTripper 出站 HTTP 自动埋点。
// next 为底层传输，tracer 为上报使用的追踪器；全局默认传输安装后可通过 setTracer 切换。
type tracedRoundTripper struct {
	next http.RoundTripper

	tracerMu sync.RWMutex
	tracer   *Tracer
}

func (rt *tracedRoundTripper) setTracer(tracer *Tracer) {
	rt.tracerMu.Lock()
	rt.tracer = tracer
	rt.tracerMu.Unlock()
}

func (rt *tracedRoundTripper) getTracer() *Tracer {
	rt.tracerMu.RLock()
	defer rt.tracerMu.RUnlock()
	return rt.tracer
}

// RoundTrip 为出站请求创建客户端片段并注入 traceparent。
// 请求上下文已存在片段时作为其子片段，否则开启新链路。
func (rt *tracedRoundTripper) RoundTrip(req *http.Request) (*http.Response, error) {
	next := rt.next
	if next == nil {
		next = baseTransport()
	}
	if req == nil {
		return next.RoundTrip(req)
	}
	if isSuppressed(req.Context()) {
		return next.RoundTrip(req)
	}

	span := newClientSpan(req, rt.getTracer())
	if span == nil {
		return next.RoundTrip(req)
	}

	defer func() {
		if r := recover(); r != nil {
			span.SetError(fmt.Errorf("panic: %v", r))
			span.Finish()
			panic(r)
		}
		span.Finish()
	}()

	span.Tag = requestURL(req)

	outReq := req.Clone(ContextWithSpan(req.Context(), span))
	if outReq.Header == nil {
		outReq.Header = make(http.Header)
	}
	// 调用方已显式设置 traceparent 时不覆盖
	if outReq.Header.Get(TraceparentHeader) == "" {
		outReq.Header.Set(TraceparentHeader, FormatTraceparent(&TraceContext{
			TraceID: span.TraceID,
			SpanID:  span.ID,
			Sampled: true,
		}))
	}

	resp, err := next.RoundTrip(outReq)
	if err != nil {
		span.SetError(err)
		return nil, err
	}
	if resp != nil && resp.StatusCode >= 400 {
		span.SetError(fmt.Errorf("HTTP %d", resp.StatusCode))
	}
	return resp, err
}

// newClientSpan 创建出站片段，返回 nil 表示无需埋点
func newClientSpan(req *http.Request, tracer *Tracer) *Span {
	if parent := SpanFromContext(req.Context()); parent != nil {
		return NewChildSpan(parent, clientSpanName(req))
	}
	if tracer == nil {
		tracer = DefaultTracer()
	}
	if tracer == nil {
		return nil
	}
	return tracer.NewSpan(clientSpanName(req), "")
}

func clientSpanName(req *http.Request) string {
	path := "/"
	host := ""
	if req.URL != nil {
		host = req.URL.Host
		if req.URL.Path != "" {
			path = req.URL.Path
		}
	}
	return req.Method + " " + host + path
}

func requestURL(req *http.Request) string {
	if req.URL == nil {
		return ""
	}
	return req.URL.String()
}

// WrapRoundTripper 包装出站传输，为使用该传输的请求创建客户端片段并注入 traceparent。
// next 为 nil 时使用当前默认传输；重复包装或传入已包装的传输时原样返回。
func (t *Tracer) WrapRoundTripper(next http.RoundTripper) http.RoundTripper {
	return wrapRoundTripper(next, t)
}

// WrapRoundTripper 使用默认追踪器包装出站传输。
func WrapRoundTripper(next http.RoundTripper) http.RoundTripper {
	return wrapRoundTripper(next, nil)
}

func wrapRoundTripper(next http.RoundTripper, tracer *Tracer) http.RoundTripper {
	if next == nil {
		next = baseTransport()
	}
	switch next.(type) {
	case *tracedRoundTripper:
		return next
	}
	return &tracedRoundTripper{next: next, tracer: tracer}
}

// WrapClient 包装 HTTP 客户端，为其请求创建出站片段并注入 traceparent。
// client 为 nil 时新建客户端；重复包装时原样返回。
func (t *Tracer) WrapClient(client *http.Client) *http.Client {
	if client == nil {
		client = &http.Client{}
	}
	client.Transport = t.WrapRoundTripper(client.Transport)
	return client
}

// WrapClient 使用默认追踪器包装 HTTP 客户端。
func WrapClient(client *http.Client) *http.Client {
	return DefaultTracer().WrapClient(client)
}

// NewClient 创建已开启自动埋点的 HTTP 客户端。
func (t *Tracer) NewClient() *http.Client {
	return &http.Client{Transport: wrapRoundTripper(nil, t)}
}

// NewClient 使用默认追踪器创建已开启自动埋点的 HTTP 客户端。
func NewClient() *http.Client {
	return DefaultTracer().NewClient()
}

// InstrumentDefaultTransport 将出站自动埋点安装到 http.DefaultTransport，返回包装后的传输。
// 已安装时仅切换追踪器，不会重复包装。
func (t *Tracer) InstrumentDefaultTransport() http.RoundTripper {
	instrumentMu.Lock()
	defer instrumentMu.Unlock()

	if current, ok := http.DefaultTransport.(*tracedRoundTripper); ok {
		current.setTracer(t)
		instrumentedDefault = current
		return current
	}

	originalDefaultTransport = http.DefaultTransport
	next := http.DefaultTransport
	if next == nil {
		next = &http.Transport{}
	}
	wrapped := &tracedRoundTripper{next: next, tracer: t}
	instrumentedDefault = wrapped
	http.DefaultTransport = wrapped
	return wrapped
}

// InstrumentDefaultTransport 使用指定追踪器安装默认传输自动埋点。
func InstrumentDefaultTransport(tracer *Tracer) http.RoundTripper {
	if tracer == nil {
		tracer = DefaultTracer()
	}
	return tracer.InstrumentDefaultTransport()
}

// UninstrumentDefaultTransport 卸载默认传输自动埋点。仅当当前安装由该追踪器完成时生效。
func (t *Tracer) UninstrumentDefaultTransport() {
	instrumentMu.Lock()
	defer instrumentMu.Unlock()

	if instrumentedDefault == nil || instrumentedDefault.getTracer() != t {
		return
	}
	if http.DefaultTransport == instrumentedDefault {
		http.DefaultTransport = originalDefaultTransport
	}
	instrumentedDefault = nil
}

// SetAutoInstrument 开启或关闭默认 HTTP 传输自动埋点，返回自身以支持链式调用。
// NewTracer 默认已安装，传入 false 可卸载；传入 true 可重新安装。
func (t *Tracer) SetAutoInstrument(enable bool) *Tracer {
	if enable {
		t.InstrumentDefaultTransport()
	} else {
		t.UninstrumentDefaultTransport()
	}
	return t
}

// baseTransport 返回未包装的默认传输，避免重复包装造成递归
func baseTransport() http.RoundTripper {
	instrumentMu.Lock()
	defer instrumentMu.Unlock()

	if instrumentedDefault != nil && http.DefaultTransport == instrumentedDefault {
		if instrumentedDefault.next != nil {
			return instrumentedDefault.next
		}
		return &http.Transport{}
	}
	if transport := http.DefaultTransport; transport != nil {
		if wrapped, ok := transport.(*tracedRoundTripper); ok {
			if wrapped.next != nil {
				return wrapped.next
			}
			return &http.Transport{}
		}
		return transport
	}
	return &http.Transport{}
}

// tracedHandler 入站 HTTP 自动埋点
type tracedHandler struct {
	next   http.Handler
	tracer *Tracer
}

// ServeHTTP 解析上游 traceparent 创建入口片段，并记录状态码与 panic
func (h *tracedHandler) ServeHTTP(w http.ResponseWriter, req *http.Request) {
	if req == nil || hasServerSpan(req.Context()) {
		h.next.ServeHTTP(w, req)
		return
	}

	span := newServerSpan(req, h.tracer)
	span.Tag = req.Method + " " + req.RequestURI
	ctx := markServerSpan(ContextWithSpan(req.Context(), span))
	req = req.WithContext(ctx)

	recorder := &statusRecorder{ResponseWriter: w, status: http.StatusOK}
	defer func() {
		if r := recover(); r != nil {
			if r != http.ErrAbortHandler {
				span.SetError(fmt.Errorf("panic: %v", r))
			}
			span.Finish()
			panic(r)
		}
		if recorder.status >= 500 {
			span.SetError(fmt.Errorf("HTTP %d", recorder.status))
		}
		span.Finish()
	}()

	h.next.ServeHTTP(recorder, req)
}

// newServerSpan 创建入口片段。读取上游 traceparent 复用 TraceId 与 ParentId。
func newServerSpan(req *http.Request, tracer *Tracer) *Span {
	name := serverSpanName(req)

	var traceID, parentID string
	if context, ok := ParseTraceparent(req.Header.Get(TraceparentHeader)); ok {
		traceID = context.TraceID
		parentID = context.SpanID
	}

	if tracer == nil {
		tracer = DefaultTracer()
	}
	if tracer == nil {
		if traceID == "" {
			traceID = NewTraceID()
		}
		return &Span{
			ID:        NewSpanID(),
			ParentID:  parentID,
			TraceID:   traceID,
			StartTime: time.Now().UnixMilli(),
			name:      name,
		}
	}

	if traceID == "" {
		return tracer.NewSpan(name, "")
	}

	span := tracer.NewSpan(name, parentID)
	span.TraceID = traceID
	return span
}

func serverSpanName(req *http.Request) string {
	path := "/"
	if req.URL != nil && req.URL.Path != "" {
		path = req.URL.Path
	}
	return req.Method + " " + path
}

// WrapHandler 包装 HTTP 处理器，为每个请求创建入口片段。
// handler 为 nil 时使用 404 处理器；重复包装时原样返回。
func (t *Tracer) WrapHandler(handler http.Handler) http.Handler {
	return wrapHandler(handler, t)
}

// WrapHandler 使用默认追踪器包装 HTTP 处理器。
func WrapHandler(handler http.Handler) http.Handler {
	return wrapHandler(handler, nil)
}

// WrapHandlerFunc 包装 HTTP 处理函数。handler 为 nil 时使用 404 处理器。
func (t *Tracer) WrapHandlerFunc(handler http.HandlerFunc) http.HandlerFunc {
	if handler == nil {
		return http.NotFoundHandler().ServeHTTP
	}
	return t.WrapHandler(handler).ServeHTTP
}

// WrapHandlerFunc 使用默认追踪器包装 HTTP 处理函数。handler 为 nil 时使用 404 处理器。
func WrapHandlerFunc(handler http.HandlerFunc) http.HandlerFunc {
	if handler == nil {
		return http.NotFoundHandler().ServeHTTP
	}
	return WrapHandler(handler).ServeHTTP
}

func wrapHandler(handler http.Handler, tracer *Tracer) http.Handler {
	if handler == nil {
		handler = http.NotFoundHandler()
	}
	if _, ok := handler.(*tracedHandler); ok {
		return handler
	}
	return &tracedHandler{next: handler, tracer: tracer}
}

// statusRecorder 记录响应状态码，同时转发可选的 HTTP 接口
type statusRecorder struct {
	http.ResponseWriter
	status      int
	wroteHeader bool
}

// WriteHeader 记录状态码后转发
func (w *statusRecorder) WriteHeader(code int) {
	if !w.wroteHeader {
		w.status = code
		w.wroteHeader = true
	}
	w.ResponseWriter.WriteHeader(code)
}

// Write 首次写入时视为 200 状态码
func (w *statusRecorder) Write(data []byte) (int, error) {
	if !w.wroteHeader {
		w.status = http.StatusOK
		w.wroteHeader = true
	}
	return w.ResponseWriter.Write(data)
}

// Flush 转发 Flush
func (w *statusRecorder) Flush() {
	if flusher, ok := w.ResponseWriter.(http.Flusher); ok {
		flusher.Flush()
	}
}

// Hijack 转发 Hijack
func (w *statusRecorder) Hijack() (net.Conn, *bufio.ReadWriter, error) {
	if hijacker, ok := w.ResponseWriter.(http.Hijacker); ok {
		return hijacker.Hijack()
	}
	return nil, nil, http.ErrNotSupported
}

// Push 转发 HTTP/2 推送
func (w *statusRecorder) Push(target string, opts *http.PushOptions) error {
	if pusher, ok := w.ResponseWriter.(http.Pusher); ok {
		return pusher.Push(target, opts)
	}
	return http.ErrNotSupported
}
