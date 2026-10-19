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
	"strings"
	"sync"
	"time"
)

const (
	// DefaultVersion 默认版本号。服务端只有收到 Version 才下发 Excludes/EnableMeter
	DefaultVersion = "1.0"
	// defaultTokenExpire 登录响应未返回有效期时的默认令牌有效期（秒）
	defaultTokenExpire = 7200
	// tokenRefreshAhead 令牌到期前提前重新登录的时间（秒）
	tokenRefreshAhead = 300
	// pingInterval 心跳间隔
	pingInterval = 30 * time.Second
	// reportRawThreshold 超过该字节数的上报使用 GZip 压缩
	reportRawThreshold = 1024
	// defaultPeriod 默认采样周期（秒）
	defaultPeriod = 60
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
	Info     *AppInfo       `json:"Info,omitempty"`
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
	Code    int             `json:"code"`
	Message string          `json:"message"`
	Data    json.RawMessage `json:"data"`
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
	// Version 应用版本号。服务端只有收到版本号才下发 Excludes/EnableMeter
	Version string
	// IP 本机 IP 地址。为空时自动枚举网卡
	IP string

	// 采样参数
	Period      int
	MaxSamples  int
	MaxErrors   int
	Timeout     int
	MaxTagLen   int
	Excludes    []string
	EnableMeter bool

	startTime time.Time

	token       string
	tokenExpire int64

	builders map[string]*SpanBuilder
	mu       sync.Mutex

	// stateMu 保护身份信息、采样参数、令牌与 HTTP 客户端
	stateMu sync.RWMutex
	client  *http.Client

	gcMu   sync.Mutex
	lastGC uint32

	loginMu sync.Mutex

	stopCh    chan struct{}
	stopOnce  sync.Once
	startOnce sync.Once
	wg        sync.WaitGroup
}

// NewTracer 创建追踪器
func NewTracer(server, appID, secret string) *Tracer {
	ip := getLocalIP()
	return &Tracer{
		Server:      server,
		AppID:       appID,
		AppName:     appID,
		Secret:      secret,
		ClientID:    fmt.Sprintf("%s@%d", ip, os.Getpid()),
		Version:     DefaultVersion,
		IP:          ip,
		Period:      defaultPeriod,
		MaxSamples:  1,
		MaxErrors:   10,
		Timeout:     5000,
		MaxTagLen:   1024,
		EnableMeter: true,
		startTime:   time.Now(),
		builders:    make(map[string]*SpanBuilder),
		stopCh:      make(chan struct{}),
		client:      &http.Client{Timeout: 10 * time.Second},
	}
}

// SetVersion 设置应用版本号，返回自身以支持链式调用
func (t *Tracer) SetVersion(version string) *Tracer {
	t.stateMu.Lock()
	t.Version = version
	t.stateMu.Unlock()
	return t
}

// SetAppName 设置应用名，返回自身以支持链式调用
func (t *Tracer) SetAppName(name string) *Tracer {
	t.stateMu.Lock()
	t.AppName = name
	t.stateMu.Unlock()
	return t
}

// SetIP 设置本机 IP 地址，返回自身以支持链式调用
func (t *Tracer) SetIP(ip string) *Tracer {
	t.stateMu.Lock()
	t.IP = ip
	t.stateMu.Unlock()
	return t
}

// SetEnableMeter 设置是否采集并上报应用性能信息，返回自身以支持链式调用
func (t *Tracer) SetEnableMeter(enable bool) *Tracer {
	t.stateMu.Lock()
	t.EnableMeter = enable
	t.stateMu.Unlock()
	return t
}

// SetPeriod 设置采样周期（秒），返回自身以支持链式调用
func (t *Tracer) SetPeriod(seconds int) *Tracer {
	t.stateMu.Lock()
	t.Period = seconds
	t.stateMu.Unlock()
	return t
}

// SetHTTPClient 设置 HTTP 客户端，返回自身以支持链式调用
func (t *Tracer) SetHTTPClient(client *http.Client) *Tracer {
	t.stateMu.Lock()
	t.client = client
	t.stateMu.Unlock()
	return t
}

