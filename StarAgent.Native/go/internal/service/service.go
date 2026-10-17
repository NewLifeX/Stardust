package service

import (
	"errors"
	"fmt"
	"os"
	"path"
	"path/filepath"
	"strings"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
)

// ServiceName 是 Windows 服务名。
const ServiceName = "StarAgentNative"

// InstallOptions 是安装服务所需参数。
type InstallOptions struct {
	ConfigPath  string
	InstallDir  string
	DataDir     string
	ServiceUser string
	Server      string
}

// GenerateSystemdUnit 生成 systemd unit 内容。
func GenerateSystemdUnit(opts InstallOptions) string {
	exe := path.Join(opts.InstallDir, "staragent")
	cfgPath := opts.ConfigPath
	if cfgPath == "" {
		cfgPath = path.Join(opts.InstallDir, "staragent.json")
	}

	var builder strings.Builder
	builder.WriteString("[Unit]\n")
	builder.WriteString("Description=StarAgent Native Agent\n")
	builder.WriteString("After=network-online.target\n")
	builder.WriteString("Wants=network-online.target\n\n")
	builder.WriteString("[Service]\n")
	builder.WriteString("Type=simple\n")
	builder.WriteString("ExecStart=" + systemdQuote(exe) + " run -config " + systemdQuote(cfgPath) + "\n")
	builder.WriteString("WorkingDirectory=" + systemdQuote(opts.InstallDir) + "\n")
	builder.WriteString("Restart=always\n")
	builder.WriteString("RestartSec=5\n")
	builder.WriteString("KillSignal=SIGTERM\n")
	builder.WriteString("TimeoutStopSec=20\n")
	builder.WriteString("LimitNOFILE=65535\n")
	if opts.ServiceUser != "" {
		builder.WriteString("User=" + opts.ServiceUser + "\n")
	}
	builder.WriteString("\n[Install]\n")
	builder.WriteString("WantedBy=multi-user.target\n")

	return builder.String()
}

// GenerateOpenWrtScript 生成 OpenWrt procd 启动脚本。
func GenerateOpenWrtScript(opts InstallOptions) string {
	exe := path.Join(opts.InstallDir, "staragent")
	cfgPath := opts.ConfigPath
	if cfgPath == "" {
		cfgPath = path.Join(opts.InstallDir, "staragent.json")
	}

	return fmt.Sprintf(`#!/bin/sh /etc/rc.common
USE_PROCD=1
START=95
STOP=10

PROG=%q
CONFIG=%q

start_service() {
	procd_open_instance
	procd_set_param command "$PROG" run -config "$CONFIG"
	procd_set_param respawn 3600 5 5
	procd_set_param stdout 1
	procd_set_param stderr 1
	procd_close_instance
}

stop_service() {
	procd_kill staragent 0
}

reload_service() {
	stop
	start
}
`, exe, cfgPath)
}

// GenerateLaunchdPlist 生成 macOS launchd plist。
func GenerateLaunchdPlist(opts InstallOptions, logDir string) string {
	exe := path.Join(opts.InstallDir, "staragent")
	cfgPath := opts.ConfigPath
	if cfgPath == "" {
		cfgPath = path.Join(opts.InstallDir, "staragent.json")
	}
	if logDir == "" {
		logDir = path.Join(opts.InstallDir, "logs")
	}

	return fmt.Sprintf(`<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>Label</key>
	<string>com.newlifex.staragent</string>
	<key>ProgramArguments</key>
	<array>
		<string>%s</string>
		<string>run</string>
		<string>-config</string>
		<string>%s</string>
	</array>
	<key>WorkingDirectory</key>
	<string>%s</string>
	<key>RunAtLoad</key>
	<true/>
	<key>KeepAlive</key>
	<true/>
	<key>StandardOutPath</key>
	<string>%s</string>
	<key>StandardErrorPath</key>
	<string>%s</string>
</dict>
</plist>
`, xmlEscape(exe), xmlEscape(cfgPath), xmlEscape(opts.InstallDir),
		xmlEscape(path.Join(logDir, "launchd.out.log")),
		xmlEscape(path.Join(logDir, "launchd.err.log")))
}

func systemdQuote(value string) string {
	if !strings.ContainsAny(value, " \t\"'\\") {
		return value
	}

	replacer := strings.NewReplacer(`\`, `\\`, `"`, `\"`)
	return `"` + replacer.Replace(value) + `"`
}

func xmlEscape(value string) string {
	replacer := strings.NewReplacer(
		"&", "&amp;",
		"<", "&lt;",
		">", "&gt;",
		`"`, "&quot;",
		"'", "&apos;",
	)
	return replacer.Replace(value)
}

func ensureConfig(path, server, dataDir string) error {
	if _, err := os.Stat(path); err == nil {
		return nil
	} else if !errors.Is(err, os.ErrNotExist) {
		return fmt.Errorf("stat config %s: %w", path, err)
	}

	cfg := config.Default()
	cfg.Server = server
	if dataDir != "" {
		cfg.DataDir = dataDir
	}
	if err := cfg.Validate(); err != nil {
		return fmt.Errorf("cannot create initial config: %w (pass -server)", err)
	}

	if err := config.Save(path, &cfg); err != nil {
		return err
	}

	return nil
}

func copyExecutable(source, target string) error {
	if source == "" {
		exe, err := os.Executable()
		if err != nil {
			return fmt.Errorf("resolve executable: %w", err)
		}
		source = exe
	}
	if absSource, err := filepath.Abs(source); err == nil {
		if absTarget, err := filepath.Abs(target); err == nil && strings.EqualFold(absSource, absTarget) {
			return nil
		}
	}

	data, err := os.ReadFile(source)
	if err != nil {
		return fmt.Errorf("read executable %s: %w", source, err)
	}
	if err := config.WriteFileAtomic(target, data, 0o755); err != nil {
		return fmt.Errorf("install executable %s: %w", target, err)
	}

	return nil
}

func absolutePath(path string) (string, error) {
	if path == "" {
		return "", nil
	}
	if filepath.IsAbs(path) {
		return path, nil
	}

	return filepath.Abs(path)
}
