// Package stardustredis 提供 go-redis v9 的星尘埋点钩子。
//
// 该模块独立于核心 stardust 包，核心 SDK 仍然保持零第三方依赖。
package stardustredis

import (
	"context"
	"fmt"
	"net"
	"strings"

	"github.com/NewLifeX/Stardust/SDK/Go/stardust"
	"github.com/redis/go-redis/v9"
)

const (
	// pipelineCommand 管道命令的片段名称
	pipelineCommand = "PIPELINE"
	// maxTagArgs tag 中最多保留的参数个数
	maxTagArgs = 4
)

// Hook go-redis v9 埋点钩子，为普通命令与管道生成 redis:{COMMAND} 片段
type Hook struct {
	// Tracer 上报追踪器；为空时使用 stardust.DefaultTracer()
	Tracer *stardust.Tracer
}

// NewHook 创建埋点钩子
func NewHook(tracer *stardust.Tracer) *Hook {
	return &Hook{Tracer: tracer}
}

// DialHook 连接建立不做埋点，直接转发
func (h *Hook) DialHook(next redis.DialHook) redis.DialHook {
	if next == nil {
		return nil
	}
	return func(ctx context.Context, network, addr string) (net.Conn, error) {
		return next(ctx, network, addr)
	}
}

// ProcessHook 为单条命令创建片段
func (h *Hook) ProcessHook(next redis.ProcessHook) redis.ProcessHook {
	if next == nil {
		return nil
	}
	return func(ctx context.Context, cmd redis.Cmder) error {
		if cmd == nil {
			return next(ctx, cmd)
		}
		return h.trace(ctx, cmd.Name(), commandTag(cmd), func(ctx context.Context) error {
			return next(ctx, cmd)
		})
	}
}

// ProcessPipelineHook 为管道创建 redis:PIPELINE 片段，tag 为命令摘要
func (h *Hook) ProcessPipelineHook(next redis.ProcessPipelineHook) redis.ProcessPipelineHook {
	if next == nil {
		return nil
	}
	return func(ctx context.Context, cmds []redis.Cmder) error {
		if len(cmds) == 0 {
			return next(ctx, cmds)
		}
		return h.trace(ctx, pipelineCommand, pipelineTag(cmds), func(ctx context.Context) error {
			return next(ctx, cmds)
		})
	}
}

func (h *Hook) trace(ctx context.Context, command, tag string, fn func(context.Context) error) error {
	if h != nil && h.Tracer != nil {
		return h.Tracer.TraceRedisWithTag(ctx, command, tag, fn)
	}
	return stardust.TraceRedisWithTag(ctx, command, tag, fn)
}

// commandTag 生成命令参数摘要，最多保留 maxTagArgs 个参数
func commandTag(cmd redis.Cmder) string {
	args := cmd.Args()
	if len(args) <= 1 {
		return ""
	}

	parts := make([]string, 0, maxTagArgs)
	for _, arg := range args[1:] {
		parts = append(parts, fmt.Sprint(arg))
		if len(parts) >= maxTagArgs {
			break
		}
	}
	return strings.Join(parts, " ")
}

func pipelineTag(cmds []redis.Cmder) string {
	names := make([]string, 0, maxTagArgs)
	for _, cmd := range cmds {
		if cmd == nil {
			continue
		}
		names = append(names, strings.ToUpper(cmd.Name()))
		if len(names) >= maxTagArgs {
			break
		}
	}
	if len(names) == 0 {
		return ""
	}
	return strings.Join(names, ",")
}
