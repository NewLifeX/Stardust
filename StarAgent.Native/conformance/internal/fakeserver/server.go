package fakeserver

import (
	"bytes"
	"context"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"strings"
	"sync"
	"time"

	"github.com/coder/websocket"

	"staragent-conformance/internal/protocol"
)

// Options configures the fake StarServer.
type Options struct {
	// Period is the heartbeat period in seconds returned by /node/ping.
	Period int
	// AutoPingInterval controls application-level "Ping" frames sent after a
	// WebSocket connects. Zero uses 500ms; a negative value disables them.
	AutoPingInterval time.Duration
	// DisableWS makes /node/notify return 503, forcing HTTP command fallback.
	DisableWS bool
	// EventsFailures makes the first N /node/postevents calls fail.
	EventsFailures int
	// EventsFailStatus is the status code used for those failures (default 500).
	EventsFailStatus int
	// EventsWrapped returns {"code":0,"data":n} instead of a bare integer.
	EventsWrapped bool
	// Upgrade is returned by /node/upgrade.
	Upgrade *protocol.UpgradeInfo
}

// Server is an in-process fake StarServer used by the conformance runner.
type Server struct {
	mu     sync.Mutex
	opts   Options
	ctx    context.Context
	cancel context.CancelFunc
	closed bool

	listener   net.Listener
	httpServer *http.Server
	url        string
	wg         sync.WaitGroup

	registered   bool
	issuedCode   string
	issuedSecret string
	token        string
	tokenSeq     int

	requests      []RequestRecord
	logins        []LoginRecord
	pings         []PingRecord
	replies       []ReplyRecord
	events        []EventRecord
	eventAttempts []EventAttempt
	upgrades      []UpgradeRecord
	logouts       []LogoutRecord
	deliveries    []DeliveryRecord
	frames        []FrameRecord

	httpCommands []protocol.CommandModel
	wsPending    []protocol.CommandModel

	wsConnections   int
	wsActive        int
	nextConnId      int
	conns           map[int]*wsConn
	pongs           int
	kicks           int
	eventsFail      int
	eventFailStatus int
	period          int
	newServer       string
	syncTime        int64
	rotateToken     bool
	upgradeInfo     *protocol.UpgradeInfo
	disableWS       bool
	eventsWrapped   bool
}

type wsConn struct {
	id      int
	conn    *websocket.Conn
	writeMu sync.Mutex
}

type statusWriter struct {
	http.ResponseWriter
	status int
}

func (w *statusWriter) WriteHeader(code int) {
	if w.status == 0 {
		w.status = code
	}
	w.ResponseWriter.WriteHeader(code)
}

func (w *statusWriter) Write(b []byte) (int, error) {
	if w.status == 0 {
		w.status = http.StatusOK
	}

	return w.ResponseWriter.Write(b)
}

// New creates a fake server. Call Start before using it.
func New(opts Options) *Server {
	if opts.AutoPingInterval == 0 {
		opts.AutoPingInterval = 500 * time.Millisecond
	}
	if opts.Period <= 0 {
		opts.Period = 1
	}
	failStatus := opts.EventsFailStatus
	if failStatus <= 0 {
		failStatus = http.StatusInternalServerError
	}
	ctx, cancel := context.WithCancel(context.Background())

	return &Server{
		opts:            opts,
		ctx:             ctx,
		cancel:          cancel,
		period:          opts.Period,
		eventsFail:      opts.EventsFailures,
		eventFailStatus: failStatus,
		upgradeInfo:     opts.Upgrade,
		disableWS:       opts.DisableWS,
		eventsWrapped:   opts.EventsWrapped,
		conns:           make(map[int]*wsConn),
	}
}

// Start listens on a random loopback port.
func (s *Server) Start() error {
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		return err
	}
	s.mu.Lock()
	s.listener = ln
	s.url = "http://" + ln.Addr().String()
	s.mu.Unlock()

	s.httpServer = &http.Server{
		Handler:           s,
		ReadHeaderTimeout: 5 * time.Second,
	}
	go func() {
		_ = s.httpServer.Serve(ln)
	}()

	return nil
}