// Start 启动追踪器
func (t *Tracer) Start() {
	t.startOnce.Do(func() {
		t.login()
		t.wg.Add(2)
		go t.reportLoop()
		go t.pingLoop()
	})
}

// Stop 停止追踪器
func (t *Tracer) Stop() {
	t.stopOnce.Do(func() {
		close(t.stopCh)
	})
	t.wg.Wait()
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

	t.stateMu.RLock()
	excludes := t.Excludes
	maxTagLen := t.MaxTagLen
	maxSamples := t.MaxSamples
	maxErrors := t.MaxErrors
	t.stateMu.RUnlock()

	for _, exc := range excludes {
		if exc != "" && containsIgnoreCase(span.name, exc) {
			return
		}
	}

	// 截断 Tag
	if maxTagLen > 0 && len(span.Tag) > maxTagLen {
		span.Tag = span.Tag[:maxTagLen]
	}

	t.mu.Lock()
	defer t.mu.Unlock()

	builder, ok := t.builders[span.name]
	if !ok {
		builder = newSpanBuilder(span.name, maxSamples, maxErrors)
		t.builders[span.name] = builder
	}
	builder.addSpan(span)
}

func (t *Tracer) login() bool {
	t.loginMu.Lock()
	defer t.loginMu.Unlock()

	appID, appName, secret, version, ip, clientID := t.credentials()
	payload := map[string]interface{}{
		"AppId":    appID,
		"Secret":   secret,
		"ClientId": clientID,
		"AppName":  appName,
		"Version":  version,
		"IP":       ip,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Login marshal failed: %v\n", err)
		return false
	}

	apiResp, status, err := t.post("/App/Login", "application/json", body, "")
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Login failed: %v\n", err)
		return false
	}
	if status == http.StatusUnauthorized || apiResp == nil || apiResp.Code != 0 {
		return false
	}

	var loginResp LoginResponse
	if err := json.Unmarshal(apiResp.Data, &loginResp); err != nil {
		return false
	}
	if loginResp.Token == "" {
		return false
	}

	expire := int64(loginResp.Expire)
	if expire <= 0 {
		expire = defaultTokenExpire
	}

	t.stateMu.Lock()
	t.token = loginResp.Token
	t.tokenExpire = time.Now().Unix() + expire
	if loginResp.Code != "" {
		t.AppID = loginResp.Code
	}
	if loginResp.Secret != "" {
		t.Secret = loginResp.Secret
	}
	t.stateMu.Unlock()

	return true
}

func (t *Tracer) ping() {
	apiResp, err := t.postJSON("/App/Ping", t.appInfo())
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Ping failed: %v\n", err)
		return
	}
	if apiResp == nil || apiResp.Code != 0 {
		return
	}

	var pingResp PingResponse
	if err := json.Unmarshal(apiResp.Data, &pingResp); err != nil {
		return
	}

	// 服务端可能在令牌即将过期时下发新令牌
	if pingResp.Token != "" {
		t.setToken(pingResp.Token)
	}
}

func (t *Tracer) report(buildersData []*SpanBuilder) {
	appID, appName, _, version, _, clientID := t.credentials()
	model := TraceModel{
		AppID:    appID,
		AppName:  appName,
		ClientID: clientID,
		Version:  version,
		Builders: buildersData,
	}
	if t.getEnableMeter() {
		info := t.appInfo()
		model.Info = &info
	}

	body, err := json.Marshal(model)
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Report marshal failed: %v\n", err)
		return
	}

	path := "/Trace/Report"
	contentType := "application/json"
	if len(body) > reportRawThreshold {
		compressed, err := gzipData(body)
		if err != nil {
			fmt.Fprintf(os.Stderr, "[Stardust] Gzip failed: %v\n", err)
			return
		}
		body = compressed
		path = "/Trace/ReportRaw"
		contentType = "application/x-gzip"
	}

	apiResp, err := t.postWithAuth(path, contentType, body)
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Report failed: %v\n", err)
		return
	}
	if apiResp == nil || apiResp.Code != 0 {
		return
	}

	var traceResp TraceResponse
	if err := json.Unmarshal(apiResp.Data, &traceResp); err != nil {
		return
	}

	t.applyTraceResponse(&traceResp)
}

