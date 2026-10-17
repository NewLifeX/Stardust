//go:build !windows

package command

import (
	"context"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"runtime"
	"strings"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

func defaultSyncTime(target time.Time) error {
	if os.Geteuid() != 0 {
		return errors.New("需要 root 权限")
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	var cmd *exec.Cmd
	if runtime.GOOS == "darwin" {
		// BSD date 使用 MMDDhhmm[[CC]YY] 格式。
		cmd = exec.CommandContext(ctx, "date", "-u", target.UTC().Format("010215042006"))
	} else {
		cmd = exec.CommandContext(ctx, "date", "-u", "-s", target.UTC().Format(protocol.TimeLayout))
	}

	output, err := cmd.CombinedOutput()
	if err != nil {
		return fmt.Errorf("date 执行失败: %w: %s", err, strings.TrimSpace(string(output)))
	}

	return nil
}
