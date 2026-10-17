package config

import (
	"encoding/json"
	"errors"
	"fmt"
	"net/url"
	"os"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"sync"
)

// Config 是 StarAgent 的持久化配置，字段名与协议保持一致。
type Config struct {
	Server          string   `json:"Server"`
	ProductCode     string   `json:"ProductCode"`
	Code            string   `json:"Code"`
	Secret          string   `json:"Secret"`
	Project         string   `json:"Project"`
	Channel         string   `json:"Channel"`
	DataDir         string   `json:"DataDir"`
	LogLevel        string   `json:"LogLevel"`
	MetricsInterval int      `json:"MetricsInterval"`
	AllowExec       bool     `json:"AllowExec"`
	AllowedCommands []string `json:"AllowedCommands"`
	CommandTimeout  int      `json:"CommandTimeout"`
	TlsVerify       bool     `json:"TlsVerify"`
}

// Default 返回默认配置。
func Default() Config {
	return Config{
		ProductCode:     "StarAgent-Go",
		Channel:         "Release",
		DataDir:         defaultDataDir(),
		LogLevel:        "info",
		MetricsInterval: 60,
		AllowExec:       false,
		AllowedCommands: []string{},
		CommandTimeout:  30,
		TlsVerify:       true,
	}
}

// DefaultPath 返回默认配置文件路径，可用 STARAGENT_CONFIG 覆盖。
func DefaultPath() string {
	if path := os.Getenv("STARAGENT_CONFIG"); path != "" {
		return path
	}

	return "staragent.json"
}

// Load 读取配置文件，缺失时返回默认配置。环境变量随后覆盖。
func Load(path string) (*Config, error) {
	cfg := Default()

	data, err := os.ReadFile(path)
	if err != nil {
		if !errors.Is(err, os.ErrNotExist) {
			return nil, fmt.Errorf("read config %s: %w", path, err)
		}
	} else if err := json.Unmarshal(data, &cfg); err != nil {
		return nil, fmt.Errorf("decode config %s: %w", path, err)
	}

	if err := cfg.ApplyEnv(os.LookupEnv); err != nil {
		return nil, err
	}
	cfg.Normalize()

	return &cfg, nil
}

// Normalize 修正缺省值并清理字段。
func (c *Config) Normalize() {
	c.Server = strings.TrimRight(strings.TrimSpace(c.Server), "/")
	c.ProductCode = strings.TrimSpace(c.ProductCode)
	if c.ProductCode == "" {
		c.ProductCode = "StarAgent-Go"
	}
	c.Channel = strings.TrimSpace(c.Channel)
	if c.Channel == "" {
		c.Channel = "Release"
	}
	c.LogLevel = strings.TrimSpace(c.LogLevel)
	if c.LogLevel == "" {
		c.LogLevel = "info"
	}
	if c.DataDir == "" {
		c.DataDir = defaultDataDir()
	}
	if c.MetricsInterval <= 0 {
		c.MetricsInterval = 60
	}
	if c.CommandTimeout <= 0 {
		c.CommandTimeout = 30
	}
	if c.AllowedCommands == nil {
		c.AllowedCommands = []string{}
	}
	for i := range c.AllowedCommands {
		c.AllowedCommands[i] = strings.TrimSpace(c.AllowedCommands[i])
	}
}

// Validate 校验运行所需的关键配置。
func (c *Config) Validate() error {
	if c.Server == "" {
		return errors.New("Server is required (configure staragent.json or STARAGENT_SERVER)")
	}
	u, err := url.Parse(c.Server)
	if err != nil || u.Scheme == "" || u.Host == "" {
		return fmt.Errorf("invalid Server %q", c.Server)
	}
	if u.Scheme != "http" && u.Scheme != "https" {
		return fmt.Errorf("unsupported Server scheme %q", u.Scheme)
	}
	if c.ProductCode == "" {
		return errors.New("ProductCode is required")
	}
	if c.MetricsInterval < 5 || c.MetricsInterval > 3600 {
		return fmt.Errorf("MetricsInterval must be between 5 and 3600 seconds, got %d", c.MetricsInterval)
	}
	if c.CommandTimeout < 1 || c.CommandTimeout > 300 {
		return fmt.Errorf("CommandTimeout must be between 1 and 300 seconds, got %d", c.CommandTimeout)
	}

	return nil
}

