# 星尘监控 Go SDK

适用于 Go 1.18+， 提供星尘 APM 监控和配置中心的接入能力。

## 安装

无第三方依赖，仅使用 Go 标准库。

## 快速开始

```go
package main

import (
    "fmt"
    "stardust"
)

func main() {
    tracer := stardust.NewTracer("http://star.example.com:6600", "MyGoApp", "MySecret")
    tracer.Start()
    defer tracer.Stop()

    // 手动埋点
    span := tracer.NewSpan("业务操作", "")
    span.Tag = "参数信息"
    doSomething()
    span.Finish()

    // 或者使用 defer
    span2 := tracer.NewSpan("数据库查询", "")
    defer span2.Finish()
    queryDB()
}
```

## 完整代码

```go
package stardust

import (
	"bytes"
	"compress/gzip"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"os"
	"sync"
	"time"

	"crypto/rand"
	"encoding/hex"
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

	body, _ := json.Marshal(payload)
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
		gz.Write(body)
		gz.Close()

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

	respBody, _ := io.ReadAll(resp.Body)
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
	rand.Read(b)
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
```

## Gin 框架集成

```go
package main

import (
	"stardust"
	"github.com/gin-gonic/gin"
)

var tracer = stardust.NewTracer("http://star.example.com:6600", "MyGinApp", "secret")

func StardustMiddleware() gin.HandlerFunc {
	return func(c *gin.Context) {
		name := c.Request.Method + " " + c.Request.URL.Path
		span := tracer.NewSpan(name, "")
		span.Tag = c.Request.Method + " " + c.Request.RequestURI

		c.Next()

		if c.Writer.Status() >= 400 {
			span.Error = fmt.Sprintf("HTTP %d", c.Writer.Status())
		}
		if len(c.Errors) > 0 {
			span.Error = c.Errors.String()
		}
		span.Finish()
	}
}

func main() {
	tracer.Start()
	defer tracer.Stop()

	r := gin.Default()
	r.Use(StardustMiddleware())
	r.Run(":8080")
}
```

## net/http 标准库集成

```go
package main

import (
	"net/http"
	"stardust"
)

var tracer = stardust.NewTracer("http://star.example.com:6600", "MyApp", "secret")

func StardustHandler(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		span := tracer.NewSpan(r.Method+" "+r.URL.Path, "")
		span.Tag = r.Method + " " + r.RequestURI
		defer span.Finish()

		next.ServeHTTP(w, r)
	})
}

func main() {
	tracer.Start()
	defer tracer.Stop()

	mux := http.NewServeMux()
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		w.Write([]byte("Hello"))
	})

	http.ListenAndServe(":8080", StardustHandler(mux))
}
```

---

# 配置中心

## 配置中心快速开始

```go
package main

import (
	"fmt"
	"stardust"
)

func main() {
	// 创建配置客户端
	config := stardust.NewConfigClient("http://star.example.com:6600", "MyGoApp", "MySecret")
	config.Start()
	defer config.Stop()

	// 获取配置
	value := config.Get("database.host")
	fmt.Println("Database Host:", value)

	// 监听配置变更
	config.OnChange(func(configs map[string]string) {
		fmt.Println("配置已更新:", configs)
	})

	// 等待程序运行
	select {}
}
```

## 配置中心完整代码

