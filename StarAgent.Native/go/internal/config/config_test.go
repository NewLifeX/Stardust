package config

import (
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

func TestDefaults(t *testing.T) {
	cfg := Default()
	if cfg.ProductCode != "StarAgent-Go" || cfg.Channel != "Release" {
		t.Fatalf("unexpected defaults: %+v", cfg)
	}
	if cfg.MetricsInterval != 60 || cfg.CommandTimeout != 30 {
		t.Fatalf("unexpected intervals: %+v", cfg)
	}
	if cfg.AllowExec {
		t.Fatal("AllowExec must default to false")
	}
	if !cfg.TlsVerify {
		t.Fatal("TlsVerify must default to true")
	}
	if cfg.AllowedCommands == nil {
		t.Fatal("AllowedCommands should be an empty slice")
	}
}

func TestApplyEnvOverrides(t *testing.T) {
	t.Setenv("STARAGENT_SERVER", "https://star.example.com:6600/")
	t.Setenv("STARAGENT_PRODUCTCODE", "StarAgent-Test")
	t.Setenv("STARAGENT_CODE", "NODE01")
	t.Setenv("STARAGENT_SECRET", "s3cr3t")
	t.Setenv("STARAGENT_PROJECT", "demo")
	t.Setenv("STARAGENT_CHANNEL", "Beta")
	t.Setenv("STARAGENT_DATADIR", `C:\data\staragent`)
	t.Setenv("STARAGENT_LOGLEVEL", "debug")
	t.Setenv("STARAGENT_METRICSINTERVAL", "30")
	t.Setenv("STARAGENT_ALLOWEXEC", "true")
	t.Setenv("STARAGENT_ALLOWEDCOMMANDS", "bash, node/synctime;node/setchannel")
	t.Setenv("STARAGENT_COMMANDTIMEOUT", "45")
	t.Setenv("STARAGENT_TLSVERIFY", "false")

	cfg, err := Load(filepath.Join(t.TempDir(), "missing.json"))
	if err != nil {
		t.Fatalf("load: %v", err)
	}

	if cfg.Server != "https://star.example.com:6600" {
		t.Fatalf("Server = %q", cfg.Server)
	}
	if cfg.ProductCode != "StarAgent-Test" || cfg.Code != "NODE01" || cfg.Secret != "s3cr3t" {
		t.Fatalf("identity overrides failed: %+v", cfg)
	}
	if cfg.Project != "demo" || cfg.Channel != "Beta" || cfg.LogLevel != "debug" {
		t.Fatalf("string overrides failed: %+v", cfg)
	}
	if cfg.MetricsInterval != 30 || cfg.CommandTimeout != 45 {
		t.Fatalf("number overrides failed: %+v", cfg)
	}
	if !cfg.AllowExec || cfg.TlsVerify {
		t.Fatalf("bool overrides failed: %+v", cfg)
	}
	want := []string{"bash", "node/synctime", "node/setchannel"}
	if len(cfg.AllowedCommands) != len(want) {
		t.Fatalf("AllowedCommands = %v", cfg.AllowedCommands)
	}
	for i := range want {
		if cfg.AllowedCommands[i] != want[i] {
			t.Fatalf("AllowedCommands = %v", cfg.AllowedCommands)
		}
	}
}

func TestApplyEnvInvalidNumbers(t *testing.T) {
	t.Setenv("STARAGENT_METRICSINTERVAL", "abc")
	if _, err := Load(filepath.Join(t.TempDir(), "missing.json")); err == nil {
		t.Fatal("expected error for invalid MetricsInterval")
	}
}

func TestSaveLoadRoundTrip(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "nested", "staragent.json")
	cfg := Default()
	cfg.Server = "https://star.example.com:6600"
	cfg.Code = "CODE1"
	cfg.Secret = "SECRET1"
	cfg.AllowExec = true
	cfg.AllowedCommands = []string{"bash"}

	if err := Save(path, &cfg); err != nil {
		t.Fatalf("save: %v", err)
	}

	info, err := os.Stat(path)
	if err != nil {
		t.Fatalf("stat: %v", err)
	}
	if runtime.GOOS != "windows" {
		if perm := info.Mode().Perm(); perm != 0o600 {
			t.Fatalf("config permission = %o, want 600", perm)
		}
	}

	loaded, err := Load(path)
	if err != nil {
		t.Fatalf("load: %v", err)
	}
	if loaded.Server != cfg.Server || loaded.Code != "CODE1" || loaded.Secret != "SECRET1" {
		t.Fatalf("round trip failed: %+v", loaded)
	}
	if !loaded.AllowExec || len(loaded.AllowedCommands) != 1 || loaded.AllowedCommands[0] != "bash" {
		t.Fatalf("round trip flags failed: %+v", loaded)
	}

	entries, err := os.ReadDir(filepath.Dir(path))
	if err != nil {
		t.Fatalf("read dir: %v", err)
	}
	for _, entry := range entries {
		if strings.HasSuffix(entry.Name(), ".tmp") {
			t.Fatalf("temporary file left behind: %s", entry.Name())
		}
	}
}

