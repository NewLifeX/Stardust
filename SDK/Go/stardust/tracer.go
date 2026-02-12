package stardust

import (
	"bytes"
	"compress/gzip"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"os"
	"sync"
	"time"
)

// Span 追踪片段
type Span struct {
	ID        string `json:"Id"`
	ParentID  string `json:"ParentId"`
	TraceID   string `json:"TraceId"`
	StartTime int64  `json:"StartTime"`
	EndTime   int64  `json:"EndTime"`
	Tag       string `json:"Tag"`
	Error     string `json:"Error"`

	name   string
	tracer *Tracer
}

// SetError 设置错误
func (s *Span) SetError(err error) {
	if err != nil {
		s.Error = err.Error()
	}
}

// Finish 结束片段
func (s *Span) Finish() {
	s.EndTime = time.Now().UnixMilli()
	if s.tracer != nil {
		s.tracer.finishSpan(s)
	}
}

// SpanBuilder 构建器，按操作名聚合
type SpanBuilder struct {
	Name         string  `json:"Name"`
	StartTime    int64   `json:"StartTime"`
	EndTime      int64   `json:"EndTime"`
	Total        int     `json:"Total"`
	Errors       int     `json:"Errors"`
	Cost         int64   `json:"Cost"`
	MaxCost      int     `json:"MaxCost"`
	MinCost      int     `json:"MinCost"`
	Samples      []*Span `json:"Samples"`
	ErrorSamples []*Span `json:"ErrorSamples"`

	maxSamples int
	maxErrors  int
	mu         sync.Mutex
}

func newSpanBuilder(name string, maxSamples, maxErrors int) *SpanBuilder {
	return &SpanBuilder{
		Name:         name,
		StartTime:    time.Now().UnixMilli(),
		Samples:      make([]*Span, 0),
		ErrorSamples: make([]*Span, 0),
		maxSamples:   maxSamples,
		maxErrors:    maxErrors,
	}
}

func (b *SpanBuilder) addSpan(span *Span) {
	elapsed := int(span.EndTime - span.StartTime)

	b.mu.Lock()
	defer b.mu.Unlock()

	b.Total++
	b.Cost += int64(elapsed)
	if b.MaxCost == 0 || elapsed > b.MaxCost {
		b.MaxCost = elapsed
	}
	if b.MinCost == 0 || elapsed < b.MinCost {
		b.MinCost = elapsed
	}

	if span.Error != "" {
		b.Errors++
		if len(b.ErrorSamples) < b.maxErrors {
			b.ErrorSamples = append(b.ErrorSamples, span)
		}
	} else {
		if len(b.Samples) < b.maxSamples {
			b.Samples = append(b.Samples, span)
		}
	}
	b.EndTime = time.Now().UnixMilli()
}

// TraceModel 上报请求体
type TraceModel struct {
	AppID    string         `json:"AppId"`
	AppName  string         `json:"AppName"`
	ClientID string         `json:"ClientId"`
	Version  string         `json:"Version,omitempty"`
	Builders []*SpanBuilder `json:"Builders"`
}

// TraceResponse 上报响应
type TraceResponse struct {
	Period           int      `json:"Period"`
	MaxSamples       int      `json:"MaxSamples"`
	MaxErrors        int      `json:"MaxErrors"`
	Timeout          int      `json:"Timeout"`
	MaxTagLength     int      `json:"MaxTagLength"`
	RequestTagLength int      `json:"RequestTagLength"`
	EnableMeter      *bool    `json:"EnableMeter"`
	Excludes         []string `json:"Excludes"`
}

// APIResponse 通用响应
type APIResponse struct {
	Code int             `json:"code"`
	Data json.RawMessage `json:"data"`
}

// LoginResponse 登录响应
type LoginResponse struct {
	Code       string `json:"Code"`
	Secret     string `json:"Secret"`
	Name       string `json:"Name"`
	Token      string `json:"Token"`
	Expire     int    `json:"Expire"`
	ServerTime int64  `json:"ServerTime"`
}

// PingResponse 心跳响应
type PingResponse struct {
	Time       int64  `json:"Time"`
	ServerTime int64  `json:"ServerTime"`
	Period     int    `json:"Period"`
	Token      string `json:"Token"`
}

