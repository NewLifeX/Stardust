package protocol

import (
	"bytes"
	"encoding/json"
	"fmt"
	"strconv"
	"strings"
)

// APIError 表示服务端在 200 响应中返回的业务错误。
type APIError struct {
	Code int
	Msg  string
}

// Error 实现 error。
func (e *APIError) Error() string {
	if e.Msg == "" {
		return fmt.Sprintf("server returned code %d", e.Code)
	}

	return fmt.Sprintf("server returned code %d: %s", e.Code, e.Msg)
}

type wrappedResponse struct {
	Code *int            `json:"Code"`
	Data json.RawMessage `json:"Data"`
	Msg  string          `json:"Msg"`
}

// Unmarshal 解析 JSON。encoding/json 本身对字段名大小写不敏感，未知字段自动忽略。
func Unmarshal(data []byte, v any) error {
	if err := json.Unmarshal(data, v); err != nil {
		return fmt.Errorf("decode json: %w", err)
	}

	return nil
}

// ParseResponse 解析裸 JSON 或 { code, data, msg } 包装的响应。
func ParseResponse(data []byte, v any) error {
	trimmed := bytes.TrimSpace(data)
	if len(trimmed) == 0 {
		return fmt.Errorf("empty response body")
	}

	var wrapper wrappedResponse
	if err := json.Unmarshal(trimmed, &wrapper); err == nil && wrapper.Code != nil {
		if *wrapper.Code != 0 {
			return &APIError{Code: *wrapper.Code, Msg: wrapper.Msg}
		}
		if len(wrapper.Data) > 0 && string(wrapper.Data) != "null" {
			return Unmarshal(wrapper.Data, v)
		}
	}

	return Unmarshal(trimmed, v)
}

// ParseCount 解析 PostEvents 返回的裸整数或包装结构。
func ParseCount(data []byte) (int, error) {
	trimmed := bytes.TrimSpace(data)
	if len(trimmed) == 0 {
		return 0, fmt.Errorf("empty response body")
	}

	var direct int
	if err := json.Unmarshal(trimmed, &direct); err == nil {
		return direct, nil
	}

	var text string
	if err := json.Unmarshal(trimmed, &text); err == nil {
		n, err := strconv.Atoi(strings.TrimSpace(text))
		if err != nil {
			return 0, fmt.Errorf("decode event count %q: %w", text, err)
		}
		return n, nil
	}

	var wrapper struct {
		Code *int            `json:"Code"`
		Data json.RawMessage `json:"Data"`
		Msg  string          `json:"Msg"`
	}
	if err := json.Unmarshal(trimmed, &wrapper); err != nil {
		return 0, fmt.Errorf("decode event count: %w", err)
	}
	if wrapper.Code != nil && *wrapper.Code != 0 {
		return 0, &APIError{Code: *wrapper.Code, Msg: wrapper.Msg}
	}
	if len(wrapper.Data) == 0 || string(wrapper.Data) == "null" {
		return 0, nil
	}

	if err := json.Unmarshal(wrapper.Data, &direct); err == nil {
		return direct, nil
	}
	if err := json.Unmarshal(wrapper.Data, &text); err == nil {
		n, err := strconv.Atoi(strings.TrimSpace(text))
		if err != nil {
			return 0, fmt.Errorf("decode event count %q: %w", text, err)
		}
		return n, nil
	}

	return 0, fmt.Errorf("unsupported event count payload: %s", truncateForError(trimmed))
}

func truncateForError(data []byte) string {
	const max = 200
	if len(data) <= max {
		return string(data)
	}

	return string(data[:max]) + "..."
}