// ApplyEnv 使用 STARAGENT_ 前缀的环境变量覆盖配置。
func (c *Config) ApplyEnv(lookup func(string) (string, bool)) error {
	setString := func(name string, target *string) {
		if v, ok := lookup(envName(name)); ok {
			*target = v
		}
	}
	setString("Server", &c.Server)
	setString("ProductCode", &c.ProductCode)
	setString("Code", &c.Code)
	setString("Secret", &c.Secret)
	setString("Project", &c.Project)
	setString("Channel", &c.Channel)
	setString("DataDir", &c.DataDir)
	setString("LogLevel", &c.LogLevel)

	if v, ok := lookup(envName("MetricsInterval")); ok {
		n, err := strconv.Atoi(strings.TrimSpace(v))
		if err != nil {
			return fmt.Errorf("invalid %s=%q: %w", envName("MetricsInterval"), v, err)
		}
		c.MetricsInterval = n
	}
	if v, ok := lookup(envName("AllowExec")); ok {
		b, err := strconv.ParseBool(strings.TrimSpace(v))
		if err != nil {
			return fmt.Errorf("invalid %s=%q: %w", envName("AllowExec"), v, err)
		}
		c.AllowExec = b
	}
	if v, ok := lookup(envName("AllowedCommands")); ok {
		c.AllowedCommands = splitList(v)
	}
	if v, ok := lookup(envName("CommandTimeout")); ok {
		n, err := strconv.Atoi(strings.TrimSpace(v))
		if err != nil {
			return fmt.Errorf("invalid %s=%q: %w", envName("CommandTimeout"), v, err)
		}
		c.CommandTimeout = n
	}
	if v, ok := lookup(envName("TlsVerify")); ok {
		b, err := strconv.ParseBool(strings.TrimSpace(v))
		if err != nil {
			return fmt.Errorf("invalid %s=%q: %w", envName("TlsVerify"), v, err)
		}
		c.TlsVerify = b
	}

	return nil
}

func envName(field string) string {
	return "STARAGENT_" + strings.ToUpper(field)
}

func splitList(value string) []string {
	parts := strings.FieldsFunc(value, func(r rune) bool { return r == ',' || r == ';' })
	result := make([]string, 0, len(parts))
	for _, part := range parts {
		if v := strings.TrimSpace(part); v != "" {
			result = append(result, v)
		}
	}

	return result
}

func defaultDataDir() string {
	home, _ := os.UserHomeDir()
	switch runtime.GOOS {
	case "windows":
		if dir := os.Getenv("ProgramData"); dir != "" {
			return filepath.Join(dir, "StarAgent", "data")
		}
	case "darwin":
		if os.Geteuid() == 0 {
			return "/Library/Application Support/StarAgent"
		}
		if home != "" {
			return filepath.Join(home, "Library", "Application Support", "StarAgent")
		}
	default:
		if os.Geteuid() == 0 {
			return "/var/lib/staragent"
		}
	}
	if home != "" {
		return filepath.Join(home, ".staragent")
	}

	return "data"
}

// Store 提供带并发保护的配置读写与原子持久化。
type Store struct {
	mu   sync.RWMutex
	path string
	cfg  Config
}

// NewStore 创建配置存储。
func NewStore(path string, cfg Config) *Store {
	cfg.Normalize()
	return &Store{path: path, cfg: cfg}
}

// Path 返回配置文件路径。
func (s *Store) Path() string { return s.path }

// Snapshot 返回配置副本。
func (s *Store) Snapshot() Config {
	s.mu.RLock()
	defer s.mu.RUnlock()

	cfg := s.cfg
	cfg.AllowedCommands = append([]string(nil), s.cfg.AllowedCommands...)
	return cfg
}

// Update 修改配置并原子写入文件；写入失败时不改变内存状态。
func (s *Store) Update(fn func(*Config)) (Config, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	next := s.cfg
	next.AllowedCommands = append([]string(nil), s.cfg.AllowedCommands...)
	fn(&next)
	next.Normalize()

	if err := Save(s.path, &next); err != nil {
		return s.cfg, err
	}

	s.cfg = next
	cfg := next
	cfg.AllowedCommands = append([]string(nil), next.AllowedCommands...)
	return cfg, nil
}

// Save 将配置原子写入 path，权限为 0600。
func Save(path string, cfg *Config) error {
	cfg.Normalize()

	data, err := json.MarshalIndent(cfg, "", "  ")
	if err != nil {
		return fmt.Errorf("encode config: %w", err)
	}
	data = append(data, '\n')

	if err := WriteFileAtomic(path, data, 0o600); err != nil {
		return fmt.Errorf("save config %s: %w", path, err)
	}

	return nil
}

// WriteFileAtomic 在目标目录创建临时文件后重命名，避免半写文件。
func WriteFileAtomic(path string, data []byte, perm os.FileMode) error {
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0o700); err != nil {
		return fmt.Errorf("create directory %s: %w", dir, err)
	}

	file, err := os.CreateTemp(dir, ".staragent-*.tmp")
	if err != nil {
		return fmt.Errorf("create temp file: %w", err)
	}
	tempPath := file.Name()
	cleanup := func() {
		_ = file.Close()
		_ = os.Remove(tempPath)
	}

	if err := file.Chmod(perm); err != nil {
		cleanup()
		return fmt.Errorf("chmod temp file: %w", err)
	}
	if _, err := file.Write(data); err != nil {
		cleanup()
		return fmt.Errorf("write temp file: %w", err)
	}
	if err := file.Sync(); err != nil {
		cleanup()
		return fmt.Errorf("sync temp file: %w", err)
	}
	if err := file.Close(); err != nil {
		_ = os.Remove(tempPath)
		return fmt.Errorf("close temp file: %w", err)
	}
	if err := os.Rename(tempPath, path); err != nil {
		_ = os.Remove(tempPath)
		return fmt.Errorf("replace %s: %w", path, err)
	}
	if err := os.Chmod(path, perm); err != nil {
		return fmt.Errorf("chmod %s: %w", path, err)
	}

	return nil
}
