package fakeserver

import (
	"bytes"
	"context"
	"encoding/json"
	"io"
	"net/http"
	"testing"
	"time"

	"github.com/coder/websocket"

	"staragent-conformance/internal/protocol"
)

func startServer(t *testing.T, opts Options) *Server {
	t.Helper()
	srv := New(opts)
	if err := srv.Start(); err != nil {
		t.Fatalf("Start: %v", err)
	}
	t.Cleanup(func() { _ = srv.Close() })

	return srv
}

func doJSON(t *testing.T, method, url string, body any, token string) (int, []byte) {
	t.Helper()
	var reader io.Reader
	if body != nil {
		data, err := json.Marshal(body)
		if err != nil {
			t.Fatalf("marshal: %v", err)
		}
		reader = bytes.NewReader(data)
	}
	req, err := http.NewRequest(method, url, reader)
	if err != nil {
		t.Fatalf("request: %v", err)
	}
	req.Header.Set("Content-Type", "application/json")
	if token != "" {
		req.Header.Set("Authorization", "Bearer "+token)
	}
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatalf("%s %s: %v", method, url, err)
	}
	defer resp.Body.Close()
	data, _ := io.ReadAll(resp.Body)

	return resp.StatusCode, data
}

func login(t *testing.T, srv *Server, code, secret string) (int, protocol.LoginResponse, []byte) {
	t.Helper()
	body := protocol.LoginRequest{
		Code:        code,
		Secret:      secret,
		ProductCode: "StarAgent-Test",
		Node: &protocol.NodeInfo{
			MachineName: "test-host",
			Compile:     protocol.FormatDateTime(time.Now()),
			Plugins:     []string{"agent:test"},
		},
	}
	status, data := doJSON(t, http.MethodPost, srv.URL()+"/node/login", body, "")
	var resp protocol.LoginResponse
	if status == http.StatusOK {
		if err := json.Unmarshal(data, &resp); err != nil {
			t.Fatalf("decode login: %v (%s)", err, data)
		}
	}

	return status, resp, data
}

func TestLoginPingUpgradeLogout(t *testing.T) {
	srv := startServer(t, Options{})

	status, first, _ := login(t, srv, "", "")
	if status != http.StatusOK {
		t.Fatalf("first login status = %d", status)
	}
	if first.Code == "" || first.Secret == "" || first.Token == "" {
		t.Fatalf("first login response = %+v", first)
	}
	if first.ServerTime <= 0 || first.Expire <= 0 {
		t.Fatalf("first login ServerTime/Expire = %d/%d", first.ServerTime, first.Expire)
	}

	status, _, _ = login(t, srv, first.Code, "wrong")
	if status != http.StatusUnauthorized {
		t.Fatalf("login with wrong secret status = %d, want 401", status)
	}

	status, second, _ := login(t, srv, first.Code, first.Secret)
	if status != http.StatusOK {
		t.Fatalf("second login status = %d", status)
	}
	if second.Token == "" || second.Token == first.Token {
		t.Fatalf("second login token = %q, first = %q", second.Token, first.Token)
	}
	if second.Code != "" || second.Secret != "" {
		t.Fatalf("second login must not resend Code/Secret, got %q/%q", second.Code, second.Secret)
	}

	status, _ = doJSON(t, http.MethodPost, srv.URL()+"/node/ping", protocol.PingInfo{Time: protocol.NowMillis()}, "")
	if status != http.StatusUnauthorized {
		t.Fatalf("unauthenticated ping status = %d, want 401", status)
	}

	status, data := doJSON(t, http.MethodPost, srv.URL()+"/node/ping", protocol.PingInfo{Time: protocol.NowMillis(), IP: "10.0.0.8"}, second.Token)
	if status != http.StatusOK {
		t.Fatalf("ping status = %d: %s", status, data)
	}
	var ping protocol.PingResponse
	if err := json.Unmarshal(data, &ping); err != nil {
		t.Fatalf("decode ping: %v", err)
	}
	if ping.Period <= 0 || ping.ServerTime <= 0 {
		t.Fatalf("ping response = %+v", ping)
	}

	status, _ = doJSON(t, http.MethodPost, srv.URL()+"/node/upgrade?channel=Release", nil, second.Token)
	if status != http.StatusOK {
		t.Fatalf("upgrade status = %d", status)
	}
	status, _ = doJSON(t, http.MethodPost, srv.URL()+"/node/logout?reason=test", nil, second.Token)
	if status != http.StatusOK {
		t.Fatalf("logout status = %d", status)
	}

	st := srv.State()
	if len(st.Logins) != 3 {
		t.Fatalf("logins recorded = %d, want 3", len(st.Logins))
	}
	if len(st.Pings) != 1 || st.Pings[0].TimeField <= 0 {
		t.Fatalf("pings = %+v", st.Pings)
	}
	if len(st.Upgrades) != 1 || st.Upgrades[0].Channel != "Release" {
		t.Fatalf("upgrades = %+v", st.Upgrades)
	}
	if len(st.Logouts) != 1 || st.Logouts[0].Reason != "test" {
		t.Fatalf("logouts = %+v", st.Logouts)
	}
}

