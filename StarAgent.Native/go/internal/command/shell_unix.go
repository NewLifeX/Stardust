//go:build !windows

package command

import (
	"context"
	"os"
	"os/exec"
	"syscall"
	"time"
)

func shellSupported(shell string) bool { return shell == "bash" }

func platformRunShell(ctx context.Context, _ string, script string, _ time.Duration) (string, int, bool, error) {
	path := "/bin/bash"
	if _, err := os.Stat(path); err != nil {
		path = "bash"
	}

	cmd := exec.Command(path, "-c", script)
	cmd.SysProcAttr = &syscall.SysProcAttr{Setpgid: true}

	return runProcess(ctx, cmd, func() error {
		if cmd.Process == nil {
			return nil
		}
		return syscall.Kill(-cmd.Process.Pid, syscall.SIGKILL)
	})
}
