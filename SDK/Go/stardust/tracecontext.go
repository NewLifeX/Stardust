package stardust

import (
	"encoding/hex"
	"strings"
)

const (
	// TraceparentHeader W3C Trace Context 标准头名称
	TraceparentHeader = "traceparent"

	traceparentVersion = "00"
	traceparentFlags   = "01"
	// maxTraceIDLength trace-id 最大长度，防止异常头部无限膨胀
	maxTraceIDLength = 32
)

// TraceContext W3C traceparent 链路上下文
type TraceContext struct {
	// TraceID 链路标识。SDK 生成 32 位小写十六进制；解析时兼容 .NET 历史实现的短标识
	TraceID string
	// SpanID 上游片段标识，16 位小写十六进制
	SpanID string
	// Sampled 上游是否采样
	Sampled bool
}

// ParseTraceparent 解析 W3C traceparent 头部。
// 格式为 version-traceid-spanid-flags，非法或缺失时返回 false。
// trace-id 允许任意长度的非空十六进制串，以兼容 .NET 历史实现；span-id 必须为 16 位十六进制。
//
// 示例：
//
//	tc, ok := stardust.ParseTraceparent("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")
//	if ok {
//	    fmt.Println(tc.TraceID, tc.SpanID, tc.Sampled)
//	}
func ParseTraceparent(value string) (*TraceContext, bool) {
	value = strings.TrimSpace(value)
	if value == "" {
		return nil, false
	}

	parts := strings.Split(value, "-")
	if len(parts) < 4 {
		return nil, false
	}

	version := strings.ToLower(parts[0])
	if len(version) != 2 || !isHexString(version) || version == "ff" {
		return nil, false
	}
	// 00 版本不允许附加字段，更高版本允许扩展
	if version == traceparentVersion && len(parts) != 4 {
		return nil, false
	}

	traceID := strings.ToLower(parts[1])
	if traceID == "" || len(traceID) > maxTraceIDLength || !isHexString(traceID) || isZeroHex(traceID) {
		return nil, false
	}

	spanID := strings.ToLower(parts[2])
	if len(spanID) != 16 || !isHexString(spanID) || isZeroHex(spanID) {
		return nil, false
	}

	flags := strings.ToLower(parts[3])
	if len(flags) != 2 || !isHexString(flags) {
		return nil, false
	}
	flagValue, err := hex.DecodeString(flags)
	if err != nil || len(flagValue) != 1 {
		return nil, false
	}

	return &TraceContext{
		TraceID: traceID,
		SpanID:  spanID,
		Sampled: flagValue[0]&0x01 != 0,
	}, true
}

// FormatTraceparent 按 W3C 格式生成 traceparent 头部，参数非法时返回空串。
// SDK 不做头部采样，出站时通常传入 Sampled=true。
func FormatTraceparent(tc *TraceContext) string {
	if tc == nil {
		return ""
	}

	traceID := strings.ToLower(strings.TrimSpace(tc.TraceID))
	spanID := strings.ToLower(strings.TrimSpace(tc.SpanID))
	if traceID == "" || spanID == "" || !isHexString(traceID) || !isHexString(spanID) {
		return ""
	}
	if len(traceID) > maxTraceIDLength || len(spanID) != 16 {
		return ""
	}

	flags := "00"
	if tc.Sampled {
		flags = traceparentFlags
	}
	return traceparentVersion + "-" + traceID + "-" + spanID + "-" + flags
}

// NewTraceID 生成 32 位小写十六进制链路标识
func NewTraceID() string {
	for {
		id := randomHex(16)
		if !isZeroHex(id) {
			return id
		}
	}
}

// NewSpanID 生成 16 位小写十六进制片段标识
func NewSpanID() string {
	for {
		id := randomHex(8)
		if !isZeroHex(id) {
			return id
		}
	}
}

func isHexString(value string) bool {
	if value == "" {
		return false
	}
	for i := 0; i < len(value); i++ {
		c := value[i]
		if (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') {
			continue
		}
		return false
	}
	return true
}

func isZeroHex(value string) bool {
	if value == "" {
		return true
	}
	for i := 0; i < len(value); i++ {
		if value[i] != '0' {
			return false
		}
	}
	return true
}