// URL returns the base HTTP URL, for example http://127.0.0.1:54321.
func (s *Server) URL() string {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.url
}

// WSURL returns the ws:// URL of /node/notify.
func (s *Server) WSURL() string {
	return wsURLOf(s.URL())
}

func wsURLOf(raw string) string {
	u := strings.TrimRight(raw, "/")
	u = strings.Replace(u, "https://", "wss://", 1)
	u = strings.Replace(u, "http://", "ws://", 1)

	return u + "/node/notify"
}

// Close shuts down the HTTP server and all WebSocket connections.
func (s *Server) Close() error {
	s.mu.Lock()
	if s.closed {
		s.mu.Unlock()

		return nil
	}
	s.closed = true
	conns := make([]*wsConn, 0, len(s.conns))
	for _, c := range s.conns {
		conns = append(conns, c)
	}
	s.mu.Unlock()

	for _, c := range conns {
		_ = c.conn.CloseNow()
	}
	s.cancel()
	if s.httpServer != nil {
		_ = s.httpServer.Close()
	}

	done := make(chan struct{})
	go func() {
		s.wg.Wait()
		close(done)
	}()
	select {
	case <-done:
	case <-time.After(5 * time.Second):
	}

	return nil
}

// State returns a snapshot of everything the server observed.
func (s *Server) State() *State { return newState(s) }

// ServeHTTP routes node and control requests. Node paths are case-insensitive,
// matching ASP.NET Core routing.
func (s *Server) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	path := strings.ToLower(strings.TrimSuffix(r.URL.Path, "/"))
	if path == "" {
		path = "/"
	}

	if strings.HasPrefix(path, "/control/") {
		s.handleControl(w, r, path)

		return
	}

	if path == "/node/notify" {
		s.handleNotify(w, r)

		return
	}

	start := time.Now()
	body := readBody(r)
	sw := &statusWriter{ResponseWriter: w}
	defer func() {
		s.addRequest(r, body, sw.status, time.Since(start))
	}()

	switch path {
	case "/node/login":
		s.handleLogin(sw, r, body)
	case "/node/logout":
		s.handleLogout(sw, r)
	case "/node/ping":
		s.handlePing(sw, r, body)
	case "/node/upgrade":
		s.handleUpgrade(sw, r)
	case "/node/postevents":
		s.handlePostEvents(sw, r, body)
	case "/node/commandreply":
		s.handleCommandReply(sw, r, body)
	default:
		http.Error(sw, "not found", http.StatusNotFound)
	}
}

func readBody(r *http.Request) []byte {
	if r.Body == nil {
		return nil
	}
	body, _ := io.ReadAll(io.LimitReader(r.Body, 1<<20))
	_ = r.Body.Close()
	r.Body = io.NopCloser(bytes.NewReader(body))

	return body
}

func (s *Server) addRequest(r *http.Request, body []byte, status int, elapsed time.Duration) {
	if status == 0 {
		status = http.StatusOK
	}
	rec := RequestRecord{
		At:       time.Now().UTC(),
		Method:   r.Method,
		Path:     strings.ToLower(r.URL.Path),
		Query:    r.URL.RawQuery,
		Body:     protocol.Truncate(string(body), 16*1024),
		Status:   status,
		HasAuth:  protocol.BearerToken(r) != "",
		Remote:   r.RemoteAddr,
		Duration: elapsed.Milliseconds(),
	}
	s.mu.Lock()
	s.requests = append(s.requests, rec)
	s.mu.Unlock()
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}

func writeError(w http.ResponseWriter, status int, message string) {
	writeJSON(w, status, map[string]string{"error": message})
}

func (s *Server) authenticate(r *http.Request) bool {
	token := protocol.BearerToken(r)
	if token == "" {
		return false
	}
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.token != "" && token == s.token
}

