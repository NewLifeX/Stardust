package stardust

import (
	"context"
	"sync"
	"time"
)

type spanContextKey struct{}

type suppressContextKey struct{}

type serverSpanContextKey struct{}

var (
	defaultTracerMu sync.RWMutex
	defaultTracer   *Tracer
)

// SetDefaultTracer 设置默认追踪器。HTTP 自动埋点、TraceRedis 以及包级上下文辅助函数
// 在没有显式追踪器或上下文片段时使用该实例；NewTracer 会自动将其设为默认追踪器。
func SetDefaultTracer(tracer *Tracer) {
	defaultTracerMu.Lock()
	defaultTracer = tracer
	defaultTracerMu.Unlock()
}

// DefaultTracer 返回当前默认追踪器，未设置时返回 nil。
func DefaultTracer() *Tracer {
	defaultTracerMu.RLock()
	defer defaultTracerMu.RUnlock()
	return defaultTracer
}

// ContextWithSpan 把片段写入上下文；span 为 nil 或 ctx 为 nil 时返回可用上下文。
func ContextWithSpan(ctx context.Context, span *Span) context.Context {
	if ctx == nil {
		ctx = context.Background()
	}
	if span == nil {
		return ctx
	}
	return context.WithValue(ctx, spanContextKey{}, span)
}

// SpanFromContext 读取上下文中的当前片段，不存在时返回 nil。
func SpanFromContext(ctx context.Context) *Span {
	if ctx == nil {
		return nil
	}
	span, _ := ctx.Value(spanContextKey{}).(*Span)
	return span
}

// NewSpanFromContext 基于上下文创建片段并写回上下文。
// 上下文已有片段时创建其子片段并复用 TraceId，否则使用默认追踪器开启新链路。
// 未设置默认追踪器时返回一个仅用于上下文传播的片段（不会上报）。
func NewSpanFromContext(ctx context.Context, name string) (context.Context, *Span) {
	return DefaultTracer().NewSpanFromContext(ctx, name)
}

// NewSpanFromContext 基于上下文创建片段并写回上下文。
// 上下文已有片段时创建其子片段并复用 TraceId，否则使用当前追踪器开启新链路。
func (t *Tracer) NewSpanFromContext(ctx context.Context, name string) (context.Context, *Span) {
	if ctx == nil {
		ctx = context.Background()
	}

	if parent := SpanFromContext(ctx); parent != nil {
		span := NewChildSpan(parent, name)
		return ContextWithSpan(ctx, span), span
	}

	if t == nil {
		t = DefaultTracer()
	}
	span := t.NewSpan(name, "")
	return ContextWithSpan(ctx, span), span
}

// NewChildSpan 创建父片段的子片段，复用父片段的 TraceId 与追踪器。
// parent 为 nil 时返回 nil。
func NewChildSpan(parent *Span, name string) *Span {
	if parent == nil {
		return nil
	}

	traceID := parent.TraceID
	if traceID == "" {
		traceID = NewTraceID()
	}

	return &Span{
		ID:        NewSpanID(),
		ParentID:  parent.ID,
		TraceID:   traceID,
		StartTime: time.Now().UnixMilli(),
		name:      name,
		tracer:    parent.tracer,
	}
}

// withSuppress 标记上下文为 SDK 自身请求，自动埋点将跳过该上下文。
func withSuppress(ctx context.Context) context.Context {
	if ctx == nil {
		ctx = context.Background()
	}
	return context.WithValue(ctx, suppressContextKey{}, true)
}

// isSuppressed 判断上下文是否被标记为 SDK 自身请求
func isSuppressed(ctx context.Context) bool {
	if ctx == nil {
		return false
	}
	value, _ := ctx.Value(suppressContextKey{}).(bool)
	return value
}

// markServerSpan 标记上下文已存在入口片段，避免重复创建入口 Span
func markServerSpan(ctx context.Context) context.Context {
	if ctx == nil {
		ctx = context.Background()
	}
	return context.WithValue(ctx, serverSpanContextKey{}, true)
}

// hasServerSpan 判断上下文是否已由 WrapHandler 创建入口片段
func hasServerSpan(ctx context.Context) bool {
	if ctx == nil {
		return false
	}
	value, _ := ctx.Value(serverSpanContextKey{}).(bool)
	return value
}

// newInstrumentSpan 为自动埋点创建片段：优先复用上下文片段作为父片段，否则新建链路。
// tracer 为 nil 时回退默认追踪器；两者都没有时返回仅用于传播的片段。
func newInstrumentSpan(ctx context.Context, tracer *Tracer, name string) (context.Context, *Span) {
	if ctx == nil {
		ctx = context.Background()
	}

	if parent := SpanFromContext(ctx); parent != nil {
		span := NewChildSpan(parent, name)
		return ContextWithSpan(ctx, span), span
	}

	if tracer == nil {
		tracer = DefaultTracer()
	}
	span := tracer.NewSpan(name, "")
	return ContextWithSpan(ctx, span), span
}

// finishInstrumentSpan 结束自动埋点片段并记录错误；span 为 nil 时安全忽略。
func finishInstrumentSpan(span *Span, err error) {
	if span == nil {
		return
	}
	if err != nil {
		span.SetError(err)
	}
	span.Finish()
}
