package command

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"strings"
	"sync"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

const (
	dedupTTL        = time.Hour
	maxArgumentSize = 8 * 1024
	maxConcurrency  = 4
)

// ShellRunner 执行 shell 脚本并返回合并后的输出。
type ShellRunner func(ctx context.Context, shell, script string, timeout time.Duration) (output string, exitCode int, truncated bool, err error)

// Options 是命令执行器配置。
type Options struct {
	AllowExec       bool
	AllowedCommands []string
	Timeout         time.Duration

	// ServerTime 返回服务端当前时间，用于 node/synctime 无参数场景。
	ServerTime func() time.Time
	// SetChannel 持久化更新通道。
	SetChannel func(string) error
	// SyncTime 设置系统时间。
	SyncTime func(time.Time) error
	// Restart 用当前可执行文件重新拉起进程。
	Restart func() error
	// RunShell 覆盖 shell 执行器，主要用于测试。
	RunShell ShellRunner
	// Now 覆盖时钟，主要用于测试。
	Now func() time.Time
	// Logger 记录审计日志。
	Logger *slog.Logger
}

// Executor 按协议规则执行命令。
type Executor struct {
	opts Options

	mu      sync.Mutex
	seen    map[int64]time.Time
	sem     chan struct{}
	shellMu sync.Mutex
}

// New 创建命令执行器。
func New(opts Options) *Executor {
	if opts.Timeout <= 0 {
		opts.Timeout = 30 * time.Second
	}
	if opts.Timeout > 300*time.Second {
		opts.Timeout = 300 * time.Second
	}
	if opts.Now == nil {
		opts.Now = time.Now
	}
	if opts.RunShell == nil {
		opts.RunShell = platformRunShell
	}
	if opts.SyncTime == nil {
		opts.SyncTime = defaultSyncTime
	}

	return &Executor{
		opts: opts,
		seen: make(map[int64]time.Time),
		sem:  make(chan struct{}, maxConcurrency),
	}
}

// Handle 处理命令并把所有回复通过 reply 回调发出。调用方应在独立 goroutine 中执行。
func (e *Executor) Handle(ctx context.Context, cmd protocol.CommandModel, reply func(protocol.CommandReplyModel)) {
	if reply == nil {
		reply = func(protocol.CommandReplyModel) {}
	}
	if strings.TrimSpace(cmd.Command) == "" {
		cmd.Command = ""
	}

	if cmd.Id != 0 && e.isDuplicate(cmd.Id) {
		e.log().Debug("duplicate command ignored", "id", cmd.Id, "command", cmd.Command)
		return
	}

	now := e.opts.Now()
	if cmd.Expire.IsSet() && cmd.Expire.Before(now) {
		e.audit(cmd, protocol.StatusCanceled, "expired")
		reply(protocol.CommandReplyModel{Id: cmd.Id, Status: protocol.StatusCanceled, Data: "命令已过期"})
		return
	}

	if cmd.StartTime.IsSet() && cmd.StartTime.After(now) {
		e.audit(cmd, protocol.StatusProcessing, "delayed")
		reply(protocol.CommandReplyModel{Id: cmd.Id, Status: protocol.StatusProcessing, Data: "已安排执行"})
		go e.runDelayed(ctx, cmd, reply)
		return
	}

	reply(e.execute(ctx, cmd))
}

func (e *Executor) runDelayed(ctx context.Context, cmd protocol.CommandModel, reply func(protocol.CommandReplyModel)) {
	target := cmd.StartTime.Time
	for {
		remaining := target.Sub(e.opts.Now())
		if remaining <= 0 {
			break
		}
		wait := remaining
		if wait > time.Hour {
			wait = time.Hour
		}
		timer := time.NewTimer(wait)
		select {
		case <-ctx.Done():
			timer.Stop()
			e.audit(cmd, protocol.StatusCanceled, "canceled while waiting")
			reply(protocol.CommandReplyModel{Id: cmd.Id, Status: protocol.StatusCanceled, Data: "Agent 已停止，延迟命令取消"})
			return
		case <-timer.C:
		}
	}

	if cmd.Expire.IsSet() && cmd.Expire.Before(e.opts.Now()) {
		e.audit(cmd, protocol.StatusCanceled, "expired before start")
		reply(protocol.CommandReplyModel{Id: cmd.Id, Status: protocol.StatusCanceled, Data: "命令已过期"})
		return
	}

	reply(e.execute(ctx, cmd))
}

