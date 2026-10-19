package stardust

import (
	"os"
	"testing"
	"time"
)

func TestNewTracer(t *testing.T) {
	tracer := NewTracer("http://localhost:6600", "TestApp", "TestSecret")
	if tracer == nil {
		t.Fatal("NewTracer returned nil")
	}
	if tracer.AppID != "TestApp" {
		t.Errorf("Expected AppID=TestApp, got %s", tracer.AppID)
	}
	if tracer.AppName != "TestApp" {
		t.Errorf("Expected AppName=TestApp, got %s", tracer.AppName)
	}
	if tracer.Secret != "TestSecret" {
		t.Errorf("Expected Secret=TestSecret, got %s", tracer.Secret)
	}
	if tracer.Period != 60 {
		t.Errorf("Expected Period=60, got %d", tracer.Period)
	}
	if tracer.MaxSamples != 1 {
		t.Errorf("Expected MaxSamples=1, got %d", tracer.MaxSamples)
	}
	if tracer.MaxErrors != 10 {
		t.Errorf("Expected MaxErrors=10, got %d", tracer.MaxErrors)
	}
	if tracer.Timeout != 5000 {
		t.Errorf("Expected Timeout=5000, got %d", tracer.Timeout)
	}
	if tracer.MaxTagLen != 1024 {
		t.Errorf("Expected MaxTagLen=1024, got %d", tracer.MaxTagLen)
	}
	if !tracer.EnableMeter {
		t.Error("Expected EnableMeter=true by default")
	}
	if tracer.Version == "" {
		t.Error("Expected non-empty default Version")
	}
	if tracer.IP == "" {
		t.Error("Expected non-empty IP")
	}
	if tracer.ClientID == "" {
		t.Error("Expected non-empty ClientID")
	}
	if tracer.startTime.IsZero() {
		t.Error("Expected startTime to be initialized")
	}
}

func TestTracerSetters(t *testing.T) {
	tracer := NewTracer("http://localhost:6600", "TestApp", "TestSecret").
		SetVersion("2.0.1").
		SetAppName("MyGoApp").
		SetIP("10.1.2.3").
		SetEnableMeter(false).
		SetPeriod(15)

	if tracer.Version != "2.0.1" {
		t.Errorf("Expected Version=2.0.1, got %s", tracer.Version)
	}
	if tracer.AppName != "MyGoApp" {
		t.Errorf("Expected AppName=MyGoApp, got %s", tracer.AppName)
	}
	if tracer.IP != "10.1.2.3" {
		t.Errorf("Expected IP=10.1.2.3, got %s", tracer.IP)
	}
	if tracer.EnableMeter {
		t.Error("Expected EnableMeter=false")
	}
	if tracer.Period != 15 {
		t.Errorf("Expected Period=15, got %d", tracer.Period)
	}
}

func TestAppInfo(t *testing.T) {
	tracer := NewTracer("http://localhost:6600", "TestApp", "TestSecret").
		SetVersion("2.0.1").
		SetAppName("MyGoApp").
		SetIP("10.1.2.3")
	t.Cleanup(tracer.Stop)

	info := tracer.appInfo()

	if info.Id != os.Getpid() {
		t.Errorf("Expected Id=%d, got %d", os.Getpid(), info.Id)
	}
	if info.Name != "MyGoApp" {
		t.Errorf("Expected Name=MyGoApp, got %s", info.Name)
	}
	if info.Version != "2.0.1" {
		t.Errorf("Expected Version=2.0.1, got %s", info.Version)
	}
	if info.IP != "10.1.2.3" {
		t.Errorf("Expected IP=10.1.2.3, got %s", info.IP)
	}
	if info.CommandLine == "" {
		t.Error("Expected non-empty CommandLine")
	}
	if info.StartTime == "" {
		t.Error("Expected non-empty StartTime")
	} else if _, err := time.Parse(time.RFC3339, info.StartTime); err != nil {
		t.Errorf("StartTime should be RFC3339: %v", err)
	}
	if info.WorkingSet <= 0 {
		t.Errorf("Expected WorkingSet > 0, got %d", info.WorkingSet)
	}
	if info.HeapSize < 0 {
		t.Errorf("Expected HeapSize >= 0, got %d", info.HeapSize)
	}
	if info.Threads < 1 {
		t.Errorf("Expected Threads >= 1, got %d", info.Threads)
	}
	if info.Time <= 0 {
		t.Errorf("Expected Time > 0, got %d", info.Time)
	}
	if info.GCCount < 0 {
		t.Errorf("Expected GCCount >= 0, got %d", info.GCCount)
	}

	second := tracer.appInfo()
	if second.GCCount < 0 {
		t.Errorf("Expected second GCCount >= 0, got %d", second.GCCount)
	}
}