func (s *Server) handleLogin(w http.ResponseWriter, r *http.Request, body []byte) {
	if r.Method != http.MethodPost {
		writeError(w, http.StatusMethodNotAllowed, "POST required")

		return
	}

	var req protocol.LoginRequest
	if err := json.Unmarshal(body, &req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid login body: "+err.Error())

		return
	}

	now := time.Now().UTC()
	rec := LoginRecord{
		At:          now,
		Code:        req.Code,
		Secret:      req.Secret,
		ProductCode: req.ProductCode,
		ClientId:    req.ClientId,
		Project:     req.Project,
		Node:        req.Node,
		Remote:      r.RemoteAddr,
		Status:      http.StatusOK,
	}

	var resp protocol.LoginResponse
	s.mu.Lock()
	switch {
	case !s.registered:
		switch {
		case req.Code == "" && req.Secret == "":
			s.issuedCode = "CONF0001"
			s.issuedSecret = newSecret()
			s.registered = true
			resp.Code = s.issuedCode
			resp.Secret = s.issuedSecret
		case req.Code == "" || req.Secret == "":
			rec.Status = http.StatusBadRequest
			rec.Error = "Code and Secret must be supplied together"
		default:
			s.issuedCode = req.Code
			s.issuedSecret = req.Secret
			s.registered = true
			resp.Code = s.issuedCode
			resp.Secret = s.issuedSecret
		}
	case req.Code != s.issuedCode || req.Secret != s.issuedSecret:
		rec.Status = http.StatusUnauthorized
		rec.Error = "invalid node code or secret"
	}

	if rec.Status == http.StatusOK {
		s.tokenSeq++
		s.token = fmt.Sprintf("conf-token-%d", s.tokenSeq)
		name := req.Node.GetMachineName()
		if name == "" {
			name, _, _ = net.SplitHostPort(r.RemoteAddr)
		}
		resp.Name = name
		resp.Token = s.token
		resp.Expire = 7200
		resp.Time = 0
		resp.ServerTime = now.UnixMilli()
		rec.ResponseCode = resp.Code
		rec.ResponseSecret = resp.Secret
		rec.ResponseToken = resp.Token
	}
	s.logins = append(s.logins, rec)
	s.mu.Unlock()

	if rec.Status != http.StatusOK {
		writeError(w, rec.Status, rec.Error)

		return
	}
	writeJSON(w, http.StatusOK, resp)
}

func (s *Server) handleLogout(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		writeError(w, http.StatusMethodNotAllowed, "POST required")

		return
	}
	rec := LogoutRecord{At: time.Now().UTC(), Reason: r.URL.Query().Get("reason")}
	if !s.authenticate(r) {
		rec.Status = http.StatusUnauthorized
		s.mu.Lock()
		s.logouts = append(s.logouts, rec)
		s.mu.Unlock()
		writeError(w, http.StatusUnauthorized, "invalid token")

		return
	}
	rec.Status = http.StatusOK
	s.mu.Lock()
	s.logouts = append(s.logouts, rec)
	s.mu.Unlock()
	writeJSON(w, http.StatusOK, protocol.LoginResponse{Name: "conformance-node", ServerTime: protocol.NowMillis()})
}

func (s *Server) handlePing(w http.ResponseWriter, r *http.Request, body []byte) {
	if r.Method != http.MethodPost {
		writeError(w, http.StatusMethodNotAllowed, "POST required")

		return
	}
	if !s.authenticate(r) {
		writeError(w, http.StatusUnauthorized, "invalid token")

		return
	}

	var info protocol.PingInfo
	if err := json.Unmarshal(body, &info); err != nil {
		writeError(w, http.StatusBadRequest, "invalid ping body: "+err.Error())

		return
	}

	now := time.Now().UTC()
	resp := protocol.PingResponse{
		Time:       0,
		ServerTime: now.UnixMilli(),
		Period:     s.period,
	}
	rec := PingRecord{
		At:           now,
		TimeField:    info.Time,
		IP:           info.IP,
		CpuRate:      info.CpuRate,
		Memory:       info.Memory,
		ProcessCount: info.ProcessCount,
		Status:       http.StatusOK,
	}

	s.mu.Lock()
	resp.Commands = append(resp.Commands, s.httpCommands...)
	s.httpCommands = nil
	resp.NewServer = s.newServer
	resp.SyncTime = s.syncTime
	if s.rotateToken {
		s.tokenSeq++
		s.token = fmt.Sprintf("conf-token-%d", s.tokenSeq)
		resp.Token = s.token
		s.rotateToken = false
	}
	for _, cmd := range resp.Commands {
		rec.CommandIds = append(rec.CommandIds, cmd.Id)
	}
	s.pings = append(s.pings, rec)
	s.mu.Unlock()

	writeJSON(w, http.StatusOK, resp)
}

