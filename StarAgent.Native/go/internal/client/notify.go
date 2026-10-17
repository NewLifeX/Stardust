package client

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"net/http"
	"net/url"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	"github.com/coder/websocket"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

// NotifyHandler 是 WebSocket 通知回调。
type NotifyHandler struct {
	Interval  time.Duration
	OnCommand func(ctx context.Context, cmd protocol.CommandModel)
	OnPong    func()
	Logger    *slog.Logger
}

// RunNotify 建立并维持一条 /node/notify WebSocket 连接，直到连接关闭或 context 取消。
func (c *Client) RunNotify(ctx context.Context, handler NotifyHandler) error {
	if handler.Interval <= 0 {
		handler.Interval = 60 * time.Second
	}
	logger := handler.Logger
	if logger == nil {
		logger = c.logger
	}

	notifyURL, err := notifyURL(c.BaseURL())
	if err != nil {
		return err
	}

	c.mu.RLock()
	token := c.token
	c.mu.RUnlock()
	if token == "" {
		return errors.New("notify: missing token")
	}

	dialCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
	defer cancel()

	conn, response, err := websocket.Dial(dialCtx, notifyURL, &websocket.DialOptions{
		HTTPHeader: http.Header{"Authorization": []string{"Bearer " + token}},
	})
	if err != nil {
		if response != nil {
			return fmt.Errorf("notify dial: %w (http %d)", err, response.StatusCode)
		}
		return fmt.Errorf("notify dial: %w", err)
	}
	defer conn.CloseNow()

	conn.SetReadLimit(1 << 20)
	logger.Info("notify websocket connected", "url", notifyURL)

	var writeMu sync.Mutex
	write := func(value string) error {
		writeCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
		defer cancel()

		writeMu.Lock()
		defer writeMu.Unlock()
		return conn.Write(writeCtx, websocket.MessageText, []byte(value))
	}

	var lastPong atomic.Int64
	lastPong.Store(time.Now().UnixNano())

	done := make(chan struct{})
	var closeOnce sync.Once
	closeConn := func() {
		closeOnce.Do(func() {
			_ = conn.CloseNow()
			close(done)
		})
	}
	defer closeConn()

	keepaliveDone := make(chan struct{})
	go func() {
		defer close(keepaliveDone)
		ticker := time.NewTicker(handler.Interval)
		defer ticker.Stop()

		for {
			select {
			case <-ctx.Done():
				closeConn()
				return
			case <-done:
				return
			case <-ticker.C:
				if time.Since(time.Unix(0, lastPong.Load())) > 3*handler.Interval {
					logger.Warn("notify pong timeout, closing connection")
					closeConn()
					return
				}
				if err := write("Ping"); err != nil {
					logger.Warn("notify ping failed", "error", err)
					closeConn()
					return
				}
			}
		}
	}()
	defer func() {
		closeConn()
		<-keepaliveDone
	}()

	for {
		messageType, data, err := conn.Read(ctx)
		if err != nil {
			if ctx.Err() != nil {
				return ctx.Err()
			}
			return fmt.Errorf("notify read: %w", err)
		}

		switch messageType {
		case websocket.MessageText:
			text := strings.TrimSpace(string(data))
			switch text {
			case "Ping":
				if err := write("Pong"); err != nil {
					return fmt.Errorf("notify pong: %w", err)
				}
			case "Pong":
				lastPong.Store(time.Now().UnixNano())
				if handler.OnPong != nil {
					handler.OnPong()
				}
			default:
				var cmd protocol.CommandModel
				if err := json.Unmarshal(data, &cmd); err != nil {
					logger.Warn("notify json ignored", "error", err)
					continue
				}
				if strings.TrimSpace(cmd.Command) == "" {
					logger.Warn("notify command ignored: empty command", "id", cmd.Id)
					continue
				}
				if handler.OnCommand != nil {
					command := cmd
					go handler.OnCommand(ctx, command)
				}
			}
		case websocket.MessageBinary:
			logger.Debug("notify binary frame ignored", "bytes", len(data))
		default:
			logger.Debug("notify frame ignored", "type", messageType)
		}
	}
}

func notifyURL(base string) (string, error) {
	u, err := url.Parse(base)
	if err != nil {
		return "", fmt.Errorf("parse server url: %w", err)
	}

	switch u.Scheme {
	case "http":
		u.Scheme = "ws"
	case "https":
		u.Scheme = "wss"
	default:
		return "", fmt.Errorf("unsupported server scheme %q", u.Scheme)
	}
	u.Path = strings.TrimRight(u.Path, "/") + "/node/notify"

	return u.String(), nil
}