// Tracer 星尘追踪器
type Tracer struct {
	Server   string
	AppID    string
	AppName  string
	Secret   string
	ClientID string

	// 采样参数
	Period      int
	MaxSamples  int
	MaxErrors   int
	Timeout     int
	MaxTagLen   int
	Excludes    []string
	EnableMeter bool

	token       string
	tokenExpire int64

	builders map[string]*SpanBuilder
	mu       sync.Mutex
	running  bool
	stopCh   chan struct{}
	client   *http.Client
}

// NewTracer 创建追踪器
func NewTracer(server, appID, secret string) *Tracer {
	return &Tracer{
		Server:      server,
		AppID:       appID,
		AppName:     appID,
		Secret:      secret,
		ClientID:    fmt.Sprintf("%s@%d", getLocalIP(), os.Getpid()),
		Period:      60,
		MaxSamples:  1,
		MaxErrors:   10,
		Timeout:     5000,
		MaxTagLen:   1024,
		EnableMeter: true,
		builders:    make(map[string]*SpanBuilder),
		stopCh:      make(chan struct{}),
		client:      &http.Client{Timeout: 10 * time.Second},
	}
}

// Start 启动追踪器
func (t *Tracer) Start() {
	t.login()
	t.running = true
	go t.reportLoop()
	go t.pingLoop()
}

// Stop 停止追踪器
func (t *Tracer) Stop() {
	t.running = false
	close(t.stopCh)
	t.flush()
}

// NewSpan 创建追踪片段
func (t *Tracer) NewSpan(name, parentID string) *Span {
	return &Span{
		ID:        randomHex(8),
		ParentID:  parentID,
		TraceID:   randomHex(16),
		StartTime: time.Now().UnixMilli(),
		name:      name,
		tracer:    t,
	}
}

func (t *Tracer) finishSpan(span *Span) {
	// 排除自身
	if span.name == "/Trace/Report" || span.name == "/Trace/ReportRaw" {
		return
	}
	for _, exc := range t.Excludes {
		if exc != "" && containsIgnoreCase(span.name, exc) {
			return
		}
	}

	// 截断 Tag
	if len(span.Tag) > t.MaxTagLen {
		span.Tag = span.Tag[:t.MaxTagLen]
	}

	t.mu.Lock()
	defer t.mu.Unlock()

	builder, ok := t.builders[span.name]
	if !ok {
		builder = newSpanBuilder(span.name, t.MaxSamples, t.MaxErrors)
		t.builders[span.name] = builder
	}
	builder.addSpan(span)
}

func (t *Tracer) login() {
	payload := map[string]interface{}{
		"AppId":    t.AppID,
		"Secret":   t.Secret,
		"ClientId": t.ClientID,
		"AppName":  t.AppName,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return
	}

	resp, err := t.client.Post(t.Server+"/App/Login", "application/json", bytes.NewReader(body))
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Login failed: %v\n", err)
		return
	}
	defer resp.Body.Close()

	var apiResp APIResponse
	if err := json.NewDecoder(resp.Body).Decode(&apiResp); err != nil || apiResp.Code != 0 {
		return
	}

	var loginResp LoginResponse
	if err := json.Unmarshal(apiResp.Data, &loginResp); err != nil {
		return
	}

	t.token = loginResp.Token
	if loginResp.Expire > 0 {
		t.tokenExpire = time.Now().Unix() + int64(loginResp.Expire)
	}
	if loginResp.Code != "" {
		t.AppID = loginResp.Code
	}
	if loginResp.Secret != "" {
		t.Secret = loginResp.Secret
	}
}

func (t *Tracer) ping() {
	payload := map[string]interface{}{
		"Id":   os.Getpid(),
		"Name": t.AppName,
		"Time": time.Now().UnixMilli(),
	}

	body, err := json.Marshal(payload)
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Ping marshal failed: %v\n", err)
		return
	}
	reqURL := fmt.Sprintf("%s/App/Ping?Token=%s", t.Server, url.QueryEscape(t.token))

	resp, err := t.client.Post(reqURL, "application/json", bytes.NewReader(body))
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Ping failed: %v\n", err)
		return
	}
	defer resp.Body.Close()

	var apiResp APIResponse
	if err := json.NewDecoder(resp.Body).Decode(&apiResp); err != nil || apiResp.Code != 0 {
		return
	}

	var pingResp PingResponse
	if err := json.Unmarshal(apiResp.Data, &pingResp); err != nil {
		return
	}

	if pingResp.Token != "" {
		t.token = pingResp.Token
	}
}