func TestHTTPFallbackCommandsAndEventRetry(t *testing.T) {
	srv := startServer(t, Options{EventsFailures: 2})
	status, loginResp, _ := login(t, srv, "", "")
	if status != http.StatusOK {
		t.Fatalf("login status = %d", status)
	}

	cmd := protocol.CommandModel{Id: 77, Command: "node/synctime"}
	status, _ = doJSON(t, http.MethodPost, srv.URL()+"/control/ping", map[string]any{"Commands": []protocol.CommandModel{cmd}}, "")
	if status != http.StatusOK {
		t.Fatalf("control ping status = %d", status)
	}
	status, data := doJSON(t, http.MethodPost, srv.URL()+"/node/ping", protocol.PingInfo{Time: protocol.NowMillis()}, loginResp.Token)
	if status != http.StatusOK {
		t.Fatalf("ping status = %d: %s", status, data)
	}
	var ping protocol.PingResponse
	if err := json.Unmarshal(data, &ping); err != nil {
		t.Fatalf("decode ping: %v", err)
	}
	if len(ping.Commands) != 1 || ping.Commands[0].Id != 77 {
		t.Fatalf("ping commands = %+v", ping.Commands)
	}

	status, _ = doJSON(t, http.MethodPost, srv.URL()+"/node/commandreply", protocol.CommandReplyModel{Id: 77, Status: protocol.StatusCompleted, Data: "ok"}, loginResp.Token)
	if status != http.StatusOK {
		t.Fatalf("commandreply status = %d", status)
	}

	events := []protocol.EventModel{{Time: protocol.NowMillis(), Type: "info", Name: "Startup"}}
	for i := 0; i < 2; i++ {
		status, _ = doJSON(t, http.MethodPost, srv.URL()+"/node/postevents", events, loginResp.Token)
		if status != http.StatusInternalServerError {
			t.Fatalf("postevents attempt %d status = %d, want 500", i+1, status)
		}
	}
	status, data = doJSON(t, http.MethodPost, srv.URL()+"/node/postevents", events, loginResp.Token)
	if status != http.StatusOK {
		t.Fatalf("postevents status = %d: %s", status, data)
	}
	count, err := protocol.ParseEventCount(data)
	if err != nil || count != 1 {
		t.Fatalf("ParseEventCount(%s) = %d, %v", data, count, err)
	}

	st := srv.State()
	if len(st.Replies) != 1 || st.Replies[0].Reply.Id != 77 {
		t.Fatalf("replies = %+v", st.Replies)
	}
	if len(st.EventAttempts) != 3 || len(st.Events) != 1 {
		t.Fatalf("event attempts=%d events=%d", len(st.EventAttempts), len(st.Events))
	}
}

