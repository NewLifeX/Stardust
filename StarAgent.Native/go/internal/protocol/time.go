package protocol

import (
	"encoding/json"
	"fmt"
	"strings"
	"time"
)

// TimeLayout 是协议使用的 UTC 时间格式，对应 .NET 的 yyyy-MM-dd HH:mm:ss。
const TimeLayout = "2006-01-02 15:04:05"

// ZeroTimeText 是未设置时间时的协议文本。
const ZeroTimeText = "0001-01-01 00:00:00"

var parseLayouts = []string{
	TimeLayout,
	"2006-01-02 15:04:05.999999999",
	"2006-01-02T15:04:05Z07:00",
	"2006-01-02T15:04:05",
	"2006-01-02T15:04:05.999999999",
	time.RFC3339Nano,
}

// Time 包装 time.Time，按协议要求使用 UTC 的 yyyy-MM-dd HH:mm:ss 文本。
type Time struct {
	time.Time
}

// NewTime 创建协议时间。
func NewTime(t time.Time) Time {
	if t.IsZero() {
		return Time{}
	}

	return Time{Time: t.UTC()}
}

// TimeFromUnixMilli 从 Unix 毫秒创建协议时间。
func TimeFromUnixMilli(ms int64) Time {
	if ms == 0 {
		return Time{}
	}

	return NewTime(time.UnixMilli(ms))
}

// UnixMilli 返回 Unix 毫秒，零值返回 0。
func (t Time) UnixMilli() int64 {
	if t.IsZero() {
		return 0
	}

	return t.Time.UnixMilli()
}

// IsSet 判断时间是否已设置。协议用 0001-01-01 表示未设置。
func (t Time) IsSet() bool {
	return !t.IsZero() && t.Year() > 2000
}

// String 返回协议时间文本。
func (t Time) String() string {
	if t.IsZero() {
		return ZeroTimeText
	}

	return t.UTC().Format(TimeLayout)
}

// MarshalJSON 序列化为 yyyy-MM-dd HH:mm:ss。
func (t Time) MarshalJSON() ([]byte, error) {
	return json.Marshal(t.String())
}

// UnmarshalJSON 解析协议时间，兼容 null、空字符串与 RFC3339。
func (t *Time) UnmarshalJSON(data []byte) error {
	if len(data) == 0 || string(data) == "null" {
		t.Time = time.Time{}
		return nil
	}

	var text string
	if err := json.Unmarshal(data, &text); err != nil {
		return fmt.Errorf("parse protocol time: %w", err)
	}

	parsed, err := ParseTime(text)
	if err != nil {
		return err
	}

	t.Time = parsed
	return nil
}

// ParseTime 解析协议时间文本。
func ParseTime(text string) (time.Time, error) {
	text = strings.TrimSpace(text)
	if text == "" || text == ZeroTimeText || strings.HasPrefix(text, "0001-01-01T00:00:00") {
		return time.Time{}, nil
	}

	for _, layout := range parseLayouts {
		if t, err := time.Parse(layout, text); err == nil {
			return t.UTC(), nil
		}
	}

	return time.Time{}, fmt.Errorf("unsupported time format: %q", text)
}
