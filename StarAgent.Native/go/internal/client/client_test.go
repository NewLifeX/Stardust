package client

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/coder/websocket"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/command"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

func TestLoginPingCommandReplyFlow(t *testing.T) {
	var (
		mu             sync.Mutex
		loginRequest   protocol.LoginInfo
		commandReplies []protocol.CommandReplyModel
	)
	mux := http.NewServeMux()
	mux.HandleFunc("/node/login", func(w http.ResponseWriter, r *http.Request) {
		var request protocol.LoginInfo
		if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		mu.Lock()
		loginRequest = request
		mu.Unlock()

		writeJSON(t, w, protocol.LoginResponse{
			Code: "CODE1", Secret: "SECRET1", Name: "node-1", Token: "token-1", Expire: 7200,
		})
	})
	mux.HandleFunc("/node/ping", func(w http.ResponseWriter, r *http.Request) {
		if got := r.Header.Get("Authorization"); got != "Bearer token-1" {
			http.Error(w, "bad token", http.StatusUnauthorized)
			return
		}
		writeJSON(t, w, protocol.PingResponse{
			Period: 60,
			Commands: []protocol.CommandModel{{
				Id: 123, Command: "node/setchannel", Argument: "Beta",
			}},
		})
	})
	mux.HandleFunc("/node/commandreply", func(w http.ResponseWriter, r *http.Request) {
		if got := r.Header.Get("Authorization"); got != "Bearer token-1" {
			http.Error(w, "bad token", http.StatusUnauthorized)
			return
		}
		var reply protocol.CommandReplyModel
		if err := json.NewDecoder(r.Body).Decode(&reply); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		mu.Lock()
		commandReplies = append(commandReplies, reply)
		mu.Unlock()
		w.WriteHeader(http.StatusNoContent)
	})
	server := httptest.NewServer(mux)
	defer server.Close()

	cl, err := New(server.URL, true, nil)
	if err != nil {
		t.Fatalf("new client: %v", err)
	}

	login, err := cl.Login(context.Background(), protocol.LoginInfo{ProductCode: "StarAgent-Go"})
	if err != nil {
		t.Fatalf("login: %v", err)
	}
	if login.Token != "token-1" || cl.Token() != "token-1" {
		t.Fatalf("token not stored: %+v", login)
	}

	ping, err := cl.Ping(context.Background(), protocol.PingInfo{Time: time.Now().UnixMilli()})
	if err != nil {
		t.Fatalf("ping: %v", err)
	}
	if len(ping.Commands) != 1 {
		t.Fatalf("unexpected commands: %+v", ping.Commands)
	}

	var channel string
	executor := command.New(command.Options{SetChannel: func(value string) error {
		channel = value
		return nil
	}})
	var reply protocol.CommandReplyModel
	done := make(chan struct{})
	executor.Handle(context.Background(), ping.Commands[0], func(value protocol.CommandReplyModel) {
		reply = value
		close(done)
	})
	select {
	case <-done:
	case <-time.After(3 * time.Second):
		t.Fatal("command execution timed out")
	}

	if err := cl.CommandReply(context.Background(), reply); err != nil {
		t.Fatalf("command reply: %v", err)
	}

	mu.Lock()
	defer mu.Unlock()
	if loginRequest.ProductCode != "StarAgent-Go" {
		t.Fatalf("login request = %+v", loginRequest)
	}
	if channel != "Beta" {
		t.Fatalf("channel = %q", channel)
	}
	if len(commandReplies) != 1 || commandReplies[0].Id != 123 || commandReplies[0].Status != protocol.StatusCompleted {
		t.Fatalf("command replies = %+v", commandReplies)
	}
}

func TestPingReloginAfter401(t *testing.T) {
	var logins atomic.Int32
	var pings atomic.Int32

	mux := http.NewServeMux()
	mux.HandleFunc("/node/login", func(w http.ResponseWriter, r *http.Request) {
		count := logins.Add(1)
		writeJSON(t, w, protocol.LoginResponse{Token: "token-" + string(rune('0'+count)), Expire: 3600})
	})
	mux.HandleFunc("/node/ping", func(w http.ResponseWriter, r *http.Request) {
		if pings.Add(1) == 1 {
			http.Error(w, "expired", http.StatusUnauthorized)
			return
		}
		writeJSON(t, w, protocol.PingResponse{Period: 60})
	})
	server := httptest.NewServer(mux)
	defer server.Close()

	cl, err := New(server.URL, true, nil)
	if err != nil {
		t.Fatalf("new client: %v", err)
	}
	cl.OnUnauthorized(func(ctx context.Context) error {
		_, err := cl.Login(ctx, protocol.LoginInfo{ProductCode: "StarAgent-Go"})
		return err
	})

	if _, err := cl.Login(context.Background(), protocol.LoginInfo{ProductCode: "StarAgent-Go"}); err != nil {
		t.Fatalf("initial login: %v", err)
	}
	if _, err := cl.Ping(context.Background(), protocol.PingInfo{}); err != nil {
		t.Fatalf("ping after 401: %v", err)
	}
	if logins.Load() != 2 {
		t.Fatalf("logins = %d, want 2", logins.Load())
	}
	if pings.Load() != 2 {
		t.Fatalf("pings = %d, want 2", pings.Load())
	}
}

