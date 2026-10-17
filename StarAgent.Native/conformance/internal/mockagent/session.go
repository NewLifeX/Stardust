package mockagent

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"runtime"
	"strings"
	"sync"
	"time"

	"github.com/coder/websocket"

	"staragent-conformance/internal/protocol"
)

// Logger receives mock-agent log lines.
type Logger func(format string, args ...any)

type session struct {
	loginMu sync.Mutex
	mu      sync.Mutex

	cfg            *configFile
	client         *http.Client
	logf           Logger
	server         string
	productCode    string
	code           string
	secret         string
	token          string
	dataDir        string
	allowExec      bool
	metricInterval int
	startedAt      time.Time
}

func newSession(cfg *configFile, logf Logger) *session {
	if logf == nil {
		logf = func(string, ...any) {}
	}
	server := cfg.string("Server")
	if v := envFirst("STARAGENT_SERVER", "STARAGENT_SERVER_URL"); v != "" {
		server = v
	}
	dataDir := cfg.string("DataDir")
	if v := envFirst("STARAGENT_DATA_DIR", "STARAGENT_DATADIR"); v != "" {
		dataDir = v
	}
	if dataDir == "" {
		dataDir = "data"
	}

	return &session{
		cfg:            cfg,
		client:         &http.Client{Timeout: 10 * time.Second},
		logf:           logf,
		server:         strings.TrimRight(server, "/"),
		productCode:    orDefault(cfg.string("ProductCode"), "StarAgent-Conformance-Mock"),
		code:           cfg.string("Code"),
		secret:         cfg.string("Secret"),
		dataDir:        dataDir,
		allowExec:      cfg.bool("AllowExec", false),
		metricInterval: cfg.int("MetricsInterval", 60),
		startedAt:      time.Now(),
	}
}

func envFirst(names ...string) string {
	for _, name := range names {
		if v := os.Getenv(name); v != "" {
			return v
		}
	}

	return ""
}

func orDefault(v, def string) string {
	if v == "" {
		return def
	}

	return v
}

func (s *session) currentToken() string {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.token
}

func (s *session) setToken(token string) {
	s.mu.Lock()
	s.token = token
	s.mu.Unlock()
}

func (s *session) currentServer() string {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.server
}

func (s *session) loginWithRetry(ctx context.Context) error {
	backoff := 100 * time.Millisecond
	for {
		if err := s.login(ctx); err == nil {
			return nil
		} else if ctx.Err() != nil {
			return nil
		} else {
			s.logf("login failed: %v (retrying in %s)", err, backoff)
		}
		if !sleepCtx(ctx, backoff) {
			return nil
		}
		backoff *= 2
		if backoff > 2*time.Second {
			backoff = 2 * time.Second
		}
	}
}

func (s *session) login(ctx context.Context) error {
	s.loginMu.Lock()
	defer s.loginMu.Unlock()

	s.mu.Lock()
	server, productCode := s.server, s.productCode
	code, secret, dataDir := s.code, s.secret, s.dataDir
	s.mu.Unlock()
	if server == "" {
		return fmt.Errorf("config Server is empty")
	}

	hostname, _ := os.Hostname()
	username := envFirst("USERNAME", "USER")
	now := time.Now().UTC()
	req := protocol.LoginRequest{
		Code:        code,
		Secret:      secret,
		ProductCode: productCode,
		ClientId:    "127.0.0.1@mock",
		Node: &protocol.NodeInfo{
			Version:            "1.0.0-conformance",
			Compile:            protocol.FormatDateTime(s.startedAt),
			OSName:             runtime.GOOS,
			Architecture:       runtime.GOARCH,
			MachineName:        hostname,
			UserName:           username,
			IP:                 "127.0.0.1",
			ProcessorCount:     runtime.NumCPU(),
			Memory:             8 << 30,
			AvailableMemory:    4 << 30,
			FreeMemory:         2 << 30,
			TotalSize:          100 << 30,
			AvailableFreeSpace: 50 << 30,
			DriveInfo:          "/[mock]=50G/100G",
			UUID:               "mock-uuid",
			MachineGuid:        "mock-machine-guid",
			Macs:               "00-00-00-00-00-00",
			InstallPath:        dataDir,
			Runtime:            runtime.Version(),
			Framework:          runtime.Version(),
			TimeZone:           "UTC",
			Time:               protocol.FormatDateTime(now),
			Plugins:            []string{"agent:mock", "cap:exec=false", "cap:upgrade=false", "os:" + runtime.GOOS},
		},
	}
	body, err := json.Marshal(req)
	if err != nil {
		return err
	}

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, server+"/node/login", bytes.NewReader(body))
	if err != nil {
		return err
	}
	httpReq.Header.Set("Content-Type", "application/json")
	resp, err := s.client.Do(httpReq)
	if err != nil {
		return err
	}
	data, _ := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
	_ = resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("HTTP %d: %s", resp.StatusCode, protocol.Truncate(string(data), 300))
	}

	var login protocol.LoginResponse
	if err := json.Unmarshal(data, &login); err != nil {
		return fmt.Errorf("decode login response: %w", err)
	}
	if login.Token == "" {
		return fmt.Errorf("login response has no Token")
	}

	s.mu.Lock()
	s.token = login.Token
	changed := false
	if login.Code != "" && login.Code != s.code {
		s.code = login.Code
		changed = true
	}
	if login.Secret != "" && login.Secret != s.secret {
		s.secret = login.Secret
		changed = true
	}
	newCode, newSecret := s.code, s.secret
	s.mu.Unlock()

	if changed {
		if err := s.cfg.persistSession(newCode, newSecret); err != nil {
			s.logf("persist Code/Secret failed: %v", err)
		}
	}
	s.logf("login ok (code=%s)", newCode)

	return nil
}

