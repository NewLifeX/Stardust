package client

import (
	"bytes"
	"context"
	"crypto/tls"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

const (
	requestTimeout = 15 * time.Second
	maxResponse    = 1 << 20
)

// HTTPError 表示服务端返回非 2xx 状态。
type HTTPError struct {
	StatusCode int
	Body       string
}

// Error 实现 error。
func (e *HTTPError) Error() string {
	return fmt.Sprintf("http %d: %s", e.StatusCode, e.Body)
}

// Client 是 StarServer Node 接口客户端。
type Client struct {
	mu             sync.RWMutex
	baseURL        string
	token          string
	tokenExpire    time.Time
	http           *http.Client
	logger         *slog.Logger
	userAgent      string
	onUnauthorized func(ctx context.Context) error
}

// New 创建客户端。
func New(server string, tlsVerify bool, logger *slog.Logger) (*Client, error) {
	if logger == nil {
		logger = slog.Default()
	}

	dialer := &net.Dialer{Timeout: 5 * time.Second, KeepAlive: 30 * time.Second}
	transport := &http.Transport{
		Proxy:                 http.ProxyFromEnvironment,
		DialContext:           dialer.DialContext,
		ForceAttemptHTTP2:     true,
		TLSHandshakeTimeout:   5 * time.Second,
		ResponseHeaderTimeout: requestTimeout,
	}
	if !tlsVerify {
		//nolint:gosec // TlsVerify=false 仅用于测试环境，协议文档已明确要求生产校验证书。
		transport.TLSClientConfig = &tls.Config{InsecureSkipVerify: true}
	}

	client := &Client{
		http:      &http.Client{Transport: transport, Timeout: requestTimeout},
		logger:    logger,
		userAgent: "StarAgent-Go",
	}
	if err := client.SetBaseURL(server); err != nil {
		return nil, err
	}

	return client, nil
}

// SetBaseURL 切换服务端地址。
func (c *Client) SetBaseURL(server string) error {
	server = strings.TrimRight(strings.TrimSpace(server), "/")
	u, err := url.Parse(server)
	if err != nil || u.Scheme == "" || u.Host == "" {
		return fmt.Errorf("invalid server url %q", server)
	}
	if u.Scheme != "http" && u.Scheme != "https" {
		return fmt.Errorf("unsupported server scheme %q", u.Scheme)
	}

	c.mu.Lock()
	c.baseURL = server
	c.mu.Unlock()

	return nil
}

// BaseURL 返回当前服务端地址。
func (c *Client) BaseURL() string {
	c.mu.RLock()
	defer c.mu.RUnlock()

	return c.baseURL
}

// SetToken 设置登录令牌。
func (c *Client) SetToken(token string, expireSeconds int) {
	c.mu.Lock()
	defer c.mu.Unlock()

	c.token = token
	if token == "" || expireSeconds <= 0 {
		c.tokenExpire = time.Time{}
		return
	}
	c.tokenExpire = time.Now().Add(time.Duration(expireSeconds) * time.Second)
}

// Token 返回当前令牌。
func (c *Client) Token() string {
	c.mu.RLock()
	defer c.mu.RUnlock()

	return c.token
}

// TokenExpired 判断令牌是否已过期（预留 1 分钟余量）。
func (c *Client) TokenExpired() bool {
	c.mu.RLock()
	defer c.mu.RUnlock()

	return c.token == "" || (!c.tokenExpire.IsZero() && time.Now().After(c.tokenExpire.Add(-time.Minute)))
}

// OnUnauthorized 注册 401 后的重新登录回调。
func (c *Client) OnUnauthorized(fn func(ctx context.Context) error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	c.onUnauthorized = fn
}

// SetUserAgent 设置 User-Agent。
func (c *Client) SetUserAgent(value string) {
	c.mu.Lock()
	defer c.mu.Unlock()

	c.userAgent = value
}

// Login 调用 POST /node/login 并保存令牌。
func (c *Client) Login(ctx context.Context, request protocol.LoginInfo) (*protocol.LoginResponse, error) {
	var response protocol.LoginResponse
	if err := c.do(ctx, http.MethodPost, "/node/login", request, &response, false); err != nil {
		return nil, fmt.Errorf("login: %w", err)
	}
	if response.Token != "" {
		c.SetToken(response.Token, response.Expire)
	}

	return &response, nil
}

// Logout 调用 POST /node/logout。
func (c *Client) Logout(ctx context.Context, reason string) (*protocol.LoginResponse, error) {
	path := "/node/logout"
	if reason != "" {
		path += "?reason=" + url.QueryEscape(reason)
	}

	var response protocol.LoginResponse
	if err := c.do(ctx, http.MethodPost, path, nil, &response, true); err != nil {
		return nil, fmt.Errorf("logout: %w", err)
	}

	return &response, nil
}

// Ping 调用 POST /node/ping。
func (c *Client) Ping(ctx context.Context, request protocol.PingInfo) (*protocol.PingResponse, error) {
	var response protocol.PingResponse
	if err := c.do(ctx, http.MethodPost, "/node/ping", request, &response, true); err != nil {
		return nil, fmt.Errorf("ping: %w", err)
	}
	if response.Token != "" {
		c.SetToken(response.Token, 0)
		c.logger.Debug("received renewed token", "period", response.Period)
	}

	return &response, nil
}

// PostEvents 调用 POST /node/postevents，返回服务端写入条数。
func (c *Client) PostEvents(ctx context.Context, events []protocol.EventModel) (int, error) {
	data, err := c.send(ctx, http.MethodPost, "/node/postevents", events, true)
	if err != nil {
		return 0, fmt.Errorf("post events: %w", err)
	}

	count, err := protocol.ParseCount(data)
	if err != nil {
		return 0, fmt.Errorf("post events: %w", err)
	}

	return count, nil
}

// CommandReply 调用 POST /node/commandreply。
func (c *Client) CommandReply(ctx context.Context, reply protocol.CommandReplyModel) error {
	if err := c.do(ctx, http.MethodPost, "/node/commandreply", reply, nil, true); err != nil {
		return fmt.Errorf("command reply %d: %w", reply.Id, err)
	}

	return nil
}

// Upgrade 调用 POST /node/upgrade?channel=...。没有可用升级时返回 nil。
func (c *Client) Upgrade(ctx context.Context, channel string) (*protocol.UpgradeInfo, error) {
	if channel == "" {
		channel = "Release"
	}
	data, err := c.send(ctx, http.MethodPost, "/node/upgrade?channel="+url.QueryEscape(channel), nil, true)
	if err != nil {
		return nil, fmt.Errorf("upgrade: %w", err)
	}
	if len(bytes.TrimSpace(data)) == 0 || string(bytes.TrimSpace(data)) == "null" {
		return nil, nil
	}

	var info protocol.UpgradeInfo
	if err := protocol.ParseResponse(data, &info); err != nil {
		return nil, fmt.Errorf("upgrade: %w", err)
	}
	if strings.TrimSpace(info.Version) == "" {
		return nil, nil
	}

	return &info, nil
}

func (c *Client) do(ctx context.Context, method, path string, body any, out any, auth bool) error {
	data, err := c.send(ctx, method, path, body, auth)
	if err != nil {
		return err
	}
	if out == nil {
		return nil
	}
	if len(bytes.TrimSpace(data)) == 0 {
		return errors.New("empty response body")
	}

	return protocol.ParseResponse(data, out)
}

func (c *Client) send(ctx context.Context, method, path string, body any, auth bool) ([]byte, error) {
	data, status, err := c.sendOnce(ctx, method, path, body, auth)
	if err != nil {
		return nil, err
	}

	if status == http.StatusUnauthorized && auth {
		c.logger.Warn("server returned 401, re-login and retry once", "path", path)
		c.SetToken("", 0)

		c.mu.RLock()
		relogin := c.onUnauthorized
		c.mu.RUnlock()
		if relogin == nil {
			return nil, &HTTPError{StatusCode: status, Body: truncateBody(data)}
		}
		if err := relogin(ctx); err != nil {
			return nil, fmt.Errorf("re-login after 401: %w", err)
		}

		data, status, err = c.sendOnce(ctx, method, path, body, auth)
		if err != nil {
			return nil, err
		}
	}

	if status < 200 || status >= 300 {
		return nil, &HTTPError{StatusCode: status, Body: truncateBody(data)}
	}

	return data, nil
}

func (c *Client) sendOnce(ctx context.Context, method, path string, body any, auth bool) ([]byte, int, error) {
	var reader io.Reader
	if body != nil {
		data, err := json.Marshal(body)
		if err != nil {
			return nil, 0, fmt.Errorf("encode request: %w", err)
		}
		reader = bytes.NewReader(data)
	}

	request, err := http.NewRequestWithContext(ctx, method, c.BaseURL()+path, reader)
	if err != nil {
		return nil, 0, fmt.Errorf("create request: %w", err)
	}
	if body != nil {
		request.Header.Set("Content-Type", "application/json")
	}
	request.Header.Set("Accept", "application/json")

	c.mu.RLock()
	token := c.token
	userAgent := c.userAgent
	c.mu.RUnlock()
	if auth && token != "" {
		request.Header.Set("Authorization", "Bearer "+token)
	}
	if userAgent != "" {
		request.Header.Set("User-Agent", userAgent)
	}

	response, err := c.http.Do(request)
	if err != nil {
		return nil, 0, fmt.Errorf("%s %s: %w", method, path, err)
	}
	defer response.Body.Close()

	data, err := io.ReadAll(io.LimitReader(response.Body, maxResponse+1))
	if err != nil {
		return nil, 0, fmt.Errorf("read response: %w", err)
	}
	if len(data) > maxResponse {
		return nil, 0, fmt.Errorf("response body exceeds %d bytes", maxResponse)
	}

	c.logger.Debug("http request", "method", method, "path", path, "status", response.StatusCode, "bytes", len(data))

	return data, response.StatusCode, nil
}

func truncateBody(data []byte) string {
	const max = 512
	text := strings.TrimSpace(string(data))
	if len(text) <= max {
		return text
	}

	return text[:max] + "..."
}

// RequestDelay 是辅助格式化延迟毫秒的函数。
func RequestDelay(start time.Time) int64 {
	return time.Since(start).Milliseconds()
}

// StatusCode 从错误中提取 HTTP 状态码。
func StatusCode(err error) (int, bool) {
	var httpErr *HTTPError
	if errors.As(err, &httpErr) {
		return httpErr.StatusCode, true
	}

	return 0, false
}

// BuildQuery 构造查询字符串。
func BuildQuery(values map[string]string) string {
	query := url.Values{}
	for key, value := range values {
		query.Set(key, value)
	}

	return query.Encode()
}

// ParseRetryAfter 解析 Retry-After 秒数，解析失败返回 0。
func ParseRetryAfter(value string) time.Duration {
	seconds, err := strconv.Atoi(strings.TrimSpace(value))
	if err != nil || seconds <= 0 {
		return 0
	}

	return time.Duration(seconds) * time.Second
}