func (s *Server) handleCommandReply(w http.ResponseWriter, r *http.Request, body []byte) {
	if r.Method != http.MethodPost {
		writeError(w, http.StatusMethodNotAllowed, "POST required")

		return
	}
	if !s.authenticate(r) {
		writeError(w, http.StatusUnauthorized, "invalid token")

		return
	}

	var reply protocol.CommandReplyModel
	if err := json.Unmarshal(body, &reply); err != nil {
		writeError(w, http.StatusBadRequest, "invalid reply body: "+err.Error())

		return
	}
	s.mu.Lock()
	s.replies = append(s.replies, ReplyRecord{At: time.Now().UTC(), Reply: reply, Status: http.StatusOK})
	s.mu.Unlock()

	writeJSON(w, http.StatusOK, true)
}

func (s *Server) handlePostEvents(w http.ResponseWriter, r *http.Request, body []byte) {
	if r.Method != http.MethodPost {
		writeError(w, http.StatusMethodNotAllowed, "POST required")

		return
	}
	if !s.authenticate(r) {
		writeError(w, http.StatusUnauthorized, "invalid token")

		return
	}

	var events []protocol.EventModel
	if err := json.Unmarshal(body, &events); err != nil {
		writeError(w, http.StatusBadRequest, "invalid event body: "+err.Error())

		return
	}

	now := time.Now().UTC()
	s.mu.Lock()
	attempt := EventAttempt{At: now, Count: len(events), Events: append([]protocol.EventModel(nil), events...)}
	if s.eventsFail > 0 {
		s.eventsFail--
		attempt.Status = s.eventFailStatus
		attempt.Error = "configured failure"
		s.eventAttempts = append(s.eventAttempts, attempt)
		status := s.eventFailStatus
		s.mu.Unlock()
		writeError(w, status, attempt.Error)

		return
	}
	attempt.Status = http.StatusOK
	s.eventAttempts = append(s.eventAttempts, attempt)
	for _, e := range events {
		s.events = append(s.events, EventRecord{At: now, Event: e, BatchSize: len(events)})
	}
	wrapped := s.eventsWrapped
	s.mu.Unlock()

	if wrapped {
		writeJSON(w, http.StatusOK, map[string]any{"code": 0, "data": len(events), "msg": ""})
	} else {
		writeJSON(w, http.StatusOK, len(events))
	}
}

func (s *Server) handleUpgrade(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		writeError(w, http.StatusMethodNotAllowed, "POST required")

		return
	}
	rec := UpgradeRecord{At: time.Now().UTC(), Channel: r.URL.Query().Get("channel")}
	if !s.authenticate(r) {
		rec.Status = http.StatusUnauthorized
		s.mu.Lock()
		s.upgrades = append(s.upgrades, rec)
		s.mu.Unlock()
		writeError(w, http.StatusUnauthorized, "invalid token")

		return
	}
	rec.Status = http.StatusOK
	s.mu.Lock()
	s.upgrades = append(s.upgrades, rec)
	info := s.upgradeInfo
	s.mu.Unlock()

	if info == nil {
		writeJSON(w, http.StatusOK, map[string]any{})

		return
	}
	writeJSON(w, http.StatusOK, info)
}

