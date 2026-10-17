package scenario

import (
	"context"
	"fmt"
	"net/http"
	"strings"
	"time"

	"staragent-conformance/internal/fakeserver"
	"staragent-conformance/internal/protocol"
)

// Definition is one conformance scenario.
type Definition struct {
	Name        string
	Description string
	Run         func(context.Context, *Env) error
}

// Definitions returns the built-in scenarios in execution order.
func Definitions() []Definition {
	return []Definition{
		{Name: "basic", Description: "login, heartbeat, WebSocket command, reply and persisted Code/Secret", Run: runBasic},
		{Name: "http-fallback", Description: "commands delivered in the ping response when WebSocket is unavailable", Run: runHTTPFallback},
		{Name: "dedup", Description: "a duplicated command Id is executed and replied only once", Run: runDedup},
		{Name: "expired", Description: "an expired command is replied with Status=3 (cancelled)", Run: runExpired},
		{Name: "reconnect", Description: "WebSocket disconnect is followed by reconnect and command delivery", Run: runReconnect},
		{Name: "events", Description: "failed event uploads are retried until accepted", Run: runEvents},
	}
}

// Names returns the scenario names.
func Names() []string {
	defs := Definitions()
	names := make([]string, 0, len(defs))
	for _, def := range defs {
		names = append(names, def.Name)
	}

	return names
}

// Select resolves "all" or a comma-separated scenario list.
func Select(name string) ([]Definition, error) {
	defs := Definitions()
	if strings.EqualFold(strings.TrimSpace(name), "all") {
		return defs, nil
	}
	byName := make(map[string]Definition, len(defs))
	for _, def := range defs {
		byName[def.Name] = def
	}

	var selected []Definition
	for _, part := range strings.Split(name, ",") {
		part = strings.TrimSpace(part)
		if part == "" {
			continue
		}
		def, ok := byName[strings.ToLower(part)]
		if !ok {
			return nil, fmt.Errorf("unknown scenario %q (available: %s)", part, strings.Join(Names(), ", "))
		}
		selected = append(selected, def)
	}
	if len(selected) == 0 {
		return nil, fmt.Errorf("no scenario selected")
	}

	return selected, nil
}

func runBasic(ctx context.Context, e *Env) error {
	if err := e.StartAgent(); err != nil {
		return fmt.Errorf("start agent: %w", err)
	}

	st, err := e.WaitFor("first login", func(s *fakeserver.State) bool { return len(s.Logins) >= 1 })
	if err != nil {
		return err
	}
	first := st.Logins[0]
	if first.Status != http.StatusOK {
		return fmt.Errorf("first login failed: status=%d error=%s", first.Status, first.Error)
	}
	if first.Code != "" || first.Secret != "" {
		return fmt.Errorf("first login must dynamically register with empty Code/Secret, got Code=%q Secret=%q", first.Code, first.Secret)
	}
	if first.ProductCode == "" {
		return fmt.Errorf("login request did not include ProductCode")
	}
	if first.Node == nil {
		return fmt.Errorf("login request did not include Node")
	}
	if !protocol.IsDateTime(first.Node.Compile) {
		return fmt.Errorf("Node.Compile %q is not yyyy-MM-dd HH:mm:ss", first.Node.Compile)
	}
	if first.ResponseCode == "" || first.ResponseSecret == "" {
		return fmt.Errorf("first login response did not include Code/Secret")
	}
	if first.ResponseToken == "" {
		return fmt.Errorf("first login response did not include Token")
	}

	st, err = e.WaitFor("heartbeat", func(s *fakeserver.State) bool { return len(s.Pings) >= 1 })
	if err != nil {
		return err
	}
	ping := st.Pings[0]
	if ping.Status != http.StatusOK {
		return fmt.Errorf("first ping was rejected: status=%d error=%s", ping.Status, ping.Error)
	}
	if ping.TimeField <= 0 {
		return fmt.Errorf("PingInfo.Time must be Unix milliseconds, got %d", ping.TimeField)
	}

	if _, err := e.WaitFor("WebSocket connection", func(s *fakeserver.State) bool { return s.WSConnections >= 1 }); err != nil {
		return err
	}
	if _, err := e.WaitFor("application-level Pong", func(s *fakeserver.State) bool { return s.Pongs >= 1 }); err != nil {
		return err
	}

	const commandId int64 = 1001
	if err := e.Command(protocol.CommandModel{Id: commandId, Command: "node/synctime"}, "ws"); err != nil {
		return fmt.Errorf("enqueue command: %w", err)
	}
	_, replies, err := e.WaitForReply(commandId, 1)
	if err != nil {
		return err
	}
	if replies[0].Reply.Status != protocol.StatusCompleted {
		return fmt.Errorf("command %d reply status=%d, want %d", commandId, replies[0].Reply.Status, protocol.StatusCompleted)
	}
	if replies[0].Reply.Data == "" {
		return fmt.Errorf("command %d reply did not include Data", commandId)
	}

	pingsBeforeRestart := len(st.Pings)
	if err := e.RestartAgent(); err != nil {
		return fmt.Errorf("restart agent: %w", err)
	}
	st, err = e.WaitFor("second login after restart", func(s *fakeserver.State) bool { return len(s.Logins) >= 2 })
	if err != nil {
		return err
	}
	second := st.Logins[1]
	if second.Status != http.StatusOK {
		return fmt.Errorf("second login failed: status=%d error=%s", second.Status, second.Error)
	}
	if second.Code != st.IssuedCode || second.Secret != st.IssuedSecret {
		return fmt.Errorf("agent did not persist Code/Secret across restart: sent Code=%q Secret=%q", second.Code, second.Secret)
	}
	if second.ResponseToken == "" {
		return fmt.Errorf("second login response did not include a new Token")
	}

	st, err = e.WaitFor("heartbeat after restart", func(s *fakeserver.State) bool {
		return len(s.Pings) > pingsBeforeRestart && s.Pings[len(s.Pings)-1].Status == http.StatusOK
	})
	if err != nil {
		return err
	}

	return nil
}

