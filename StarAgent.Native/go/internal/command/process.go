package command

import (
	"context"
	"os/exec"
)

func runProcess(ctx context.Context, cmd *exec.Cmd, kill func() error) (string, int, bool, error) {
	buffer := newHeadTailBuffer(MaxOutputBytes)
	cmd.Stdout = buffer
	cmd.Stderr = buffer

	if err := cmd.Start(); err != nil {
		return "", -1, false, err
	}

	done := make(chan error, 1)
	go func() { done <- cmd.Wait() }()

	select {
	case err := <-done:
		return buffer.String(), exitCode(err), buffer.Truncated(), err
	case <-ctx.Done():
		_ = kill()
		if cmd.Process != nil {
			_ = cmd.Process.Kill()
		}
		<-done
		return buffer.String(), -1, buffer.Truncated(), ctx.Err()
	}
}

func exitCode(err error) int {
	if err == nil {
		return 0
	}
	if exitErr, ok := err.(*exec.ExitError); ok {
		return exitErr.ExitCode()
	}

	return -1
}
