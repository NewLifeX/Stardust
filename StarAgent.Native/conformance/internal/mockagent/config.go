package mockagent

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"sync"
)

// configFile is a minimal staragent.json reader/writer that preserves unknown
// fields while persisting Code/Secret across restarts.
type configFile struct {
	mu   sync.Mutex
	path string
	raw  map[string]any
}

func loadConfig(path string) (*configFile, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("read config: %w", err)
	}
	raw := map[string]any{}
	if len(data) > 0 {
		if err := json.Unmarshal(data, &raw); err != nil {
			return nil, fmt.Errorf("parse config %s: %w", path, err)
		}
	}

	return &configFile{path: path, raw: raw}, nil
}

func (c *configFile) string(key string) string {
	c.mu.Lock()
	defer c.mu.Unlock()
	if v, ok := c.raw[key]; ok {
		if s, ok := v.(string); ok {
			return s
		}
	}

	return ""
}

func (c *configFile) int(key string, def int) int {
	c.mu.Lock()
	defer c.mu.Unlock()
	if v, ok := c.raw[key]; ok {
		switch n := v.(type) {
		case float64:
			return int(n)
		case json.Number:
			if i, err := n.Int64(); err == nil {
				return int(i)
			}
		case string:
			if i, err := strconv.Atoi(n); err == nil {
				return i
			}
		}
	}

	return def
}

func (c *configFile) bool(key string, def bool) bool {
	c.mu.Lock()
	defer c.mu.Unlock()
	if v, ok := c.raw[key]; ok {
		if b, ok := v.(bool); ok {
			return b
		}
	}

	return def
}

func (c *configFile) persistSession(code, secret string) error {
	c.mu.Lock()
	defer c.mu.Unlock()
	if code != "" {
		c.raw["Code"] = code
	}
	if secret != "" {
		c.raw["Secret"] = secret
	}

	return c.saveLocked()
}

func (c *configFile) persistServer(server string) error {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.raw["Server"] = server

	return c.saveLocked()
}

func (c *configFile) saveLocked() error {
	data, err := json.MarshalIndent(c.raw, "", "  ")
	if err != nil {
		return err
	}
	data = append(data, '\n')
	if dir := filepath.Dir(c.path); dir != "." && dir != "" {
		_ = os.MkdirAll(dir, 0o755)
	}
	tmp := c.path + ".tmp"
	if err := os.WriteFile(tmp, data, 0o600); err != nil {
		return err
	}

	return os.Rename(tmp, c.path)
}