func TestNon2xxError(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.Error(w, "denied", http.StatusForbidden)
	}))
	defer server.Close()

	cl, err := New(server.URL, true, nil)
	if err != nil {
		t.Fatalf("new client: %v", err)
	}
	_, err = cl.Login(context.Background(), protocol.LoginInfo{ProductCode: "StarAgent-Go"})
	if err == nil {
		t.Fatal("expected error")
	}
	status, ok := StatusCode(err)
	if !ok || status != http.StatusForbidden {
		t.Fatalf("status = %d, ok=%v, err=%v", status, ok, err)
	}
}

func TestWrappedPingResponse(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"code":0,"data":{"Period":30,"ServerTime":123},"msg":"ok"}`))
	}))
	defer server.Close()

	cl, err := New(server.URL, true, nil)
	if err != nil {
		t.Fatalf("new client: %v", err)
	}
	cl.SetToken("t", 60)

	resp, err := cl.Ping(context.Background(), protocol.PingInfo{})
	if err != nil {
		t.Fatalf("ping: %v", err)
	}
	if resp.Period != 30 || resp.ServerTime != 123 {
		t.Fatalf("unexpected response: %+v", resp)
	}
}

func TestPostEventsCount(t *testing.T) {
	var received []protocol.EventModel
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if err := json.NewDecoder(r.Body).Decode(&received); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		_, _ = w.Write([]byte(`{"code":0,"data":2}`))
	}))
	defer server.Close()

	cl, err := New(server.URL, true, nil)
	if err != nil {
		t.Fatalf("new client: %v", err)
	}
	cl.SetToken("t", 60)

	count, err := cl.PostEvents(context.Background(), []protocol.EventModel{
		{Time: 1, Type: "info", Name: "A"},
		{Time: 2, Type: "info", Name: "B"},
	})
	if err != nil {
		t.Fatalf("post events: %v", err)
	}
	if count != 2 || len(received) != 2 {
		t.Fatalf("count=%d received=%+v", count, received)
	}
}

func TestBackoffSequence(t *testing.T) {
	backoff := &Backoff{Min: time.Second, Max: 30 * time.Second}
	want := []time.Duration{1, 2, 4, 8, 16, 30, 30}
	for i, expected := range want {
		if got := backoff.Next(); got != expected*time.Second {
			t.Fatalf("attempt %d = %v, want %v", i, got, expected*time.Second)
		}
	}

	backoff.Reset()
	if got := backoff.Next(); got != time.Second {
		t.Fatalf("after reset = %v, want 1s", got)
	}
}

func TestNotifyWebSocket(t *testing.T) {
	commandCh := make(chan protocol.CommandModel, 1)
	serverErrors := make(chan error, 4)

	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/node/notify" {
			http.NotFound(w, r)
			return
		}
		if got := r.Header.Get("Authorization"); got != "Bearer token-1" {
			http.Error(w, "bad token", http.StatusUnauthorized)
			return
		}
		conn, err := websocket.Accept(w, r, nil)
		if err != nil {
			serverErrors <- err
			return
		}
		defer conn.CloseNow()

		ctx := r.Context()
		if err := conn.Write(ctx, websocket.MessageText, []byte("Ping")); err != nil {
			serverErrors <- err
			return
		}

		sentCommand := false
		for {
			_, data, err := conn.Read(ctx)
			if err != nil {
				return
			}
			switch string(data) {
			case "Pong":
				if !sentCommand {
					sentCommand = true
					payload, _ := json.Marshal(protocol.CommandModel{Id: 7, Command: "node/synctime"})
					if err := conn.Write(ctx, websocket.MessageText, payload); err != nil {
						serverErrors <- err
						return
					}
				}
			case "Ping":
				if err := conn.Write(ctx, websocket.MessageText, []byte("Pong")); err != nil {
					serverErrors <- err
					return
				}
			}
		}
	}))
	defer server.Close()

	cl, err := New(server.URL, true, nil)
	if err != nil {
		t.Fatalf("new client: %v", err)
	}
	cl.SetToken("token-1", 3600)

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	runErr := make(chan error, 1)
	go func() {
		runErr <- cl.RunNotify(ctx, NotifyHandler{
			Interval: 100 * time.Millisecond,
			OnCommand: func(_ context.Context, cmd protocol.CommandModel) {
				commandCh <- cmd
			},
		})
	}()

	select {
	case cmd := <-commandCh:
		if cmd.Id != 7 || cmd.Command != "node/synctime" {
			t.Fatalf("unexpected command: %+v", cmd)
		}
	case err := <-serverErrors:
		t.Fatalf("server error: %v", err)
	case <-time.After(5 * time.Second):
		t.Fatal("timed out waiting for websocket command")
	}

	cancel()
	select {
	case err := <-runErr:
		if err != nil && !strings.Contains(err.Error(), "context canceled") && !strings.Contains(err.Error(), "closed") {
			t.Fatalf("notify error: %v", err)
		}
	case <-time.After(3 * time.Second):
		t.Fatal("notify did not stop after cancel")
	}
}

func writeJSON(t *testing.T, w http.ResponseWriter, value any) {
	t.Helper()

	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(value); err != nil {
		t.Fatalf("encode response: %v", err)
	}
}