func (s *Server) handleNotify(w http.ResponseWriter, r *http.Request) {
	s.wg.Add(1)
	defer s.wg.Done()

	start := time.Now()
	if r.Method != http.MethodGet {
		http.Error(w, "GET required", http.StatusMethodNotAllowed)
		s.addRequest(r, nil, http.StatusMethodNotAllowed, time.Since(start))

		return
	}
	if s.isWSDisabled() {
		http.Error(w, "websocket disabled by scenario", http.StatusServiceUnavailable)
		s.addRequest(r, nil, http.StatusServiceUnavailable, time.Since(start))

		return
	}
	if !s.authenticate(r) {
		http.Error(w, "invalid token", http.StatusUnauthorized)
		s.addRequest(r, nil, http.StatusUnauthorized, time.Since(start))

		return
	}

	conn, err := websocket.Accept(w, r, &websocket.AcceptOptions{InsecureSkipVerify: true})
	if err != nil {
		s.addRequest(r, nil, http.StatusBadRequest, time.Since(start))

		return
	}
	conn.SetReadLimit(1 << 20)
	s.addRequest(r, nil, http.StatusSwitchingProtocols, time.Since(start))

	s.mu.Lock()
	s.nextConnId++
	id := s.nextConnId
	wc := &wsConn{id: id, conn: conn}
	s.wsConnections++
	s.wsActive++
	s.conns[id] = wc
	pending := append([]protocol.CommandModel(nil), s.wsPending...)
	s.wsPending = nil
	pingInterval := s.opts.AutoPingInterval
	serverCtx := s.ctx
	s.mu.Unlock()

	defer func() {
		s.mu.Lock()
		delete(s.conns, id)
		if s.wsActive > 0 {
			s.wsActive--
		}
		s.mu.Unlock()
		_ = conn.CloseNow()
	}()

	connCtx, cancel := context.WithCancel(serverCtx)
	defer cancel()

	for _, cmd := range pending {
		_ = s.sendCommand(wc, cmd)
	}

	if pingInterval > 0 {
		s.wg.Add(1)
		go func() {
			defer s.wg.Done()
			ticker := time.NewTicker(pingInterval)
			defer ticker.Stop()
			for {
				select {
				case <-connCtx.Done():
					return
				case <-ticker.C:
					if err := s.writeFrame(wc, "Ping"); err != nil {
						cancel()

						return
					}
				}
			}
		}()
	}

	for {
		typ, data, err := conn.Read(connCtx)
		if err != nil {
			return
		}
		switch typ {
		case websocket.MessageText:
			text := string(data)
			s.recordFrame(wc.id, "recv", "text", text)
			switch text {
			case "Ping":
				_ = s.writeFrame(wc, "Pong")
			case "Pong":
				s.mu.Lock()
				s.pongs++
				s.mu.Unlock()
			default:
				// Agents do not normally send JSON; keep it in the frame log.
			}
		case websocket.MessageBinary:
			s.recordFrame(wc.id, "recv", "binary", fmt.Sprintf("<%d bytes>", len(data)))
		}
	}
}

func (s *Server) isWSDisabled() bool {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.disableWS
}

func (s *Server) writeFrame(wc *wsConn, payload string) error {
	wc.writeMu.Lock()
	defer wc.writeMu.Unlock()

	ctx, cancel := context.WithTimeout(s.ctx, 5*time.Second)
	defer cancel()
	if err := wc.conn.Write(ctx, websocket.MessageText, []byte(payload)); err != nil {
		return err
	}
	s.recordFrame(wc.id, "sent", "text", payload)

	return nil
}

func (s *Server) sendCommand(wc *wsConn, cmd protocol.CommandModel) error {
	data, err := json.Marshal(cmd)
	if err != nil {
		return err
	}
	wc.writeMu.Lock()
	defer wc.writeMu.Unlock()

	ctx, cancel := context.WithTimeout(s.ctx, 5*time.Second)
	defer cancel()
	if err := wc.conn.Write(ctx, websocket.MessageText, data); err != nil {
		return err
	}
	s.recordFrame(wc.id, "sent", "text", protocol.Truncate(string(data), 4*1024))

	return nil
}

func (s *Server) recordFrame(connId int, dir, typ, payload string) {
	rec := FrameRecord{
		At:      time.Now().UTC(),
		ConnId:  connId,
		Dir:     dir,
		Type:    typ,
		Payload: protocol.Truncate(payload, 4*1024),
	}
	s.mu.Lock()
	s.frames = append(s.frames, rec)
	s.mu.Unlock()
}

func newSecret() string {
	buf := make([]byte, 8)
	if _, err := rand.Read(buf); err != nil {
		return "conf-secret-fallback"
	}

	return "conf-secret-" + hex.EncodeToString(buf)
}
