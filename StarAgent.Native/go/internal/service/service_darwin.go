//go:build darwin

package service

import (
	"fmt"
	"os"
	"os/exec"
	"path/filepath"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
)

const launchdLabel = "com.newlifex.staragent"

// Install 安装 launchd plist。
func Install(opts InstallOptions) error {
	if opts.InstallDir == "" {
		if os.Geteuid() == 0 {
			opts.InstallDir = "/usr/local/staragent"
		} else {
			home, _ := os.UserHomeDir()
			opts.InstallDir = filepath.Join(home, "Library", "Application Support", "StarAgent")
		}
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

	plistPath, err := launchdPlistPath()
	if err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(plistPath), 0o755); err != nil {
		return fmt.Errorf("create launchd directory: %w", err)
	}
	plist := GenerateLaunchdPlist(opts, filepath.Join(opts.InstallDir, "logs"))
	if err := config.WriteFileAtomic(plistPath, []byte(plist), 0o644); err != nil {
		return fmt.Errorf("write launchd plist: %w", err)
	}

	_ = exec.Command("launchctl", "unload", "-w", plistPath).Run()
	if output, err := exec.Command("launchctl", "load", "-w", plistPath).CombinedOutput(); err != nil {
		return fmt.Errorf("launchctl load: %w: %s", err, string(output))
	}

	return nil
}

// Uninstall 移除 launchd plist。
func Uninstall() error {
	plistPath, err := launchdPlistPath()
	if err != nil {
		return err
	}
	_ = exec.Command("launchctl", "unload", "-w", plistPath).Run()
	if err := os.Remove(plistPath); err != nil && !os.IsNotExist(err) {
		return fmt.Errorf("remove plist: %w", err)
	}

	return nil
}

// Start 加载 launchd 服务。
func Start() error {
	plistPath, err := launchdPlistPath()
	if err != nil {
		return err
	}
	output, err := exec.Command("launchctl", "load", "-w", plistPath).CombinedOutput()
	if err != nil {
		return fmt.Errorf("launchctl load: %w: %s", err, string(output))
	}

	return nil
}

// Stop 卸载 launchd 服务。
func Stop() error {
	plistPath, err := launchdPlistPath()
	if err != nil {
		return err
	}
	output, err := exec.Command("launchctl", "unload", "-w", plistPath).CombinedOutput()
	if err != nil {
		return fmt.Errorf("launchctl unload: %w: %s", err, string(output))
	}

	return nil
}

// Status 查询 launchd 服务。
func Status() (string, error) {
	output, err := exec.Command("launchctl", "list", launchdLabel).CombinedOutput()
	return string(output), err
}

func launchdPlistPath() (string, error) {
	if os.Geteuid() == 0 {
		return filepath.Join("/Library/LaunchDaemons", launchdLabel+".plist"), nil
	}
	home, err := os.UserHomeDir()
	if err != nil {
		return "", fmt.Errorf("resolve home directory: %w", err)
	}

	return filepath.Join(home, "Library", "LaunchAgents", launchdLabel+".plist"), nil
}
