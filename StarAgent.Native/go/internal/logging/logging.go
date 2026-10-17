package logging

import (
	"fmt"
	"io"
	"log/slog"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"
)

// Logger 是带按天滚动文件输出的日志器。
type Logger struct {
	*slog.Logger
	writer *dailyWriter
}

// New 创建日志器。日志写入 dir/staragent-YYYY-MM-DD.log，保留 keepDays 天。
func New(dir, level string, console bool) (*Logger, error) {
	lvl, err := parseLevel(level)
	if err != nil {
		return nil, err
	}
	if err := os.MkdirAll(dir, 0o700); err != nil {
		return nil, fmt.Errorf("create log directory %s: %w", dir, err)
	}

	writer := &dailyWriter{
		dir:      dir,
		prefix:   "staragent-",
		keepDays: 7,
	}
	var out io.Writer = writer
	if console {
		out = io.MultiWriter(writer, os.Stderr)
	}

	handler := slog.NewTextHandler(out, &slog.HandlerOptions{Level: lvl})
	return &Logger{Logger: slog.New(handler), writer: writer}, nil
}

// NewConsole 创建只输出到 stderr 的日志器，用于日志目录不可用时降级。
func NewConsole(level string) *Logger {
	lvl, err := parseLevel(level)
	if err != nil {
		lvl = slog.LevelInfo
	}

	handler := slog.NewTextHandler(os.Stderr, &slog.HandlerOptions{Level: lvl})
	return &Logger{Logger: slog.New(handler)}
}

// Close 关闭日志文件。
func (l *Logger) Close() error {
	if l == nil || l.writer == nil {
		return nil
	}

	return l.writer.Close()
}

func parseLevel(level string) (slog.Level, error) {
	switch strings.ToLower(strings.TrimSpace(level)) {
	case "", "info":
		return slog.LevelInfo, nil
	case "debug":
		return slog.LevelDebug, nil
	case "warn", "warning":
		return slog.LevelWarn, nil
	case "error":
		return slog.LevelError, nil
	default:
		return slog.LevelInfo, fmt.Errorf("unsupported log level %q", level)
	}
}

type dailyWriter struct {
	mu       sync.Mutex
	dir      string
	prefix   string
	keepDays int
	day      string
	file     *os.File
}

func (w *dailyWriter) Write(p []byte) (int, error) {
	w.mu.Lock()
	defer w.mu.Unlock()

	if err := w.rotateLocked(); err != nil {
		return 0, err
	}

	return w.file.Write(p)
}

func (w *dailyWriter) Close() error {
	w.mu.Lock()
	defer w.mu.Unlock()

	if w.file == nil {
		return nil
	}

	err := w.file.Close()
	w.file = nil
	return err
}

func (w *dailyWriter) rotateLocked() error {
	now := time.Now()
	day := now.Format("2006-01-02")
	if w.file != nil && w.day == day {
		return nil
	}

	if w.file != nil {
		_ = w.file.Close()
		w.file = nil
	}

	if err := os.MkdirAll(w.dir, 0o700); err != nil {
		return fmt.Errorf("create log directory %s: %w", w.dir, err)
	}

	path := filepath.Join(w.dir, w.prefix+day+".log")
	file, err := os.OpenFile(path, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0o600)
	if err != nil {
		return fmt.Errorf("open log file %s: %w", path, err)
	}

	w.file = file
	w.day = day
	w.cleanupLocked(now)

	return nil
}

func (w *dailyWriter) cleanupLocked(now time.Time) {
	if w.keepDays <= 0 {
		return
	}

	entries, err := os.ReadDir(w.dir)
	if err != nil {
		return
	}

	cutoff := now.AddDate(0, 0, -w.keepDays)
	for _, entry := range entries {
		if entry.IsDir() || !strings.HasPrefix(entry.Name(), w.prefix) || !strings.HasSuffix(entry.Name(), ".log") {
			continue
		}

		dayText := strings.TrimSuffix(strings.TrimPrefix(entry.Name(), w.prefix), ".log")
		day, err := time.ParseInLocation("2006-01-02", dayText, time.Local)
		if err != nil {
			continue
		}
		if day.Before(cutoff) {
			_ = os.Remove(filepath.Join(w.dir, entry.Name()))
		}
	}
}
