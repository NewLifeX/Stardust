package agent

import (
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"sync"
	"testing"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

func TestOnceLoginPingCommandReply(t *testing.T) {
	var (
		mu       sync.Mutex
		loginReq protocol.LoginInfo
		replies  []protocol.CommandReplyModel
		logouts  int
	)
	mux := http.NewServeMux()
	mux.HandleFunc("/node/login", func(w http.ResponseWriter, r *http.Request) {
		var request protocol.LoginInfo
		if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		mu.Lock()
		loginReq = request
		mu.Unlock()
		writeJSON(w, protocol.LoginResponse{
			Code: "CODE1", Secret: "SECRET1", Name: "node-1", Token: "token-1", Expire: 7200,
			ServerTime: time.Now().UnixMilli(),
		})
	})
	mux.HandleFunc("/node/ping", func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("Authorization") != "Bearer token-1" {
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
		writeJSON(w, protocol.PingResponse{
			Period: 60,
			Commands: []protocol.CommandModel{{
				Id: 123, Command: "node/setchannel", Argument: "Beta",
			}},
		})
	})
	mux.HandleFunc("/node/commandreply", func(w http.ResponseWriter, r *http.Request) {
		var reply protocol.CommandReplyModel
		if err := json.NewDecoder(r.Body).Decode(&reply); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		mu.Lock()
		replies = append(replies, reply)
		mu.Unlock()
		w.WriteHeader(http.StatusNoContent)
	})
	mux.HandleFunc("/node/logout", func(w http.ResponseWriter, r *http.Request) {
		mu.Lock()
		logouts++
		mu.Unlock()
		writeJSON(w, protocol.LoginResponse{Name: "node-1"})
	})
	mux.HandleFunc("/node/postevents", func(w http.ResponseWriter, r *http.Request) {
		_, _ = w.Write([]byte("0"))
	})
	server := httptest.NewServer(mux)
	defer server.Close()

	dir := t.TempDir()
	cfg := config.Default()
	cfg.Server = server.URL
	cfg.DataDir = dir
	cfg.CommandTimeout = 5
	cfg.Channel = "Release"
	cfg.AllowExec = false
	cfgPath := filepath.Join(dir, "staragent.json")
	if err := config.Save(cfgPath, &cfg); err != nil {
		t.Fatalf("save config: %v", err)
	}

	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	instance, err := New(context.Background(), Options{
		ConfigPath: cfgPath,
		Config:     &cfg,
		Logger:     logger,
		Version:    "test-version",
		BuildTime:  time.Now(),
	})
	if err != nil {
		t.Fatalf("new agent: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 60*time.Second)
	defer cancel()
	if err := instance.Once(ctx); err != nil {
		t.Fatalf("once: %v", err)
	}

	mu.Lock()
	defer mu.Unlock()

	if loginReq.Code != "" {
		t.Fatalf("first login should not send Code: %+v", loginReq)
	}
	if loginReq.ProductCode != cfg.ProductCode || loginReq.Node.Version != "test-version" {
		t.Fatalf("unexpected login request: %+v", loginReq)
	}
	if loginReq.Node.MachineName == "" || loginReq.Node.ProcessorCount == 0 {
		t.Fatalf("node info not populated: %+v", loginReq.Node)
	}
	if len(replies) != 1 || replies[0].Id != 123 || replies[0].Status != protocol.StatusCompleted {
		t.Fatalf("unexpected command replies: %+v", replies)
	}
	if logouts != 1 {
		t.Fatalf("logout count = %d", logouts)
	}

	loaded, err := config.Load(cfgPath)
	if err != nil {
		t.Fatalf("load saved config: %v", err)
	}
	if loaded.Code != "CODE1" || loaded.Secret != "SECRET1" {
		t.Fatalf("identity not persisted: %+v", loaded)
	}
	if loaded.Channel != "Beta" {
		t.Fatalf("channel not persisted: %q", loaded.Channel)
	}
}

func TestEventQueuePersistenceAndCap(t *testing.T) {
	path := filepath.Join(t.TempDir(), "events.json")
	queue, err := NewEventQueue(path, 2)
	if err != nil {
		t.Fatalf("new queue: %v", err)
	}

	_ = queue.Add(protocol.EventModel{Time: 1, Type: "info", Name: "A"})
	_ = queue.Add(protocol.EventModel{Time: 2, Type: "info", Name: "B"})
	_ = queue.Add(protocol.EventModel{Time: 3, Type: "info", Name: "C"})
	if queue.Len() != 2 {
		t.Fatalf("queue length = %d", queue.Len())
	}

	queue2, err := NewEventQueue(path, 2)
	if err != nil {
		t.Fatalf("reload queue: %v", err)
	}
	batch := queue2.Take(10)
	if len(batch) != 2 || batch[0].Name != "B" || batch[1].Name != "C" {
		t.Fatalf("unexpected batch: %+v", batch)
	}
	if queue2.Len() != 0 {
		t.Fatalf("queue should be empty, got %d", queue2.Len())
	}

	queue2.PutFront(batch)
	if queue2.Len() != 2 {
		t.Fatalf("queue after PutFront = %d", queue2.Len())
	}
}

func writeJSON(w http.ResponseWriter, value any) {
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(value)
}