func (t *Tracer) report(buildersData []*SpanBuilder) {
	model := TraceModel{
		AppID:    t.AppID,
		AppName:  t.AppName,
		ClientID: t.ClientID,
		Builders: buildersData,
	}

	body, err := json.Marshal(model)
	if err != nil {
		return
	}

	var resp *http.Response
	if len(body) > 1024 {
		// Gzip 压缩
		var buf bytes.Buffer
		gz := gzip.NewWriter(&buf)
		if _, err := gz.Write(body); err != nil {
			fmt.Fprintf(os.Stderr, "[Stardust] Gzip write failed: %v\n", err)
			return
		}
		if err := gz.Close(); err != nil {
			fmt.Fprintf(os.Stderr, "[Stardust] Gzip close failed: %v\n", err)
			return
		}

		reqURL := fmt.Sprintf("%s/Trace/ReportRaw?Token=%s", t.Server, url.QueryEscape(t.token))
		resp, err = t.client.Post(reqURL, "application/x-gzip", &buf)
	} else {
		reqURL := fmt.Sprintf("%s/Trace/Report?Token=%s", t.Server, url.QueryEscape(t.token))
		resp, err = t.client.Post(reqURL, "application/json", bytes.NewReader(body))
	}

	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Report failed: %v\n", err)
		return
	}
	defer resp.Body.Close()

	respBody, err := io.ReadAll(resp.Body)
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Report read response failed: %v\n", err)
		return
	}
	var apiResp APIResponse
	if err := json.Unmarshal(respBody, &apiResp); err != nil || apiResp.Code != 0 {
		return
	}

	var traceResp TraceResponse
	if err := json.Unmarshal(apiResp.Data, &traceResp); err != nil {
		return
	}

	if traceResp.Period > 0 {
		t.Period = traceResp.Period
	}
	if traceResp.MaxSamples > 0 {
		t.MaxSamples = traceResp.MaxSamples
	}
	if traceResp.MaxErrors > 0 {
		t.MaxErrors = traceResp.MaxErrors
	}
	if traceResp.Timeout > 0 {
		t.Timeout = traceResp.Timeout
	}
	if traceResp.MaxTagLength > 0 {
		t.MaxTagLen = traceResp.MaxTagLength
	}
	if traceResp.Excludes != nil {
		t.Excludes = traceResp.Excludes
	}
}

func (t *Tracer) flush() {
	t.mu.Lock()
	if len(t.builders) == 0 {
		t.mu.Unlock()
		return
	}
	list := make([]*SpanBuilder, 0, len(t.builders))
	for _, b := range t.builders {
		if b.Total > 0 {
			list = append(list, b)
		}
	}
	t.builders = make(map[string]*SpanBuilder)
	t.mu.Unlock()

	if len(list) > 0 {
		t.report(list)
	}
}

func (t *Tracer) reportLoop() {
	ticker := time.NewTicker(time.Duration(t.Period) * time.Second)
	defer ticker.Stop()
	for {
		select {
		case <-ticker.C:
			t.flush()
		case <-t.stopCh:
			return
		}
	}
}

func (t *Tracer) pingLoop() {
	ticker := time.NewTicker(30 * time.Second)
	defer ticker.Stop()
	for {
		select {
		case <-ticker.C:
			t.ping()
		case <-t.stopCh:
			return
		}
	}
}

// ========== 工具函数 ==========

func randomHex(n int) string {
	b := make([]byte, n)
	if _, err := rand.Read(b); err != nil {
		// Fallback: use timestamp-based pseudo-random
		// This is not cryptographically secure but ensures we have valid data
		ts := time.Now().UnixNano()
		for i := 0; i < n; i++ {
			b[i] = byte(ts >> (8 * (i % 8)))
			if i%8 == 7 {
				ts = ts * 31 + int64(i) // Simple mixing
			}
		}
	}
	return hex.EncodeToString(b)
}

func getLocalIP() string {
	conn, err := net.Dial("udp", "8.8.8.8:80")
	if err != nil {
		return "127.0.0.1"
	}
	defer conn.Close()
	return conn.LocalAddr().(*net.UDPAddr).IP.String()
}

func containsIgnoreCase(s, substr string) bool {
	return len(s) >= len(substr) &&
		bytes.Contains(bytes.ToLower([]byte(s)), bytes.ToLower([]byte(substr)))
}
