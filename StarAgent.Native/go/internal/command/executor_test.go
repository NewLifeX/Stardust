package command

import (
	"context"
	"errors"
	"runtime"
	"strings"
	"testing"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

func testShell() string {
	if runtime.GOOS == "windows" {
		return "cmd"
	}

	return "bash"
}

func collectReply(t *testing.T, run func(reply func(protocol.CommandReplyModel))) []protocol.CommandReplyModel {
	t.Helper()

	replies := make(chan protocol.CommandReplyModel, 8)
	run(func(reply protocol.CommandReplyModel) { replies <- reply })

	var result []protocol.CommandReplyModel
	timeout := time.After(3 * time.Second)
	for {
		select {
		case reply := <-replies:
			result = append(result, reply)
		case <-time.After(100 * time.Millisecond):
			return result
		case <-timeout:
			t.Fatal("timed out waiting for replies")
		}
	}
}

func TestAllowExecDisabled(t *testing.T) {
	executor := New(Options{AllowExec: false})
	cmd := protocol.CommandModel{Id: 1, Command: testShell(), Argument: "echo hi"}

	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), cmd, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCanceled || !strings.Contains(replies[0].Data, "未启用") {
		t.Fatalf("unexpected replies: %+v", replies)
	}
}

func TestWhitelist(t *testing.T) {
	executor := New(Options{AllowExec: true, AllowedCommands: []string{"node/synctime"}})

	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: testShell(), Argument: "echo hi"}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCanceled || !strings.Contains(replies[0].Data, "白名单") {
		t.Fatalf("unexpected whitelist reply: %+v", replies)
	}

	replies = collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 2, Command: "node/synctime"}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCompleted {
		t.Fatalf("unexpected synctime reply: %+v", replies)
	}
}

func TestWhitelistWildcard(t *testing.T) {
	executor := New(Options{AllowedCommands: []string{"node/*"}})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: "node/setchannel", Argument: "Beta"}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusError {
		// 白名单通过，但没有配置 SetChannel，因此返回错误而不是取消。
		t.Fatalf("unexpected reply: %+v", replies)
	}
	if strings.Contains(replies[0].Data, "白名单") {
		t.Fatalf("wildcard should allow command: %+v", replies)
	}
}

func TestDeduplication(t *testing.T) {
	executor := New(Options{})
	cmd := protocol.CommandModel{Id: 99, Command: "node/synctime"}

	first := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), cmd, reply)
	})
	if len(first) != 1 {
		t.Fatalf("expected first reply, got %+v", first)
	}

	second := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), cmd, reply)
	})
	if len(second) != 0 {
		t.Fatalf("duplicate command must not reply: %+v", second)
	}
}

func TestExpiredCommand(t *testing.T) {
	executor := New(Options{})
	cmd := protocol.CommandModel{
		Id:      1,
		Command: "node/synctime",
		Expire:  protocol.NewTime(time.Now().Add(-time.Minute)),
	}

	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), cmd, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCanceled || !strings.Contains(replies[0].Data, "过期") {
		t.Fatalf("unexpected replies: %+v", replies)
	}
}

func TestDelayedCommand(t *testing.T) {
	executor := New(Options{})
	cmd := protocol.CommandModel{
		Id:        1,
		Command:   "node/synctime",
		StartTime: protocol.NewTime(time.Now().Add(50 * time.Millisecond)),
	}

	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), cmd, reply)
	})
	if len(replies) != 2 {
		t.Fatalf("expected processing and completion replies, got %+v", replies)
	}
	if replies[0].Status != protocol.StatusProcessing {
		t.Fatalf("first reply = %+v", replies[0])
	}
	if replies[1].Status != protocol.StatusCompleted || replies[1].Data != "无需同步" {
		t.Fatalf("second reply = %+v", replies[1])
	}
}

func TestUnknownAndUnsupportedCommands(t *testing.T) {
	executor := New(Options{})
	for _, name := range []string{"does-not-exist", "node/reboot", "deploy/app", "framework/install"} {
		replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
			executor.Handle(context.Background(), protocol.CommandModel{Id: int64(len(name)), Command: name}, reply)
		})
		if len(replies) != 1 || replies[0].Status != protocol.StatusError || !strings.Contains(replies[0].Data, "不支持命令") {
			t.Fatalf("%s => %+v", name, replies)
		}
	}
}