func TestSpan(t *testing.T) {
	tracer := NewTracer("http://localhost:6600", "TestApp", "TestSecret")

	span := tracer.NewSpan("TestOperation", "")
	if span == nil {
		t.Fatal("NewSpan returned nil")
	}
	if span.name != "TestOperation" {
		t.Errorf("Expected name=TestOperation, got %s", span.name)
	}
	if span.ID == "" {
		t.Error("Span ID should not be empty")
	}
	if span.TraceID == "" {
		t.Error("Span TraceID should not be empty")
	}

	span.Tag = "test tag"
	span.Finish()

	if span.EndTime == 0 {
		t.Error("Span EndTime should not be zero after Finish()")
	}
	if span.EndTime < span.StartTime {
		t.Error("Span EndTime should be greater than StartTime")
	}
}

func TestSpanBuilder(t *testing.T) {
	builder := newSpanBuilder("TestOp", 10, 5)
	if builder == nil {
		t.Fatal("newSpanBuilder returned nil")
	}
	if builder.Name != "TestOp" {
		t.Errorf("Expected Name=TestOp, got %s", builder.Name)
	}

	// 添加正常 span
	span1 := &Span{
		StartTime: time.Now().UnixMilli(),
		EndTime:   time.Now().UnixMilli() + 100,
		Error:     "",
	}
	builder.addSpan(span1)

	if builder.Total != 1 {
		t.Errorf("Expected Total=1, got %d", builder.Total)
	}
	if builder.Errors != 0 {
		t.Errorf("Expected Errors=0, got %d", builder.Errors)
	}

	// 添加错误 span
	span2 := &Span{
		StartTime: time.Now().UnixMilli(),
		EndTime:   time.Now().UnixMilli() + 200,
		Error:     "test error",
	}
	builder.addSpan(span2)

	if builder.Total != 2 {
		t.Errorf("Expected Total=2, got %d", builder.Total)
	}
	if builder.Errors != 1 {
		t.Errorf("Expected Errors=1, got %d", builder.Errors)
	}
	if len(builder.ErrorSamples) != 1 {
		t.Errorf("Expected 1 error sample, got %d", len(builder.ErrorSamples))
	}
}

func TestNewConfigClient(t *testing.T) {
	config := NewConfigClient("http://localhost:6600", "TestApp", "TestSecret")
	if config == nil {
		t.Fatal("NewConfigClient returned nil")
	}
	if config.AppID != "TestApp" {
		t.Errorf("Expected AppID=TestApp, got %s", config.AppID)
	}
	if config.Secret != "TestSecret" {
		t.Errorf("Expected Secret=TestSecret, got %s", config.Secret)
	}
}

func TestConfigClientGetSet(t *testing.T) {
	config := NewConfigClient("http://localhost:6600", "TestApp", "TestSecret")

	// 手动设置配置（模拟从服务器获取）
	config.mu.Lock()
	config.configs["test.key"] = "test.value"
	config.configs["database.host"] = "localhost"
	config.mu.Unlock()

	// 测试 Get
	value := config.Get("test.key")
	if value != "test.value" {
		t.Errorf("Expected test.value, got %s", value)
	}

	// 测试不存在的 key
	empty := config.Get("nonexistent")
	if empty != "" {
		t.Errorf("Expected empty string, got %s", empty)
	}

	// 测试 GetAll
	all := config.GetAll()
	if len(all) != 2 {
		t.Errorf("Expected 2 configs, got %d", len(all))
	}
	if all["database.host"] != "localhost" {
		t.Errorf("Expected localhost, got %s", all["database.host"])
	}
}

func TestRandomHex(t *testing.T) {
	hex1 := randomHex(8)
	hex2 := randomHex(8)

	if len(hex1) != 16 { // 8 bytes = 16 hex chars
		t.Errorf("Expected length 16, got %d", len(hex1))
	}
	if hex1 == hex2 {
		t.Error("randomHex should generate different values")
	}
}

func TestGetLocalIP(t *testing.T) {
	ip := getLocalIP()
	if ip == "" {
		t.Error("getLocalIP should not return empty string")
	}
	// 应该返回 IP 地址格式，至少不是空
	if len(ip) < 7 { // 最短的 IP: 0.0.0.0
		t.Errorf("IP address too short: %s", ip)
	}
}

func TestContainsIgnoreCase(t *testing.T) {
	tests := []struct {
		s      string
		substr string
		want   bool
	}{
		{"HelloWorld", "world", true},
		{"HelloWorld", "WORLD", true},
		{"HelloWorld", "hello", true},
		{"HelloWorld", "xyz", false},
		{"", "test", false},
		{"test", "", true},
	}

	for _, tt := range tests {
		got := containsIgnoreCase(tt.s, tt.substr)
		if got != tt.want {
			t.Errorf("containsIgnoreCase(%q, %q) = %v, want %v",
				tt.s, tt.substr, got, tt.want)
		}
	}
}