func TestWebSocketCommandAndPingPong(t *testing.T) {
	srv := startServer(t, Options{AutoPingInterval: 100 * time.Millisecond})
	status, loginResp, _ := login(t, srv, "", "")
	if status != http.StatusOK {
		t.Fatalf("login status = %d", status)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	header := http.Header{}
	header.Set("Authorization", "Bearer "+loginResp.Token)
	conn, _, err := websocket.Dial(ctx, srv.WSURL(), &websocket.DialOptions{HTTPHeader: header})
	if err != nil {
		t.Fatalf("dial ws: %v", err)
	}
	defer conn.CloseNow()

	cmd := protocol.CommandModel{Id: 88, Command: "node/synctime"}
	status, _ = doJSON(t, http.MethodPost, srv.URL()+"/control/command?via=ws", cmd, "")
	if status != http.StatusOK {
		t.Fatalf("control command status = %d", status)
	}

	var got protocol.CommandModel
	for got.Id == 0 {
		typ, data, err := conn.Read(ctx)
		if err != nil {
			t.Fatalf("read ws: %v", err)
		}
		if typ != websocket.MessageText {
			continue
		}
		switch string(data) {
		case "Ping":
			if err := conn.Write(ctx, websocket.MessageText, []byte("Pong")); err != nil {
				t.Fatalf("write pong: %v", err)
			}
		default:
			var parsed protocol.CommandModel
			if err := json.Unmarshal(data, &parsed); err != nil {
				t.Fatalf("decode command %s: %v", data, err)
			}
			got = parsed
		}
	}
	if got.Id != 88 || got.Command != "node/synctime" {
		t.Fatalf("command = %+v", got)
	}

	serverPingSeen := false
	pingDeadline := time.Now().Add(5 * time.Second)
	for !serverPingSeen && time.Now().Before(pingDeadline) {
		typ, data, err := conn.Read(ctx)
		if err != nil {
			t.Fatalf("read ws: %v", err)
		}
		if typ != websocket.MessageText {
			continue
		}
		switch string(data) {
		case "Ping":
			if err := conn.Write(ctx, websocket.MessageText, []byte("Pong")); err != nil {
				t.Fatalf("write pong: %v", err)
			}
			serverPingSeen = true
		}
	}
	if !serverPingSeen {
		t.Fatal("server did not send an application-level Ping")
	}

	if err := conn.Write(ctx, websocket.MessageText, []byte("Ping")); err != nil {
		t.Fatalf("write ping: %v", err)
	}
	pongSeen := false
	pongDeadline := time.Now().Add(5 * time.Second)
	for !pongSeen && time.Now().Before(pongDeadline) {
		typ, data, err := conn.Read(ctx)
		if err != nil {
			t.Fatalf("read ws: %v", err)
		}
		if typ != websocket.MessageText {
			continue
		}
		switch string(data) {
		case "Ping":
			if err := conn.Write(ctx, websocket.MessageText, []byte("Pong")); err != nil {
				t.Fatalf("write pong: %v", err)
			}
		case "Pong":
			pongSeen = true
		}
	}
	if !pongSeen {
		t.Fatal("server did not reply with Pong")
	}
	waitFor(t, 2*time.Second, func() bool { return srv.State().Pongs >= 1 })

	st := srv.State()
	if st.WSConnections != 1 || st.WSActive != 1 {
		t.Fatalf("ws connections=%d active=%d", st.WSConnections, st.WSActive)
	}
	if st.Pongs < 1 {
		t.Fatalf("server did not observe a Pong, pongs=%d", st.Pongs)
	}
	if len(st.Frames) == 0 {
		t.Fatal("no websocket frames recorded")
	}

	status, _ = doJSON(t, http.MethodPost, srv.URL()+"/control/kick", nil, "")
	if status != http.StatusOK {
		t.Fatalf("kick status = %d", status)
	}
	waitFor(t, 2*time.Second, func() bool { return srv.State().WSActive == 0 })
}

func waitFor(t *testing.T, timeout time.Duration, predicate func() bool) {
	t.Helper()
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		if predicate() {
			return
		}
		time.Sleep(20 * time.Millisecond)
	}
	t.Fatal("condition not met before timeout")
}