func (e *Executor) execute(ctx context.Context, cmd protocol.CommandModel) protocol.CommandReplyModel {
	started := e.opts.Now()
	result := e.dispatch(ctx, cmd)
	result.Id = cmd.Id
	result.Data = TruncateOutput(result.Data)
	e.auditDuration(cmd, result.Status, e.opts.Now().Sub(started))

	return result
}

func (e *Executor) dispatch(ctx context.Context, cmd protocol.CommandModel) protocol.CommandReplyModel {
	name := cmd.Command
	if !e.isAllowed(name) {
		return reply(protocol.StatusCanceled, "命令不在白名单: "+name)
	}

	select {
	case e.sem <- struct{}{}:
		defer func() { <-e.sem }()
	case <-ctx.Done():
		return reply(protocol.StatusCanceled, "Agent 已停止")
	}

	switch strings.ToLower(name) {
	case "bash":
		return e.runShell(ctx, cmd, "bash")
	case "cmd":
		return e.runShell(ctx, cmd, "cmd")
	case "node/synctime":
		return e.syncTime(ctx, cmd)
	case "node/setchannel":
		return e.setChannel(cmd)
	case "node/restart":
		return e.restart(cmd)
	case "node/reboot":
		return unsupported(name)
	case "framework/install":
		return unsupported(name)
	default:
		if strings.HasPrefix(strings.ToLower(name), "deploy/") {
			return unsupported(name)
		}
		return unsupported(name)
	}
}

func (e *Executor) runShell(ctx context.Context, cmd protocol.CommandModel, shell string) protocol.CommandReplyModel {
	if !e.opts.AllowExec {
		return reply(protocol.StatusCanceled, "命令执行未启用")
	}
	if !shellSupported(shell) {
		return unsupported(cmd.Command)
	}
	if len(cmd.Argument) > maxArgumentSize {
		return reply(protocol.StatusError, fmt.Sprintf("命令参数过长（%d 字节，上限 %d）", len(cmd.Argument), maxArgumentSize))
	}

	script, timeout, err := e.parseShellArgument(cmd.Argument)
	if err != nil {
		return reply(protocol.StatusError, err.Error())
	}

	// shell 命令串行执行，避免同时耗尽主机资源。
	e.shellMu.Lock()
	defer e.shellMu.Unlock()

	runCtx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	output, exitCode, _, err := e.opts.RunShell(runCtx, shell, script, timeout)
	if err != nil {
		if errors.Is(err, context.DeadlineExceeded) || errors.Is(runCtx.Err(), context.DeadlineExceeded) {
			return reply(protocol.StatusError, fmt.Sprintf("命令执行超时（%s）\n%s", timeout, output))
		}
		if errors.Is(err, context.Canceled) {
			return reply(protocol.StatusCanceled, "命令执行已取消\n"+output)
		}
		return reply(protocol.StatusError, "执行失败: "+err.Error()+"\n"+output)
	}
	if exitCode != 0 {
		return reply(protocol.StatusError, fmt.Sprintf("%s\n(exit code: %d)", output, exitCode))
	}
	if strings.TrimSpace(output) == "" {
		return reply(protocol.StatusCompleted, "命令执行成功")
	}

	return reply(protocol.StatusCompleted, output)
}

func (e *Executor) parseShellArgument(argument string) (string, time.Duration, error) {
	script := strings.TrimSpace(argument)
	timeout := e.opts.Timeout

	if strings.HasPrefix(script, "{") {
		var payload struct {
			Cmd     string `json:"cmd"`
			Timeout int64  `json:"timeout"`
		}
		if err := json.Unmarshal([]byte(script), &payload); err == nil {
			if strings.TrimSpace(payload.Cmd) != "" {
				script = payload.Cmd
			}
			if payload.Timeout > 0 {
				timeout = time.Duration(payload.Timeout) * time.Millisecond
			}
		}
	}

	if script == "" {
		return "", 0, errors.New("命令为空")
	}
	if len(script) > maxArgumentSize {
		return "", 0, fmt.Errorf("命令过长（%d 字节，上限 %d）", len(script), maxArgumentSize)
	}
	if timeout <= 0 {
		timeout = 30 * time.Second
	}
	if timeout > 300*time.Second {
		timeout = 300 * time.Second
	}

	return script, timeout, nil
}

