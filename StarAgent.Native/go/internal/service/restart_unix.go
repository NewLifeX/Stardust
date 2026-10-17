//go:build !windows

package service

import (
	"fmt"
	"os"
	"os/exec"
	"syscall"
)

// RestartProcess 用当前可执行文件与参数重新拉起进程，然后退出当前进程。
func RestartProcess() error {
	exe, err := os.Executable()
	if err != nil {
		return fmt.Errorf("resolve executable: %w", err)
	}

	cmd := exec.Command(exe, os.Args[1:]...)
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	cmd.Env = append(os.Environ(), "STARAGENT_RESTARTED=1")
	cmd.SysProcAttr = &syscall.SysProcAttr{Setsid: true}

	if err := cmd.Start(); err != nil {
		return fmt.Errorf("start new process: %w", err)
	}

	os.Exit(0)
	return nil
}