func TestSetChannel(t *testing.T) {
	var channel string
	executor := New(Options{SetChannel: func(value string) error {
		channel = value
		return nil
	}})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: "node/setchannel", Argument: "Beta"}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCompleted || channel != "Beta" {
		t.Fatalf("replies=%+v channel=%q", replies, channel)
	}

	replies = collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 2, Command: "node/setchannel", Argument: "bad channel!"}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusError {
		t.Fatalf("invalid channel accepted: %+v", replies)
	}
}

func TestShellExecution(t *testing.T) {
	script := "echo hello-agent"
	executor := New(Options{AllowExec: true})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: testShell(), Argument: script}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCompleted {
		t.Fatalf("unexpected replies: %+v", replies)
	}
	if !strings.Contains(replies[0].Data, "hello-agent") {
		t.Fatalf("unexpected output: %q", replies[0].Data)
	}
}

func TestShellTimeout(t *testing.T) {
	executor := New(Options{
		AllowExec: true,
		RunShell: func(ctx context.Context, shell, script string, timeout time.Duration) (string, int, bool, error) {
			<-ctx.Done()
			return "partial output", -1, false, ctx.Err()
		},
	})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{
			Id:       1,
			Command:  testShell(),
			Argument: `{"cmd":"slow","timeout":50}`,
		}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusError || !strings.Contains(replies[0].Data, "超时") {
		t.Fatalf("unexpected timeout reply: %+v", replies)
	}
	if !strings.Contains(replies[0].Data, "partial output") {
		t.Fatalf("partial output lost: %q", replies[0].Data)
	}
}

func TestShellArgumentTooLong(t *testing.T) {
	executor := New(Options{AllowExec: true})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{
			Id:       1,
			Command:  testShell(),
			Argument: strings.Repeat("x", maxArgumentSize+1),
		}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusError || !strings.Contains(replies[0].Data, "过长") {
		t.Fatalf("unexpected replies: %+v", replies)
	}
}

func TestShellJSONArgument(t *testing.T) {
	var gotScript string
	executor := New(Options{
		AllowExec: true,
		RunShell: func(ctx context.Context, shell, script string, timeout time.Duration) (string, int, bool, error) {
			gotScript = script
			return "ok", 0, false, nil
		},
	})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{
			Id:       1,
			Command:  testShell(),
			Argument: `{"cmd":"uptime","timeout":5000}`,
		}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCompleted || gotScript != "uptime" {
		t.Fatalf("replies=%+v script=%q", replies, gotScript)
	}
}

func TestTruncateOutput(t *testing.T) {
	input := strings.Repeat("a", 40000) + "MIDDLE" + strings.Repeat("b", 40000)
	got := TruncateOutput(input)
	if len(got) > MaxOutputBytes {
		t.Fatalf("truncated length = %d", len(got))
	}
	if !strings.Contains(got, "a") || !strings.Contains(got, "b") {
		t.Fatal("expected head and tail to be preserved")
	}
	if !strings.Contains(got, "output truncated") {
		t.Fatal("expected truncation marker")
	}

	short := "short"
	if TruncateOutput(short) != short {
		t.Fatal("short output must not change")
	}
}

func TestSyncTimeCallback(t *testing.T) {
	var target time.Time
	executor := New(Options{
		Now: func() time.Time { return time.Date(2026, 10, 17, 10, 0, 0, 0, time.UTC) },
		SyncTime: func(value time.Time) error {
			target = value
			return nil
		},
	})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: "node/synctime", Argument: "2026-10-17 12:00:00"}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusCompleted {
		t.Fatalf("unexpected replies: %+v", replies)
	}
	want := time.Date(2026, 10, 17, 12, 0, 0, 0, time.UTC)
	if !target.Equal(want) {
		t.Fatalf("target = %v, want %v", target, want)
	}
}

func TestSyncTimeFailure(t *testing.T) {
	executor := New(Options{
		Now:      func() time.Time { return time.Date(2026, 10, 17, 10, 0, 0, 0, time.UTC) },
		SyncTime: func(time.Time) error { return errors.New("denied") },
	})
	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: "node/synctime", Argument: "2026-10-17 12:00:00"}, reply)
	})
	if len(replies) != 1 || replies[0].Status != protocol.StatusError || !strings.Contains(replies[0].Data, "denied") {
		t.Fatalf("unexpected replies: %+v", replies)
	}
}