func (s *session) refresh(ctx context.Context, stale string) error {
	s.mu.Lock()
	current := s.token
	s.mu.Unlock()
	if current != "" && current != stale {
		return nil
	}

	return s.login(ctx)
}

func (s *session) doJSON(ctx context.Context, method, path string, in any, out any) (int, []byte, error) {
	var body []byte
	if in != nil {
		var err error
		body, err = json.Marshal(in)
		if err != nil {
			return 0, nil, err
		}
	}

	for attempt := 0; attempt < 2; attempt++ {
		token := s.currentToken()
		server := s.currentServer()
		req, err := http.NewRequestWithContext(ctx, method, server+path, bytes.NewReader(body))
		if err != nil {
			return 0, nil, err
		}
		req.Header.Set("Content-Type", "application/json")
		if token != "" {
			req.Header.Set("Authorization", "Bearer "+token)
		}
		resp, err := s.client.Do(req)
		if err != nil {
			return 0, nil, err
		}
		data, _ := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
		_ = resp.Body.Close()

		if resp.StatusCode == http.StatusUnauthorized && attempt == 0 {
			if err := s.refresh(ctx, token); err != nil {
				return resp.StatusCode, data, fmt.Errorf("refresh token: %w", err)
			}
			continue
		}
		if resp.StatusCode < 200 || resp.StatusCode >= 300 {
			return resp.StatusCode, data, fmt.Errorf("%s %s: HTTP %d: %s", method, path, resp.StatusCode, protocol.Truncate(string(data), 300))
		}
		if out != nil && len(data) > 0 {
			if err := json.Unmarshal(data, out); err != nil {
				return resp.StatusCode, data, fmt.Errorf("decode %s: %w", path, err)
			}
		}

		return resp.StatusCode, data, nil
	}

	return 0, nil, fmt.Errorf("%s %s: unauthorized after re-login", method, path)
}

func (s *session) switchServer(ctx context.Context, newServer string) {
	newServer = strings.TrimRight(newServer, "/")
	if newServer == "" {
		return
	}
	s.mu.Lock()
	if s.server == newServer {
		s.mu.Unlock()

		return
	}
	s.server = newServer
	s.mu.Unlock()
	s.logf("switching server to %s", newServer)
	_ = s.cfg.persistServer(newServer)
	if err := s.login(ctx); err != nil {
		s.logf("login to new server failed: %v", err)
	}
}

func (s *session) pingLoop(ctx context.Context, handler *commandHandler) {
	period := time.Duration(s.metricInterval) * time.Second
	if period <= 0 {
		period = 60 * time.Second
	}
	for {
		s.pingOnce(ctx, handler, &period)
		if !sleepCtx(ctx, period) {
			return
		}
	}
}

func (s *session) pingOnce(ctx context.Context, handler *commandHandler, period *time.Duration) {
	info := protocol.PingInfo{
		Time:               protocol.NowMillis(),
		IP:                 "127.0.0.1",
		Gateway:            "127.0.0.1",
		Dns:                "127.0.0.1",
		Macs:               "00-00-00-00-00-00",
		DriveInfo:          "/[mock]=50G/100G",
		Framework:          runtime.Version(),
		Processes:          "mockagent",
		ProcessCount:       1,
		Memory:             8 << 30,
		AvailableMemory:    4 << 30,
		FreeMemory:         2 << 30,
		TotalSize:          100 << 30,
		AvailableFreeSpace: 50 << 30,
		Uptime:             int64(time.Since(s.startedAt).Seconds()),
	}
	var resp protocol.PingResponse
	if _, _, err := s.doJSON(ctx, http.MethodPost, "/node/ping", info, &resp); err != nil {
		if ctx.Err() == nil {
			s.logf("ping failed: %v", err)
		}

		return
	}
	if resp.Token != "" {
		s.setToken(resp.Token)
	}
	if resp.Period > 0 {
		*period = time.Duration(resp.Period) * time.Second
	}
	if resp.NewServer != "" {
		s.switchServer(ctx, resp.NewServer)
	}
	for _, cmd := range resp.Commands {
		cmd := cmd
		go handler.handle(ctx, cmd, "http")
	}
}

