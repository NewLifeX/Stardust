package stardustredis

import (
	"context"
	"errors"
	"net"
	"testing"

	"github.com/NewLifeX/Stardust/SDK/Go/stardust"
	"github.com/redis/go-redis/v9"
)

var _ redis.Hook = (*Hook)(nil)

func newTestHook(t *testing.T) (*Hook, *stardust.Tracer) {
	t.Helper()

	tracer := stardust.NewTracer("http://127.0.0.1:1", "TestApp", "TestSecret")
	t.Cleanup(func() {
		tracer.SetAutoInstrument(false)
		stardust.SetDefaultTracer(nil)
	})
	return NewHook(tracer), tracer
}

func TestProcessHookCreatesSpan(t *testing.T) {
	hook, tracer := newTestHook(t)

	parent := tracer.NewSpan("parent", "")
	ctx := stardust.ContextWithSpan(context.Background(), parent)

	called := false
	next := func(ctx context.Context, cmd redis.Cmder) error {
		called = true
		if stardust.SpanFromContext(ctx) == nil {
			t.Error("command context should carry a redis span")
		}
		if result, ok := cmd.(*redis.Cmd); ok {
			result.SetVal("value")
		}
		return nil
	}

	cmd := redis.NewCmd(context.Background(), "get", "user:1")
	if err := hook.ProcessHook(next)(ctx, cmd); err != nil {
		t.Fatalf("ProcessHook returned error: %v", err)
	}
	if !called {
		t.Fatal("next hook was not called")
	}
	if cmd.Val() != "value" {
		t.Errorf("command value = %q, want value", cmd.Val())
	}
}

func TestProcessHookPropagatesError(t *testing.T) {
	hook, _ := newTestHook(t)

	expected := errors.New("redis unavailable")
	next := func(ctx context.Context, cmd redis.Cmder) error {
		return expected
	}

	cmd := redis.NewCmd(context.Background(), "set", "key", "value")
	if err := hook.ProcessHook(next)(context.Background(), cmd); err != expected {
		t.Errorf("error = %v, want %v", err, expected)
	}
}

func TestProcessHookNilCommand(t *testing.T) {
	hook, _ := newTestHook(t)

	called := false
	next := func(ctx context.Context, cmd redis.Cmder) error {
		called = true
		if cmd != nil {
			t.Error("expected nil command")
		}
		return nil
	}

	if err := hook.ProcessHook(next)(context.Background(), nil); err != nil {
		t.Fatalf("ProcessHook(nil) returned error: %v", err)
	}
	if !called {
		t.Fatal("next hook was not called")
	}
}

func TestProcessPipelineHook(t *testing.T) {
	hook, tracer := newTestHook(t)

	parent := tracer.NewSpan("parent", "")
	ctx := stardust.ContextWithSpan(context.Background(), parent)

	cmds := []redis.Cmder{
		redis.NewCmd(context.Background(), "get", "a"),
		redis.NewCmd(context.Background(), "set", "b", "1"),
	}

	called := false
	next := func(ctx context.Context, got []redis.Cmder) error {
		called = true
		if len(got) != len(cmds) {
			t.Errorf("pipeline length = %d, want %d", len(got), len(cmds))
		}
		if stardust.SpanFromContext(ctx) == nil {
			t.Error("pipeline context should carry a redis span")
		}
		return nil
	}

	if err := hook.ProcessPipelineHook(next)(ctx, cmds); err != nil {
		t.Fatalf("ProcessPipelineHook returned error: %v", err)
	}
	if !called {
		t.Fatal("next pipeline hook was not called")
	}
	if tag := pipelineTag(cmds); tag != "GET,SET" {
		t.Errorf("pipeline tag = %q, want GET,SET", tag)
	}

	emptyCalled := false
	emptyNext := func(ctx context.Context, got []redis.Cmder) error {
		emptyCalled = true
		if len(got) != 0 {
			t.Errorf("empty pipeline length = %d, want 0", len(got))
		}
		return nil
	}
	if err := hook.ProcessPipelineHook(emptyNext)(ctx, nil); err != nil {
		t.Fatalf("empty pipeline returned error: %v", err)
	}
	if !emptyCalled {
		t.Fatal("empty pipeline next hook was not called")
	}
}

func TestDialHookPassthrough(t *testing.T) {
	hook, _ := newTestHook(t)

	client, server := net.Pipe()
	defer client.Close()
	defer server.Close()

	called := false
	next := func(ctx context.Context, network, addr string) (net.Conn, error) {
		called = true
		if network != "tcp" || addr != "127.0.0.1:6379" {
			t.Errorf("unexpected dial target: %s %s", network, addr)
		}
		return client, nil
	}

	conn, err := hook.DialHook(next)(context.Background(), "tcp", "127.0.0.1:6379")
	if err != nil {
		t.Fatalf("DialHook returned error: %v", err)
	}
	if !called || conn != client {
		t.Error("DialHook should pass through to next hook")
	}

	if hook.DialHook(nil) != nil {
		t.Error("DialHook(nil) should return nil")
	}
}

func TestNilHookUsesDefaultTracer(t *testing.T) {
	var hook *Hook

	called := false
	next := func(ctx context.Context, cmd redis.Cmder) error {
		called = true
		return nil
	}

	cmd := redis.NewCmd(context.Background(), "ping")
	if err := hook.ProcessHook(next)(context.Background(), cmd); err != nil {
		t.Fatalf("nil hook returned error: %v", err)
	}
	if !called {
		t.Fatal("next hook was not called")
	}
}

func TestCommandTag(t *testing.T) {
	cmd := redis.NewCmd(context.Background(), "hset", "user:1", "name", "alice", "age", "18")
	tag := commandTag(cmd)
	if tag != "user:1 name alice age" {
		t.Errorf("commandTag = %q, want first 4 args", tag)
	}

	empty := redis.NewCmd(context.Background(), "ping")
	if commandTag(empty) != "" {
		t.Errorf("commandTag(ping) = %q, want empty", commandTag(empty))
	}
}