func (t *Tracer) applyTraceResponse(traceResp *TraceResponse) {
	t.stateMu.Lock()
	defer t.stateMu.Unlock()

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
	// RequestTagLength 由 HTTP 采集层使用，本 SDK 忽略
	if traceResp.EnableMeter != nil {
		t.EnableMeter = *traceResp.EnableMeter
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
	defer t.wg.Done()

	timer := time.NewTimer(t.periodDuration())
	defer timer.Stop()
	for {
		select {
		case <-timer.C:
			t.flush()
			timer.Reset(t.periodDuration())
		case <-t.stopCh:
			return
		}
	}
}

func (t *Tracer) pingLoop() {
	defer t.wg.Done()

	timer := time.NewTimer(pingInterval)
	defer timer.Stop()
	for {
		select {
		case <-timer.C:
			t.ping()
			timer.Reset(pingInterval)
		case <-t.stopCh:
			return
		}
	}
}

// ========== 内部辅助 ==========

// credentials 返回登录与上报所需的身份信息
func (t *Tracer) credentials() (appID, appName, secret, version, ip, clientID string) {
	t.stateMu.RLock()
	appID = t.AppID
	appName = t.AppName
	secret = t.Secret
	version = t.Version
	ip = t.IP
	clientID = t.ClientID
	t.stateMu.RUnlock()

	if appName == "" {
		appName = appID
	}
	if ip == "" {
		ip = getLocalIP()
	}
	return
}

// identity 返回心跳所需的名称、版本与 IP
func (t *Tracer) identity() (name, version, ip string) {
	t.stateMu.RLock()
	defer t.stateMu.RUnlock()

	name = t.AppName
	if name == "" {
		name = t.AppID
	}
	return name, t.Version, t.IP
}

func (t *Tracer) getPeriod() int {
	t.stateMu.RLock()
	period := t.Period
	t.stateMu.RUnlock()
	if period <= 0 {
		return defaultPeriod
	}
	return period
}

func (t *Tracer) periodDuration() time.Duration {
	return time.Duration(t.getPeriod()) * time.Second
}

func (t *Tracer) getEnableMeter() bool {
	t.stateMu.RLock()
	defer t.stateMu.RUnlock()
	return t.EnableMeter
}

func (t *Tracer) getToken() string {
	t.stateMu.RLock()
	defer t.stateMu.RUnlock()
	return t.token
}

func (t *Tracer) tokenSnapshot() (string, int64) {
	t.stateMu.RLock()
	defer t.stateMu.RUnlock()
	return t.token, t.tokenExpire
}

func (t *Tracer) setToken(token string) {
	t.stateMu.Lock()
	t.token = token
	t.stateMu.Unlock()
}

func (t *Tracer) setTokenWithExpire(token string, expire int64) {
	t.stateMu.Lock()
	t.token = token
	t.tokenExpire = expire
	t.stateMu.Unlock()
}

func (t *Tracer) tokenValid() bool {
	token, expire := t.tokenSnapshot()
	return token != "" && expire > 0 && time.Now().Unix() < expire-tokenRefreshAhead
}

// ensureToken 确保存在可用令牌，缺失或即将过期时重新登录
func (t *Tracer) ensureToken() bool {
	if t.tokenValid() {
		return true
	}
	return t.login()
}

// relogin 令牌失效后重新登录一次；若其它协程已刷新令牌则直接复用
func (t *Tracer) relogin(failedToken string) bool {
	if token := t.getToken(); token != "" && token != failedToken && t.tokenValid() {
		return true
	}
	return t.login()
}

func (t *Tracer) httpClient() *http.Client {
	t.stateMu.RLock()
	client := t.client
	t.stateMu.RUnlock()
	if client == nil {
		return http.DefaultClient
	}
	return client
}

func (t *Tracer) postJSON(path string, payload interface{}) (*APIResponse, error) {
	body, err := json.Marshal(payload)
	if err != nil {
		return nil, err
	}
	return t.postWithAuth(path, "application/json", body)
}

// postWithAuth 携带令牌发送请求，服务端返回 401 时重新登录并重试一次
func (t *Tracer) postWithAuth(path, contentType string, body []byte) (*APIResponse, error) {
	if !t.ensureToken() {
		return nil, fmt.Errorf("not logged in")
	}

	failedToken := t.getToken()
	apiResp, status, err := t.post(path, contentType, body, failedToken)
	if err != nil {
		return nil, err
	}

	if status == http.StatusUnauthorized || (apiResp != nil && apiResp.Code == http.StatusUnauthorized) {
		if !t.relogin(failedToken) {
			return nil, fmt.Errorf("unauthorized")
		}

		apiResp, status, err = t.post(path, contentType, body, t.getToken())
		if err != nil {
			return nil, err
		}
		if status == http.StatusUnauthorized || (apiResp != nil && apiResp.Code == http.StatusUnauthorized) {
			return nil, fmt.Errorf("unauthorized")
		}
	}

	return apiResp, nil
}

func (t *Tracer) post(path, contentType string, body []byte, token string) (*APIResponse, int, error) {
	reqURL := t.Server + path
	if token != "" {
		reqURL = fmt.Sprintf("%s?Token=%s", reqURL, url.QueryEscape(token))
	}

	req, err := http.NewRequest(http.MethodPost, reqURL, bytes.NewReader(body))
	if err != nil {
		return nil, 0, err
	}
	req.Header.Set("Content-Type", contentType)

	resp, err := t.httpClient().Do(req)
	if err != nil {
		return nil, 0, err
	}
	defer resp.Body.Close()

	data, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, resp.StatusCode, err
	}

	apiResp := &APIResponse{}
	if len(data) > 0 {
		_ = json.Unmarshal(data, apiResp)
	}

	if resp.StatusCode == http.StatusUnauthorized {
		return apiResp, resp.StatusCode, nil
	}
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return apiResp, resp.StatusCode, fmt.Errorf("http status %d", resp.StatusCode)
	}

	return apiResp, resp.StatusCode, nil
}

