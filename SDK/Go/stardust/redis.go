package stardust

import (
	"context"
	"fmt"
	"strings"
)

// TraceRedis 为通用 Redis 客户端创建 redis:{COMMAND} 片段。
// 回调收到的上下文已携带当前片段，可用于嵌套埋点；回调返回错误时记为错误片段。
//
// 示例：
//
//	err := stardust.TraceRedis(ctx, "GET", func(ctx context.Context) error {
//	    value, err = client.Get(ctx, "key").Result()
//	    return err
//	})
func TraceRedis(ctx context.Context, command string, fn func(context.Context) error) error {
	return TraceRedisWithTag(ctx, command, "", fn)
}

// TraceRedisWithTag 为 Redis 命令创建片段，tag 通常为 key 或参数摘要（上报时按 MaxTagLength 截断）。
func TraceRedisWithTag(ctx context.Context, command, tag string, fn func(context.Context) error) error {
	return DefaultTracer().TraceRedisWithTag(ctx, command, tag, fn)
}

// TraceRedis 使用当前追踪器为 Redis 命令创建片段。
func (t *Tracer) TraceRedis(ctx context.Context, command string, fn func(context.Context) error) error {
	return t.TraceRedisWithTag(ctx, command, "", fn)
}

// TraceRedisWithTag 使用当前追踪器为 Redis 命令创建片段。
func (t *Tracer) TraceRedisWithTag(ctx context.Context, command, tag string, fn func(context.Context) error) error {
	if fn == nil {
		return nil
	}

	ctx, span := newInstrumentSpan(ctx, t, redisSpanName(command))
	if span != nil {
		span.Tag = tag
	}

	defer func() {
		if r := recover(); r != nil {
			span.SetError(fmt.Errorf("panic: %v", r))
			span.Finish()
			panic(r)
		}
	}()

	err := fn(ctx)
	finishInstrumentSpan(span, err)
	return err
}

// redisSpanName 生成 redis:{COMMAND} 片段名称，取命令首词并转大写
func redisSpanName(command string) string {
	name := strings.TrimSpace(command)
	if name == "" {
		return "redis:UNKNOWN"
	}
	if index := strings.IndexAny(name, " \t\r\n"); index > 0 {
		name = name[:index]
	}
	return "redis:" + strings.ToUpper(name)
}