func runHTTPFallback(ctx context.Context, e *Env) error {
	if err := e.Control.SetWSDisabled(ctx, true); err != nil {
		return fmt.Errorf("disable websocket: %w", err)
	}
	if err := e.StartAgent(); err != nil {
		return fmt.Errorf("start agent: %w", err)
	}
	if _, err := e.WaitFor("login", func(s *fakeserver.State) bool { return len(s.Logins) >= 1 }); err != nil {
		return err
	}
	if _, err := e.WaitFor("heartbeat", func(s *fakeserver.State) bool { return len(s.Pings) >= 1 }); err != nil {
		return err
	}

	const commandId int64 = 2001
	if err := e.Command(protocol.CommandModel{Id: commandId, Command: "node/synctime"}, "http"); err != nil {
		return fmt.Errorf("enqueue HTTP fallback command: %w", err)
	}
	st, replies, err := e.WaitForReply(commandId, 1)
	if err != nil {
		return err
	}
	if replies[0].Reply.Status != protocol.StatusCompleted {
		return fmt.Errorf("HTTP fallback command reply status=%d, want %d", replies[0].Reply.Status, protocol.StatusCompleted)
	}
	if !commandDeliveredInPing(st, commandId) {
		return fmt.Errorf("command %d was not delivered in a ping response", commandId)
	}
	if st.WSConnections != 0 {
		return fmt.Errorf("websocket should stay disabled, but %d connection(s) were accepted", st.WSConnections)
	}

	return nil
}

func runDedup(ctx context.Context, e *Env) error {
	if err := e.StartAgent(); err != nil {
		return fmt.Errorf("start agent: %w", err)
	}
	if _, err := e.WaitFor("login and websocket", func(s *fakeserver.State) bool {
		return len(s.Logins) >= 1 && s.WSConnections >= 1 && s.WSActive >= 1
	}); err != nil {
		return err
	}

	const commandId int64 = 3001
	cmd := protocol.CommandModel{Id: commandId, Command: "node/synctime"}
	if err := e.Command(cmd, "ws"); err != nil {
		return fmt.Errorf("enqueue first command: %w", err)
	}
	if err := e.Command(cmd, "ws"); err != nil {
		return fmt.Errorf("enqueue duplicate command: %w", err)
	}

	st, replies, err := e.WaitForReply(commandId, 1)
	if err != nil {
		return err
	}
	if replies[0].Reply.Status != protocol.StatusCompleted {
		return fmt.Errorf("command reply status=%d, want %d", replies[0].Reply.Status, protocol.StatusCompleted)
	}
	if !e.Sleep(1500 * time.Millisecond) {
		return e.Ctx.Err()
	}
	st, err = e.Control.State(ctx)
	if err != nil {
		return fmt.Errorf("read state: %w", err)
	}
	if got := countReplies(st, commandId); got != 1 {
		return fmt.Errorf("duplicate command produced %d replies, want exactly 1", got)
	}
	if got := countDeliveries(st, commandId); got < 2 {
		return fmt.Errorf("expected 2 deliveries of command %d, got %d", commandId, got)
	}

	return nil
}

