package scenario

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
	"time"

	"staragent-conformance/internal/fakeserver"
	"staragent-conformance/internal/protocol"
)

// Control is a small client for the fake server's /control/* endpoints.
type Control struct {
	BaseURL string
	Client  *http.Client
}

// NewControl creates a control client for a running fake server.
func NewControl(baseURL string) *Control {
	return &Control{
		BaseURL: strings.TrimRight(baseURL, "/"),
		Client:  &http.Client{Timeout: 10 * time.Second},
	}
}

func (c *Control) do(ctx context.Context, method, path string, in any, out any) error {
	var body io.Reader
	if in != nil {
		data, err := json.Marshal(in)
		if err != nil {
			return err
		}
		body = bytes.NewReader(data)
	}
	req, err := http.NewRequestWithContext(ctx, method, c.BaseURL+path, body)
	if err != nil {
		return err
	}
	if in != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	resp, err := c.Client.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()
	data, _ := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return fmt.Errorf("control %s: HTTP %d: %s", path, resp.StatusCode, protocol.Truncate(string(data), 300))
	}
	if out != nil && len(data) > 0 {
		if err := json.Unmarshal(data, out); err != nil {
			return fmt.Errorf("control %s: decode: %w", path, err)
		}
	}

	return nil
}

// State returns the current fake server snapshot.
func (c *Control) State(ctx context.Context) (*fakeserver.State, error) {
	var st fakeserver.State
	if err := c.do(ctx, http.MethodGet, "/control/state", nil, &st); err != nil {
		return nil, err
	}

	return &st, nil
}

// Command enqueues a command. via is "ws", "http" or "auto".
func (c *Control) Command(ctx context.Context, cmd protocol.CommandModel, via string) error {
	path := "/control/command"
	if via != "" {
		path += "?via=" + via
	}

	return c.do(ctx, http.MethodPost, path, cmd, nil)
}

// SetPing updates ping response options and can enqueue HTTP fallback commands.
func (c *Control) SetPing(ctx context.Context, period *int, newServer *string, commands []protocol.CommandModel, rotateToken *bool) error {
	req := map[string]any{}
	if period != nil {
		req["Period"] = *period
	}
	if newServer != nil {
		req["NewServer"] = *newServer
	}
	if commands != nil {
		req["Commands"] = commands
	}
	if rotateToken != nil {
		req["RotateToken"] = *rotateToken
	}

	return c.do(ctx, http.MethodPost, "/control/ping", req, nil)
}

// Kick forcibly closes all active WebSocket connections.
func (c *Control) Kick(ctx context.Context) error {
	return c.do(ctx, http.MethodPost, "/control/kick", map[string]any{}, nil)
}

// FailEvents makes the next count postevents calls fail with status.
func (c *Control) FailEvents(ctx context.Context, count, status int) error {
	return c.do(ctx, http.MethodPost, "/control/events-fail", map[string]any{"Count": count, "Status": status}, nil)
}

// SetWSDisabled enables or disables the WebSocket endpoint.
func (c *Control) SetWSDisabled(ctx context.Context, disabled bool) error {
	return c.do(ctx, http.MethodPost, "/control/ws", map[string]any{"Disabled": disabled}, nil)
}

// SetUpgrade configures the response of /node/upgrade.
func (c *Control) SetUpgrade(ctx context.Context, info *protocol.UpgradeInfo) error {
	if info == nil {
		return c.do(ctx, http.MethodPost, "/control/upgrade", map[string]any{"Clear": true}, nil)
	}

	return c.do(ctx, http.MethodPost, "/control/upgrade", info, nil)
}
