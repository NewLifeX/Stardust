package stardust

import (
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
	if tracer.Secret != "TestSecret" {
		t.Errorf("Expected Secret=TestSecret, got %s", tracer.Secret)
	}
	if tracer.Period != 60 {
		t.Errorf("Expected Period=60, got %d", tracer.Period)
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