func runExpired(ctx context.Context, e *Env) error {
	if err := e.StartAgent(); err != nil {
		return fmt.Errorf("start agent: %w", err)
	}
	if _, err := e.WaitFor("login and websocket", func(s *fakeserver.State) bool {
		return len(s.Logins) >= 1 && s.WSConnections >= 1
	}); err != nil {
		return err
	}

	const commandId int64 = 4001
	cmd := protocol.CommandModel{
		Id:      commandId,
		Command: "node/synctime",
		Expire:  protocol.FormatDateTime(time.Now().Add(-time.Minute)),
	}
	if err := e.Command(cmd, "ws"); err != nil {
		return fmt.Errorf("enqueue expired command: %w", err)
	}
	_, replies, err := e.WaitForReply(commandId, 1)
	if err != nil {
		return err
	}
	if replies[0].Reply.Status != protocol.StatusCancelled {
		return fmt.Errorf("expired command reply status=%d, want %d (cancelled)", replies[0].Reply.Status, protocol.StatusCancelled)
	}
	if replies[0].Reply.Data == "" {
		return fmt.Errorf("expired command reply did not include Data")
	}

	return nil
}

func runReconnect(ctx context.Context, e *Env) error {
	if err := e.StartAgent(); err != nil {
		return fmt.Errorf("start agent: %w", err)
	}
	st, err := e.WaitFor("login, websocket and heartbeat", func(s *fakeserver.State) bool {
		return len(s.Logins) >= 1 && s.WSConnections >= 1 && len(s.Pings) >= 1 && s.Pongs >= 1
	})
	if err != nil {
		return err
	}
	initialConns := st.WSConnections
	initialPings := len(st.Pings)
	initialPongs := st.Pongs

	if err := e.Control.Kick(ctx); err != nil {
		return fmt.Errorf("kick websocket: %w", err)
	}
	st, err = e.WaitFor("websocket reconnect", func(s *fakeserver.State) bool {
		return s.WSConnections > initialConns && s.WSActive >= 1
	})
	if err != nil {
		return err
	}
	if _, err := e.WaitFor("heartbeat after reconnect", func(s *fakeserver.State) bool {
		return len(s.Pings) > initialPings
	}); err != nil {
		return err
	}
	if _, err := e.WaitFor("pong after reconnect", func(s *fakeserver.State) bool {
		return s.Pongs > initialPongs
	}); err != nil {
		return err
	}

	const commandId int64 = 5001
	if err := e.Command(protocol.CommandModel{Id: commandId, Command: "node/synctime"}, "ws"); err != nil {
		return fmt.Errorf("enqueue command after reconnect: %w", err)
	}
	_, replies, err := e.WaitForReply(commandId, 1)
	if err != nil {
		return err
	}
	if replies[0].Reply.Status != protocol.StatusCompleted {
		return fmt.Errorf("post-reconnect command reply status=%d, want %d", replies[0].Reply.Status, protocol.StatusCompleted)
	}

	return nil
}

func runEvents(ctx context.Context, e *Env) error {
	if err := e.Control.FailEvents(ctx, 2, http.StatusInternalServerError); err != nil {
		return fmt.Errorf("configure event failures: %w", err)
	}
	if err := e.StartAgent(); err != nil {
		return fmt.Errorf("start agent: %w", err)
	}
	if _, err := e.WaitFor("login", func(s *fakeserver.State) bool { return len(s.Logins) >= 1 }); err != nil {
		return err
	}

	st, err := e.WaitFor("event upload retry", func(s *fakeserver.State) bool {
		return len(s.EventAttempts) >= 3 && len(s.Events) >= 1
	})
	if err != nil {
		return fmt.Errorf("%w (the events scenario expects the agent to report at least one event, e.g. Startup)", err)
	}
	if st.EventAttempts[0].Status != http.StatusInternalServerError || st.EventAttempts[1].Status != http.StatusInternalServerError {
		return fmt.Errorf("expected the first two event uploads to fail, got statuses %d and %d",
			st.EventAttempts[0].Status, st.EventAttempts[1].Status)
	}
	if st.EventAttempts[len(st.EventAttempts)-1].Status != http.StatusOK {
		return fmt.Errorf("last event attempt status=%d, want 200", st.EventAttempts[len(st.EventAttempts)-1].Status)
	}
	for _, rec := range st.Events {
		switch rec.Event.Type {
		case "info", "alert", "error":
		default:
			return fmt.Errorf("event %q has invalid Type %q", rec.Event.Name, rec.Event.Type)
		}
		if rec.Event.Name == "" {
			return fmt.Errorf("event has empty Name")
		}
		if rec.Event.Time <= 0 {
			return fmt.Errorf("event %q has invalid Time %d", rec.Event.Name, rec.Event.Time)
		}
	}

	return nil
}
