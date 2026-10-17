//go:build windows

package command

import (
	"context"
	"os"
	"os/exec"
	"strconv"
	"syscall"
	"time"
)

const createNoWindow = 0x08000000

func shellSupported(shell string) bool { return shell == "cmd" }

func platformRunShell(ctx context.Context, _ string, script string, _ time.Duration) (string, int, bool, error) {
	comspec := os.Getenv("COMSPEC")
	if comspec == "" {
		comspec = "cmd.exe"
	}

	cmd := exec.Command(comspec, "/c", script)
	cmd.SysProcAttr = &syscall.SysProcAttr{CreationFlags: createNoWindow}

	return runProcess(ctx, cmd, func() error {
		if cmd.Process == nil {
			return nil
		}

		kill := exec.Command("taskkill", "/T", "/F", "/PID", strconv.Itoa(cmd.Process.Pid))
		kill.SysProcAttr = &syscall.SysProcAttr{CreationFlags: createNoWindow}
		_ = kill.Run()

		return cmd.Process.Kill()
	})
}
