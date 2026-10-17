package logging

import (
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
	"time"
)

func TestDailyLogFile(t *testing.T) {
	dir := t.TempDir()
	logger, err := New(dir, "debug", false)
	if err != nil {
		t.Fatalf("new logger: %v", err)
	}
	logger.Info("hello", "key", "value")
	if err := logger.Close(); err != nil {
		t.Fatalf("close: %v", err)
	}

	name := "staragent-" + time.Now().Format("2006-01-02") + ".log"
	path := filepath.Join(dir, name)
	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("read log: %v", err)
	}
	if !strings.Contains(string(data), "hello") || !strings.Contains(string(data), "key=value") {
		t.Fatalf("unexpected log content: %s", data)
	}

	if runtime.GOOS != "windows" {
		info, err := os.Stat(path)
		if err != nil {
			t.Fatalf("stat log: %v", err)
		}
		if info.Mode().Perm() != 0o600 {
			t.Fatalf("log permission = %o", info.Mode().Perm())
		}
	}
}

func TestCleanupOldLogs(t *testing.T) {
	dir := t.TempDir()
	old := filepath.Join(dir, "staragent-2000-01-01.log")
	if err := os.WriteFile(old, []byte("old"), 0o600); err != nil {
		t.Fatalf("write old log: %v", err)
	}

	logger, err := New(dir, "info", false)
	if err != nil {
		t.Fatalf("new logger: %v", err)
	}
	logger.Info("new")
	_ = logger.Close()

	if _, err := os.Stat(old); !os.IsNotExist(err) {
		t.Fatalf("old log was not removed: %v", err)
	}
}

func TestInvalidLevel(t *testing.T) {
	if _, err := New(t.TempDir(), "verbose", false); err == nil {
		t.Fatal("expected error for invalid log level")
	}
}
