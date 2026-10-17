package mockagent

import (
	"context"
	"net/http"
	"strings"
	"sync"
	"time"

	"staragent-conformance/internal/protocol"
)

type eventQueue struct {
	mu    sync.Mutex
	items []protocol.EventModel
	wake  chan struct{}
}

func newEventQueue() *eventQueue {
	return &eventQueue{wake: make(chan struct{}, 1)}
}

func (q *eventQueue) push(event protocol.EventModel) {
	q.mu.Lock()
	q.items = append(q.items, event)
	q.mu.Unlock()
	select {
	case q.wake <- struct{}{}:
	default:
	}
}

func (q *eventQueue) snapshot() []protocol.EventModel {
	q.mu.Lock()
	defer q.mu.Unlock()

	return append([]protocol.EventModel(nil), q.items...)
}

func (q *eventQueue) drop(n int) {
	q.mu.Lock()
	defer q.mu.Unlock()
	if n >= len(q.items) {
		q.items = nil

		return
	}
	q.items = append([]protocol.EventModel(nil), q.items[n:]...)
}

func (q *eventQueue) wait(ctx context.Context) bool {
	select {
	case <-ctx.Done():
		return false
	case <-q.wake:
		return true
	}
}

type commandHandler struct {
	session   *session
	allowExec bool
	logf      Logger

	mu   sync.Mutex
	seen map[int64]time.Time
}

func newCommandHandler(sess *session, allowExec bool, logf Logger) *commandHandler {
	if logf == nil {
		logf = func(string, ...any) {}
	}

	return &commandHandler{
		session:   sess,
		allowExec: allowExec,
		logf:      logf,
		seen:      make(map[int64]time.Time),
	}
}

func (h *commandHandler) handle(ctx context.Context, cmd protocol.CommandModel, via string) {
	now := time.Now().UTC()
	h.mu.Lock()
	if _, ok := h.seen[cmd.Id]; ok {
		h.mu.Unlock()
		h.logf("duplicate command %d ignored", cmd.Id)

		return
	}
	h.seen[cmd.Id] = now
	h.pruneLocked(now)
	h.mu.Unlock()

	h.logf("command %d %q via %s", cmd.Id, cmd.Command, via)

	if expire := protocol.ParseDateTime(cmd.Expire); !expire.IsZero() && expire.Before(now) {
		h.reply(ctx, cmd.Id, protocol.StatusCancelled, "命令已过期")

		return
	}
	if start := protocol.ParseDateTime(cmd.StartTime); !start.IsZero() && start.After(now) {
		h.reply(ctx, cmd.Id, protocol.StatusProcessing, "命令已安排延迟执行")
		go func() {
			if !sleepCtx(ctx, start.Sub(time.Now())) {
				return
			}
			status, data := h.execute(cmd)
			h.reply(ctx, cmd.Id, status, data)
		}()

		return
	}

	status, data := h.execute(cmd)
	h.reply(ctx, cmd.Id, status, data)
}

func (h *commandHandler) pruneLocked(now time.Time) {
	if len(h.seen) < 256 {
		return
	}
	for id, at := range h.seen {
		if now.Sub(at) > time.Hour {
			delete(h.seen, id)
		}
	}
}

func (h *commandHandler) execute(cmd protocol.CommandModel) (int, string) {
	switch strings.ToLower(cmd.Command) {
	case "node/synctime":
		return protocol.StatusCompleted, "mock: 时间已同步 " + protocol.FormatDateTime(time.Now())
	case "node/setchannel":
		return protocol.StatusCompleted, "mock: 更新通道已设置"
	case "node/restart":
		return protocol.StatusCompleted, "mock: 已忽略重启"
	case "bash", "cmd":
		if !h.allowExec {
			return protocol.StatusCancelled, "命令执行未启用"
		}

		return protocol.StatusCompleted, "mock: 命令执行成功"
	default:
		return protocol.StatusError, "不支持命令: " + cmd.Command
	}
}

func (h *commandHandler) reply(ctx context.Context, id int64, status int, data string) {
	data = protocol.Truncate(data, protocol.MaxOutputBytes)
	reply := protocol.CommandReplyModel{Id: id, Status: status, Data: data}
	backoff := 100 * time.Millisecond
	for attempt := 0; attempt < 3; attempt++ {
		if _, _, err := h.session.doJSON(ctx, http.MethodPost, "/node/commandreply", reply, nil); err == nil {
			return
		} else if ctx.Err() != nil {
			return
		} else {
			h.logf("command %d reply failed (%d/3): %v", id, attempt+1, err)
		}
		if !sleepCtx(ctx, backoff) {
			return
		}
		backoff *= 2
	}
}