func TestLoadKeepsTlsVerifyDefault(t *testing.T) {
	path := filepath.Join(t.TempDir(), "staragent.json")
	if err := os.WriteFile(path, []byte(`{"Server":"http://127.0.0.1:6600"}`), 0o600); err != nil {
		t.Fatalf("write: %v", err)
	}

	cfg, err := Load(path)
	if err != nil {
		t.Fatalf("load: %v", err)
	}
	if !cfg.TlsVerify {
		t.Fatal("missing TlsVerify should keep default true")
	}

	if err := os.WriteFile(path, []byte(`{"Server":"http://127.0.0.1:6600","TlsVerify":false}`), 0o600); err != nil {
		t.Fatalf("write: %v", err)
	}
	cfg, err = Load(path)
	if err != nil {
		t.Fatalf("load: %v", err)
	}
	if cfg.TlsVerify {
		t.Fatal("explicit TlsVerify=false should be honored")
	}
}

func TestValidate(t *testing.T) {
	cfg := Default()
	if err := cfg.Validate(); err == nil {
		t.Fatal("expected missing Server error")
	}

	cfg.Server = "ftp://example.com"
	if err := cfg.Validate(); err == nil {
		t.Fatal("expected unsupported scheme error")
	}

	cfg.Server = "https://example.com:6600"
	cfg.MetricsInterval = 1
	if err := cfg.Validate(); err == nil {
		t.Fatal("expected MetricsInterval error")
	}

	cfg.MetricsInterval = 60
	cfg.CommandTimeout = 1000
	if err := cfg.Validate(); err == nil {
		t.Fatal("expected CommandTimeout error")
	}

	cfg.CommandTimeout = 30
	if err := cfg.Validate(); err != nil {
		t.Fatalf("valid config rejected: %v", err)
	}
}

func TestStoreUpdateFailureKeepsState(t *testing.T) {
	dir := t.TempDir()
	blocker := filepath.Join(dir, "blocked")
	if err := os.WriteFile(blocker, []byte("x"), 0o600); err != nil {
		t.Fatalf("write blocker: %v", err)
	}

	// 把配置路径指向“文件内部的子路径”，MkdirAll 在所有平台都会失败。
	cfg := Default()
	cfg.Server = "https://example.com:6600"
	store := NewStore(filepath.Join(blocker, "staragent.json"), cfg)

	if _, err := store.Update(func(c *Config) { c.Code = "NEW" }); err == nil {
		t.Fatal("expected save failure because parent path is a file")
	}
	if got := store.Snapshot().Code; got != "" {
		t.Fatalf("in-memory config changed after failed save: %q", got)
	}
}

func TestStoreIdentityPersistence(t *testing.T) {
	path := filepath.Join(t.TempDir(), "staragent.json")
	cfg := Default()
	cfg.Server = "http://127.0.0.1:6600"
	store := NewStore(path, cfg)

	got, err := store.Update(func(c *Config) {
		c.Code = "CODE9"
		c.Secret = "SECRET9"
		c.Channel = "Beta"
	})
	if err != nil {
		t.Fatalf("update: %v", err)
	}
	if got.Code != "CODE9" || got.Secret != "SECRET9" || got.Channel != "Beta" {
		t.Fatalf("unexpected snapshot: %+v", got)
	}

	loaded, err := Load(path)
	if err != nil {
		t.Fatalf("load: %v", err)
	}
	if loaded.Code != "CODE9" || loaded.Secret != "SECRET9" {
		t.Fatalf("identity not persisted: %+v", loaded)
	}
}
