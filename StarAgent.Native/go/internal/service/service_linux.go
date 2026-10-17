//go:build linux

package service

import (
	"fmt"
	"os"
	"os/exec"
	"os/user"
	"path/filepath"
	"strconv"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
)

const (
	systemdUnitPath = "/etc/systemd/system/staragent.service"
	openWrtInitPath = "/etc/init.d/staragent"
)

// Install 安装 systemd 服务或 OpenWrt procd 服务。
func Install(opts InstallOptions) error {
	if os.Geteuid() != 0 {
		return fmt.Errorf("install requires root privileges")
	}
	if opts.InstallDir == "" {
		opts.InstallDir = "/opt/staragent"
	}
	if opts.ConfigPath == "" {
		opts.ConfigPath = filepath.Join(opts.InstallDir, "staragent.json")
	}
	var err error
	if opts.InstallDir, err = absolutePath(opts.InstallDir); err != nil {
		return err
	}
	if opts.ConfigPath, err = absolutePath(opts.ConfigPath); err != nil {
		return err
	}

	if err := os.MkdirAll(opts.InstallDir, 0o755); err != nil {
		return fmt.Errorf("create install directory: %w", err)
	}
	if err := copyExecutable("", filepath.Join(opts.InstallDir, "staragent")); err != nil {
		return err
	}
	if err := ensureConfig(opts.ConfigPath, opts.Server, opts.DataDir); err != nil {
		return err
	}
	if err := chownServiceUser(opts); err != nil {
		return err
	}

	if isOpenWrt() {
		return installOpenWrt(opts)
	}

	unit := GenerateSystemdUnit(opts)
	if err := config.WriteFileAtomic(systemdUnitPath, []byte(unit), 0o644); err != nil {
		return fmt.Errorf("write systemd unit: %w", err)
	}
	if err := runCommand("systemctl", "daemon-reload"); err != nil {
		return err
	}
	if err := runCommand("systemctl", "enable", "staragent"); err != nil {
		return err
	}
	if err := runCommand("systemctl", "restart", "staragent"); err != nil {
		return err
	}

	return nil
}

func installOpenWrt(opts InstallOptions) error {
	script := GenerateOpenWrtScript(opts)
	if err := config.WriteFileAtomic(openWrtInitPath, []byte(script), 0o755); err != nil {
		return fmt.Errorf("write OpenWrt init script: %w", err)
	}
	if err := runCommand(openWrtInitPath, "enable"); err != nil {
		return err
	}
	_ = runCommand(openWrtInitPath, "stop")
	if err := runCommand(openWrtInitPath, "start"); err != nil {
		return err
	}

	return nil
}

// Uninstall 停止并移除服务定义，保留配置与数据。
func Uninstall() error {
	if isOpenWrt() {
		_ = runCommand(openWrtInitPath, "stop")
		_ = runCommand(openWrtInitPath, "disable")
		if err := os.Remove(openWrtInitPath); err != nil && !os.IsNotExist(err) {
			return fmt.Errorf("remove init script: %w", err)
		}
		return nil
	}

	_ = runCommand("systemctl", "stop", "staragent")
	_ = runCommand("systemctl", "disable", "staragent")
	if err := os.Remove(systemdUnitPath); err != nil && !os.IsNotExist(err) {
		return fmt.Errorf("remove systemd unit: %w", err)
	}
	_ = runCommand("systemctl", "daemon-reload")

	return nil
}

// Start 启动服务。
func Start() error {
	if isOpenWrt() {
		return runCommand(openWrtInitPath, "start")
	}

	return runCommand("systemctl", "start", "staragent")
}

// Stop 停止服务。
func Stop() error {
	if isOpenWrt() {
		return runCommand(openWrtInitPath, "stop")
	}

	return runCommand("systemctl", "stop", "staragent")
}

// Status 返回服务状态。
func Status() (string, error) {
	if isOpenWrt() {
		output, err := exec.Command(openWrtInitPath, "status").CombinedOutput()
		return string(output), err
	}

	output, err := exec.Command("systemctl", "is-active", "staragent").CombinedOutput()
	return string(output), err
}

func chownServiceUser(opts InstallOptions) error {
	if opts.ServiceUser == "" {
		return nil
	}

	account, err := user.Lookup(opts.ServiceUser)
	if err != nil {
		return fmt.Errorf("lookup service user %q: %w", opts.ServiceUser, err)
	}
	uid, err := strconv.Atoi(account.Uid)
	if err != nil {
		return fmt.Errorf("parse uid %q: %w", account.Uid, err)
	}
	gid, err := strconv.Atoi(account.Gid)
	if err != nil {
		return fmt.Errorf("parse gid %q: %w", account.Gid, err)
	}

	if err := os.Chown(opts.ConfigPath, uid, gid); err != nil {
		return fmt.Errorf("chown config: %w", err)
	}

	dataDir := opts.DataDir
	if dataDir == "" {
		if cfg, err := config.Load(opts.ConfigPath); err == nil {
			dataDir = cfg.DataDir
		}
	}
	if dataDir != "" {
		if err := os.MkdirAll(dataDir, 0o700); err != nil {
			return fmt.Errorf("create data directory: %w", err)
		}
		if err := os.Chown(dataDir, uid, gid); err != nil {
			return fmt.Errorf("chown data directory: %w", err)
		}
	}

	return nil
}

func isOpenWrt() bool {
	if _, err := os.Stat("/etc/openwrt_release"); err == nil {
		return true
	}
	if _, err := os.Stat("/etc/rc.common"); err == nil {
		if _, err := os.Stat("/sbin/procd"); err == nil {
			return true
		}
	}

	return false
}

func runCommand(name string, args ...string) error {
	output, err := exec.Command(name, args...).CombinedOutput()
	if err != nil {
		return fmt.Errorf("%s %v: %w: %s", name, args, err, string(output))
	}

	return nil
}