```go
package stardust

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"sync"
	"time"
)

// ConfigInfo 配置信息
type ConfigInfo struct {
	Version     int               `json:"Version"`
	Scope       string            `json:"Scope"`
	SourceIP    string            `json:"SourceIP"`
	NextVersion int               `json:"NextVersion"`
	NextPublish string            `json:"NextPublish"`
	UpdateTime  int64             `json:"UpdateTime"`
	Configs     map[string]string `json:"Configs"`
}

// ConfigClient 配置中心客户端
type ConfigClient struct {
	Server   string
	AppID    string
	Secret   string
	ClientID string

	token       string
	tokenExpire int64
	version     int
	configs     map[string]string
	mu          sync.RWMutex
	running     bool
	stopCh      chan struct{}
	client      *http.Client
	onChange    func(map[string]string)
}

// NewConfigClient 创建配置客户端
func NewConfigClient(server, appID, secret string) *ConfigClient {
	return &ConfigClient{
		Server:   server,
		AppID:    appID,
		Secret:   secret,
		ClientID: fmt.Sprintf("%s@%d", getLocalIP(), os.Getpid()),
		configs:  make(map[string]string),
		stopCh:   make(chan struct{}),
		client:   &http.Client{Timeout: 10 * time.Second},
	}
}

// Start 启动配置客户端
func (c *ConfigClient) Start() {
	c.login()
	c.running = true
	go c.pollLoop()
}

// Stop 停止配置客户端
func (c *ConfigClient) Stop() {
	c.running = false
	close(c.stopCh)
}

// Get 获取配置项
func (c *ConfigClient) Get(key string) string {
	c.mu.RLock()
	defer c.mu.RUnlock()
	return c.configs[key]
}

// GetAll 获取所有配置
func (c *ConfigClient) GetAll() map[string]string {
	c.mu.RLock()
	defer c.mu.RUnlock()
	result := make(map[string]string, len(c.configs))
	for k, v := range c.configs {
		result[k] = v
	}
	return result
}

// OnChange 注册配置变更回调
func (c *ConfigClient) OnChange(callback func(map[string]string)) {
	c.onChange = callback
}

func (c *ConfigClient) login() {
	payload := map[string]interface{}{
		"AppId":    c.AppID,
		"Secret":   c.Secret,
		"ClientId": c.ClientID,
		"AppName":  c.AppID,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return
	}

	resp, err := c.client.Post(c.Server+"/App/Login", "application/json", bytes.NewReader(body))
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Config login failed: %v\n", err)
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

	c.token = loginResp.Token
	if loginResp.Expire > 0 {
		c.tokenExpire = time.Now().Unix() + int64(loginResp.Expire)
	}
}

func (c *ConfigClient) getAllConfig() {
	payload := map[string]interface{}{
		"AppId":    c.AppID,
		"Secret":   c.Secret,
		"ClientId": c.ClientID,
		"Version":  c.version,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return
	}

	reqURL := fmt.Sprintf("%s/Config/GetAll?Token=%s", c.Server, url.QueryEscape(c.token))
	resp, err := c.client.Post(reqURL, "application/json", bytes.NewReader(body))
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Config GetAll failed: %v\n", err)
		return
	}
	defer resp.Body.Close()

	respBody, _ := io.ReadAll(resp.Body)
	var apiResp APIResponse
	if err := json.Unmarshal(respBody, &apiResp); err != nil || apiResp.Code != 0 {
		return
	}

	var configInfo ConfigInfo
	if err := json.Unmarshal(apiResp.Data, &configInfo); err != nil {
		return
	}

	// 版本没变化，不更新
	if c.version > 0 && configInfo.Version == c.version && configInfo.Configs == nil {
		return
	}

	// 更新配置
	if configInfo.Configs != nil {
		c.mu.Lock()
		c.configs = configInfo.Configs
		c.version = configInfo.Version
		c.mu.Unlock()

		// 触发回调
		if c.onChange != nil {
			go c.onChange(configInfo.Configs)
		}
	}
}

func (c *ConfigClient) pollLoop() {
	// 首次立即获取配置
	c.getAllConfig()

	ticker := time.NewTicker(30 * time.Second)
	defer ticker.Stop()
	for {
		select {
		case <-ticker.C:
			c.getAllConfig()
		case <-c.stopCh:
			return
		}
	}
}
```

## 配置中心与APM监控集成示例

```go
package main

import (
	"fmt"
	"stardust"
	"time"
)

func main() {
	// 启动APM监控
	tracer := stardust.NewTracer("http://star.example.com:6600", "MyGoApp", "MySecret")
	tracer.Start()
	defer tracer.Stop()

	// 启动配置中心
	config := stardust.NewConfigClient("http://star.example.com:6600", "MyGoApp", "MySecret")
	config.Start()
	defer config.Stop()

	// 监听配置变更
	config.OnChange(func(configs map[string]string) {
		span := tracer.NewSpan("配置更新", "")
		span.Tag = fmt.Sprintf("配置项数量: %d", len(configs))
		span.Finish()
		
		fmt.Println("配置已更新:")
		for k, v := range configs {
			fmt.Printf("  %s = %s\n", k, v)
		}
	})

	// 业务逻辑
	for {
		span := tracer.NewSpan("业务处理", "")
		
		// 读取配置
		dbHost := config.Get("database.host")
		dbPort := config.Get("database.port")
		
		span.Tag = fmt.Sprintf("DB: %s:%s", dbHost, dbPort)
		
		// 模拟业务处理
		time.Sleep(5 * time.Second)
		
		span.Finish()
	}
}
```

## 在Gin框架中使用配置中心

```go
package main

import (
	"fmt"
	"github.com/gin-gonic/gin"
	"stardust"
)

var (
	tracer = stardust.NewTracer("http://star.example.com:6600", "MyGinApp", "secret")
	config = stardust.NewConfigClient("http://star.example.com:6600", "MyGinApp", "secret")
)

func main() {
	tracer.Start()
	defer tracer.Stop()
	
	config.Start()
	defer config.Stop()

	// 监听配置变更
	config.OnChange(func(configs map[string]string) {
		fmt.Println("配置已更新，请重新加载相关模块")
	})

	r := gin.Default()
	
	// APM中间件
	r.Use(StardustMiddleware())

	r.GET("/config/:key", func(c *gin.Context) {
		key := c.Param("key")
		value := config.Get(key)
		c.JSON(200, gin.H{
			"key":   key,
			"value": value,
		})
	})

	r.GET("/configs", func(c *gin.Context) {
		c.JSON(200, config.GetAll())
	})

	r.Run(":8080")
}

func StardustMiddleware() gin.HandlerFunc {
	return func(c *gin.Context) {
		name := c.Request.Method + " " + c.Request.URL.Path
		span := tracer.NewSpan(name, "")
		span.Tag = c.Request.Method + " " + c.Request.RequestURI

		c.Next()

		if c.Writer.Status() >= 400 {
			span.Error = fmt.Sprintf("HTTP %d", c.Writer.Status())
		}
		if len(c.Errors) > 0 {
			span.Error = c.Errors.String()
		}
		span.Finish()
	}
}
```
