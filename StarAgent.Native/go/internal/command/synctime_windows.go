//go:build windows

package command

import (
	"context"
	"fmt"
	"os/exec"
	"strings"
	"time"
)

func defaultSyncTime(target time.Time) error {
	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	script := fmt.Sprintf("Set-Date -Date '%s'", target.UTC().Format(time.RFC3339))
	cmd := exec.CommandContext(ctx, "powershell", "-NoProfile", "-NonInteractive", "-Command", script)
	output, err := cmd.CombinedOutput()
	if err != nil {
		return fmt.Errorf("Set-Date 执行失败: %w: %s", err, strings.TrimSpace(string(output)))
	}

	return nil
}