func (e *Executor) syncTime(_ context.Context, cmd protocol.CommandModel) protocol.CommandReplyModel {
	target := time.Time{}
	if text := strings.TrimSpace(cmd.Argument); text != "" {
		parsed, err := protocol.ParseTime(text)
		if err != nil {
			return reply(protocol.StatusError, "时间格式无效: "+text)
		}
		target = parsed
	}
	if target.IsZero() {
		if e.opts.ServerTime != nil {
			target = e.opts.ServerTime()
		} else {
			target = e.opts.Now()
		}
	}

	diff := target.Sub(e.opts.Now())
	if diff > -time.Second && diff < time.Second {
		return reply(protocol.StatusCompleted, "无需同步")
	}
	if e.opts.SyncTime == nil {
		return reply(protocol.StatusError, "当前平台不支持时间同步")
	}
	if err := e.opts.SyncTime(target); err != nil {
		return reply(protocol.StatusError, "同步时间失败: "+err.Error())
	}

	return reply(protocol.StatusCompleted, fmt.Sprintf("同步时间为 %s，偏差 %s", target.UTC().Format(protocol.TimeLayout), diff.Round(time.Millisecond)))
}

func (e *Executor) setChannel(cmd protocol.CommandModel) protocol.CommandReplyModel {
	channel := strings.TrimSpace(cmd.Argument)
	if channel == "" {
		return reply(protocol.StatusError, "参数为空")
	}
	if !validChannel(channel) {
		return reply(protocol.StatusError, "通道名称无效（仅允许字母、数字、点、下划线、短横线，最长 64）")
	}
	if e.opts.SetChannel == nil {
		return reply(protocol.StatusError, "不支持修改更新通道")
	}
	if err := e.opts.SetChannel(channel); err != nil {
		return reply(protocol.StatusError, "保存通道失败: "+err.Error())
	}

	return reply(protocol.StatusCompleted, "success "+channel)
}

func (e *Executor) restart(cmd protocol.CommandModel) protocol.CommandReplyModel {
	if e.opts.Restart == nil {
		return unsupported(cmd.Command)
	}

	go func() {
		time.Sleep(time.Second)
		if err := e.opts.Restart(); err != nil {
			e.log().Error("restart process failed", "error", err)
		}
	}()

	return reply(protocol.StatusCompleted, "正在重启")
}

func (e *Executor) isAllowed(name string) bool {
	if len(e.opts.AllowedCommands) == 0 {
		return true
	}
	name = strings.ToLower(name)
	for _, item := range e.opts.AllowedCommands {
		item = strings.ToLower(strings.TrimSpace(item))
		if item == "" {
			continue
		}
		if item == name {
			return true
		}
		if strings.HasSuffix(item, "*") && strings.HasPrefix(name, strings.TrimSuffix(item, "*")) {
			return true
		}
	}

	return false
}

func (e *Executor) isDuplicate(id int64) bool {
	e.mu.Lock()
	defer e.mu.Unlock()

	now := e.opts.Now()
	for key, at := range e.seen {
		if now.Sub(at) > dedupTTL {
			delete(e.seen, key)
		}
	}
	if _, ok := e.seen[id]; ok {
		return true
	}
	e.seen[id] = now

	return false
}

func validChannel(channel string) bool {
	if len(channel) == 0 || len(channel) > 64 {
		return false
	}
	for _, r := range channel {
		switch {
		case r >= 'a' && r <= 'z':
		case r >= 'A' && r <= 'Z':
		case r >= '0' && r <= '9':
		case r == '.' || r == '_' || r == '-':
		default:
			return false
		}
	}

	return true
}

func reply(status int, data string) protocol.CommandReplyModel {
	return protocol.CommandReplyModel{Status: status, Data: data}
}

func unsupported(name string) protocol.CommandReplyModel {
	return reply(protocol.StatusError, "不支持命令: "+name)
}

func (e *Executor) log() *slog.Logger {
	if e.opts.Logger != nil {
		return e.opts.Logger
	}

	return slog.Default()
}

func (e *Executor) audit(cmd protocol.CommandModel, status int, note string) {
	e.log().Info("command audit", "id", cmd.Id, "command", cmd.Command, "status", status, "note", note, "argument_bytes", len(cmd.Argument))
}

func (e *Executor) auditDuration(cmd protocol.CommandModel, status int, elapsed time.Duration) {
	e.log().Info("command finished", "id", cmd.Id, "command", cmd.Command, "status", status, "duration", elapsed.Round(time.Millisecond).String(), "argument_bytes", len(cmd.Argument))
}