func gzipData(data []byte) ([]byte, error) {
	var buf bytes.Buffer
	gz := gzip.NewWriter(&buf)
	if _, err := gz.Write(data); err != nil {
		_ = gz.Close()
		return nil, err
	}
	if err := gz.Close(); err != nil {
		return nil, err
	}
	return buf.Bytes(), nil
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
				ts = ts*31 + int64(i) // Simple mixing
			}
		}
	}
	return hex.EncodeToString(b)
}

// getLocalIP 枚举网卡获取本机 IPv4 地址，必要时回退到 UDP 探测
func getLocalIP() string {
	ifaces, err := net.Interfaces()
	if err == nil {
		for _, iface := range ifaces {
			if iface.Flags&net.FlagUp == 0 || iface.Flags&net.FlagLoopback != 0 {
				continue
			}
			addrs, err := iface.Addrs()
			if err != nil {
				continue
			}
			for _, addr := range addrs {
				var ip net.IP
				switch v := addr.(type) {
				case *net.IPNet:
					ip = v.IP
				case *net.IPAddr:
					ip = v.IP
				}
				if ip == nil || ip.IsLoopback() {
					continue
				}
				if ip4 := ip.To4(); ip4 != nil {
					return ip4.String()
				}
			}
		}
	}

	// 回退：UDP 探测外网出口地址，设置短超时避免阻塞
	conn, err := net.DialTimeout("udp", "8.8.8.8:80", 200*time.Millisecond)
	if err == nil {
		defer conn.Close()
		if addr, ok := conn.LocalAddr().(*net.UDPAddr); ok && addr.IP != nil {
			return addr.IP.String()
		}
	}

	return "127.0.0.1"
}

func containsIgnoreCase(s, substr string) bool {
	return strings.Contains(strings.ToLower(s), strings.ToLower(substr))
}