func (s *session) wsLoop(ctx context.Context, handler *commandHandler) {
	backoff := 100 * time.Millisecond
	for {
		if ctx.Err() != nil {
			return
		}
		server := s.currentServer()
		token := s.currentToken()
		conn, resp, err := s.dialWS(ctx, server, token)
		if err != nil {
			if ctx.Err() != nil {
				return
			}
			s.logf("ws connect failed: %v", err)
			if resp != nil && resp.StatusCode == http.StatusUnauthorized {
				if err := s.refresh(ctx, token); err != nil {
					s.logf("re-login after ws 401 failed: %v", err)
				}
			}
			if !sleepCtx(ctx, backoff) {
				return
			}
			backoff *= 2
			if backoff > 2*time.Second {
				backoff = 2 * time.Second
			}

			continue
		}
		backoff = 100 * time.Millisecond
		s.logf("ws connected")
		s.readWS(ctx, conn, handler)
		_ = conn.CloseNow()
		if ctx.Err() != nil {
			return
		}
		s.logf("ws disconnected; reconnecting")
		if !sleepCtx(ctx, 100*time.Millisecond) {
			return
		}
	}
}

func (s *session) dialWS(ctx context.Context, server, token string) (*websocket.Conn, *http.Response, error) {
	u, err := url.Parse(server)
	if err != nil {
		return nil, nil, err
	}
	switch u.Scheme {
	case "https":
		u.Scheme = "wss"
	case "http":
		u.Scheme = "ws"
	}
	u.Path = strings.TrimRight(u.Path, "/") + "/node/notify"
	header := http.Header{}
	header.Set("Authorization", "Bearer "+token)
	dialCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
	defer cancel()

	return websocket.Dial(dialCtx, u.String(), &websocket.DialOptions{HTTPHeader: header})
}

func (s *session) readWS(ctx context.Context, conn *websocket.Conn, handler *commandHandler) {
	var writeMu sync.Mutex
	writeText := func(payload string) error {
		writeMu.Lock()
		defer writeMu.Unlock()
		writeCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
		defer cancel()

		return conn.Write(writeCtx, websocket.MessageText, []byte(payload))
	}

	keepCtx, stopKeepalive := context.WithCancel(ctx)
	defer stopKeepalive()
	go func() {
		ticker := time.NewTicker(2 * time.Second)
		defer ticker.Stop()
		for {
			select {
			case <-keepCtx.Done():
				return
			case <-ticker.C:
				if err := writeText("Ping"); err != nil {
					return
				}
			}
		}
	}()

	for {
		typ, data, err := conn.Read(ctx)
		if err != nil {
			return
		}
		if typ != websocket.MessageText {
			continue
		}
		text := string(data)
		switch text {
		case "Ping":
			_ = writeText("Pong")
		case "Pong":
			// keepalive acknowledged
		default:
			var cmd protocol.CommandModel
			if err := json.Unmarshal(data, &cmd); err != nil || cmd.Id == 0 || cmd.Command == "" {
				s.logf("ignoring websocket text: %s", protocol.Truncate(text, 200))

				continue
			}
			go handler.handle(ctx, cmd, "ws")
		}
	}
}

func (s *session) eventLoop(ctx context.Context, queue *eventQueue) {
	backoff := 100 * time.Millisecond
	for {
		if ctx.Err() != nil {
			return
		}
		batch := queue.snapshot()
		if len(batch) == 0 {
			if !queue.wait(ctx) {
				return
			}

			continue
		}
		_, raw, err := s.doJSON(ctx, http.MethodPost, "/node/postevents", batch, nil)
		if err != nil {
			if ctx.Err() != nil {
				return
			}
			s.logf("post events failed: %v", err)
			if !sleepCtx(ctx, backoff) {
				return
			}
			backoff *= 2
			if backoff > 2*time.Second {
				backoff = 2 * time.Second
			}

			continue
		}
		count, perr := protocol.ParseEventCount(raw)
		if perr != nil {
			s.logf("post events response: %v", perr)
			count = len(batch)
		}
		if count <= 0 {
			count = len(batch)
		}
		queue.drop(count)
		backoff = 100 * time.Millisecond
	}
}

func sleepCtx(ctx context.Context, d time.Duration) bool {
	timer := time.NewTimer(d)
	defer timer.Stop()
	select {
	case <-ctx.Done():
		return false
	case <-timer.C:
		return true
	}
}
