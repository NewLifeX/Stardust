package agent

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/client"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/command"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/identity"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/metrics"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/service"
)

const (
	defaultVersion = "1.0.0"
	maxEventBatch  = 100
	upgradePeriod  = 6 * time.Hour
)

var (
	errMigrated      = errors.New("server migrated")
	errLoginRejected = errors.New("login rejected")
)

// Options 是 Agent 启动参数。
type Options struct {
	ConfigPath string
	Config     *config.Config
	Store      *config.Store
	Logger     *slog.Logger
	Version    string
	BuildTime  time.Time
}

// Agent 维护登录、心跳、通知与命令执行主循环。
type Agent struct {
	opts  Options
	store *config.Store
	log   *slog.Logger

	client  *client.Client
	metrics *metrics.Collector
	events  *EventQueue
	exec    *command.Executor

	hardware identity.Info
	system   identity.SystemInfo

	networkMu sync.Mutex
	ip        string
	gateway   string
	dns       string
	networkAt time.Time

	loginMu sync.Mutex

	upgradeMu   sync.Mutex
	lastUpgrade time.Time

	serverOffset atomic.Int64
	lastDelay    atomic.Int64
	period       atomic.Int64
}

// New 创建 Agent。
func New(ctx context.Context, opts Options) (*Agent, error) {
	if opts.Logger == nil {
		opts.Logger = slog.Default()
	}
	if opts.Config == nil {
		path := opts.ConfigPath
		if path == "" {
			path = config.DefaultPath()
		}
		cfg, err := config.Load(path)
		if err != nil {
			return nil, err
		}
		opts.Config = cfg
		opts.ConfigPath = path
	}
	if opts.ConfigPath == "" {
		opts.ConfigPath = config.DefaultPath()
	}
	if opts.BuildTime.IsZero() {
		opts.BuildTime = time.Now()
	}

	cfg := *opts.Config
	cfg.AllowedCommands = append([]string(nil), opts.Config.AllowedCommands...)
	cfg.Normalize()
	if err := cfg.Validate(); err != nil {
		return nil, err
	}

	store := opts.Store
	if store == nil {
		store = config.NewStore(opts.ConfigPath, cfg)
	}

	if err := os.MkdirAll(cfg.DataDir, 0o700); err != nil {
		return nil, fmt.Errorf("create data directory %s: %w", cfg.DataDir, err)
	}

	cl, err := client.New(cfg.Server, cfg.TlsVerify, opts.Logger)
	if err != nil {
		return nil, err
	}

	events, err := NewEventQueue(filepath.Join(cfg.DataDir, "events.json"), 1000)
	if err != nil {
		opts.Logger.Warn("event queue load failed, starting empty", "error", err)
	}

	agent := &Agent{
		opts:    opts,
		store:   store,
		log:     opts.Logger,
		client:  cl,
		metrics: metrics.New(cfg.DataDir),
		events:  events,
	}
	agent.period.Store(int64(cfg.MetricsInterval))

	collectCtx, cancel := context.WithTimeout(ctx, 20*time.Second)
	defer cancel()

	agent.hardware = identity.Gather(collectCtx)
	agent.system = identity.System(collectCtx)
	agent.networkSnapshot(collectCtx, true)
	// 预热 CPU/网络采样，让第一次心跳就能得到有效速率。
	agent.metrics.Collect(collectCtx)

	agent.exec = command.New(command.Options{
		AllowExec:       cfg.AllowExec,
		AllowedCommands: cfg.AllowedCommands,
		Timeout:         time.Duration(cfg.CommandTimeout) * time.Second,
		ServerTime:      agent.serverNow,
		SetChannel: func(channel string) error {
			if _, err := store.Update(func(current *config.Config) { current.Channel = channel }); err != nil {
				return err
			}
			agent.upgradeMu.Lock()
			agent.lastUpgrade = time.Time{}
			agent.upgradeMu.Unlock()
			return nil
		},
		Restart: service.RestartProcess,
		Logger:  opts.Logger,
	})

	cl.OnUnauthorized(func(ctx context.Context) error {
		return agent.login(ctx)
	})

	return agent, nil
}

// Run 启动长连接主循环，直到 context 取消。
func (a *Agent) Run(ctx context.Context) error {
	a.log.Info("staragent starting", "version", a.version(), "server", a.client.BaseURL(), "allow_exec", a.store.Snapshot().AllowExec)
	a.enqueue("info", "Startup", "agent started version="+a.version())

	defer func() {
		a.enqueue("info", "Stop", "agent stopping")
		shutdownCtx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		a.flushEvents(shutdownCtx)
		if a.client.Token() != "" {
			if _, err := a.client.Logout(shutdownCtx, "agent stopping"); err != nil {
				a.log.Warn("logout failed", "error", err)
			}
		}
	}()

	backoff := client.NewBackoff()
	for {
		if ctx.Err() != nil {
			return nil
		}

		err := a.runSession(ctx, backoff)
		if ctx.Err() != nil {
			return nil
		}
		if err == nil {
			return nil
		}
		if errors.Is(err, errMigrated) {
			a.log.Info("server migration completed")
			backoff.Reset()
			continue
		}
		if errors.Is(err, errLoginRejected) {
			return err
		}

		a.log.Warn("connection lost, reconnecting with backoff", "error", err)
		a.client.SetToken("", 0)
		if err := backoff.Wait(ctx); err != nil {
			return nil
		}
	}
}

// Once 执行一次登录、心跳与命令处理，然后注销退出。
func (a *Agent) Once(ctx context.Context) error {
	if err := a.login(ctx); err != nil {
		return err
	}
	defer func() {
		logoutCtx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		a.flushEvents(logoutCtx)
		if _, err := a.client.Logout(logoutCtx, "once mode completed"); err != nil {
			a.log.Warn("logout failed", "error", err)
		}
	}()

	response, err := a.ping(ctx)
	if err != nil {
		return err
	}
	a.log.Info("once ping completed", "period", response.Period, "commands", len(response.Commands))

	cfg := a.store.Snapshot()
	waitTimeout := time.Duration(cfg.CommandTimeout+10) * time.Second
	for _, item := range response.Commands {
		cmd := item
		if cmd.StartTime.IsSet() && cmd.StartTime.After(time.Now()) {
			a.exec.Handle(ctx, cmd, func(reply protocol.CommandReplyModel) { a.sendReply(ctx, reply) })
			continue
		}

		done := make(chan struct{})
		var once sync.Once
		a.exec.Handle(ctx, cmd, func(reply protocol.CommandReplyModel) {
			a.sendReply(ctx, reply)
			if reply.Status != protocol.StatusProcessing {
				once.Do(func() { close(done) })
			}
		})
		select {
		case <-done:
		case <-time.After(waitTimeout):
			a.log.Warn("once command wait timed out", "id", cmd.Id)
		case <-ctx.Done():
			return ctx.Err()
		}
	}

	return nil
}

func (a *Agent) runSession(ctx context.Context, backoff *client.Backoff) error {
	if a.client.Token() == "" {
		if err := a.login(ctx); err != nil {
			if status, ok := client.StatusCode(err); ok && status >= 400 && status < 500 && status != http.StatusRequestTimeout && status != http.StatusTooManyRequests {
				return fmt.Errorf("%w: %v", errLoginRejected, err)
			}
			return err
		}
	}

	cfg := a.store.Snapshot()
	period := time.Duration(cfg.MetricsInterval) * time.Second
	if period <= 0 {
		period = 60 * time.Second
	}
	a.period.Store(int64(period / time.Second))

	notifyCtx, cancelNotify := context.WithCancel(ctx)
	var notifyWG sync.WaitGroup
	notifyWG.Add(1)
	go func() {
		defer notifyWG.Done()
		a.notifyLoop(notifyCtx)
	}()
	defer notifyWG.Wait()
	defer cancelNotify()

	a.checkUpgrade(ctx)

	resetBackoff := true
	for {
		started := time.Now()
		response, err := a.ping(ctx)
		if err != nil {
			return err
		}
		a.lastDelay.Store(time.Since(started).Milliseconds())
		if resetBackoff {
			backoff.Reset()
			resetBackoff = false
		}

		if response.Period > 0 {
			next := response.Period
			if next < 5 {
				next = 5
			}
			if next > 3600 {
				next = 3600
			}
			if time.Duration(next)*time.Second != period {
				a.log.Info("heartbeat period changed", "seconds", next)
			}
			period = time.Duration(next) * time.Second
			a.period.Store(int64(next))
		}

		for _, item := range response.Commands {
			cmd := item
			go a.exec.Handle(ctx, cmd, func(reply protocol.CommandReplyModel) { a.sendReply(ctx, reply) })
		}

		a.flushEvents(ctx)
		a.checkUpgrade(ctx)

		if response.NewServer != "" && strings.TrimRight(response.NewServer, "/") != strings.TrimRight(a.client.BaseURL(), "/") {
			return a.migrate(ctx, strings.TrimRight(response.NewServer, "/"))
		}

		timer := time.NewTimer(period)
		select {
		case <-ctx.Done():
			timer.Stop()
			return ctx.Err()
		case <-timer.C:
		}
	}
}

func (a *Agent) login(ctx context.Context) error {
	a.loginMu.Lock()
	defer a.loginMu.Unlock()

	cfg := a.store.Snapshot()
	request := protocol.LoginInfo{
		Code:        cfg.Code,
		Secret:      cfg.Secret,
		ProductCode: cfg.ProductCode,
		ClientId:    fmt.Sprintf("%s@%d", a.localIP(), os.Getpid()),
		Project:     cfg.Project,
		Node:        a.buildNodeInfo(ctx, cfg),
	}

	response, err := a.client.Login(ctx, request)
	if err != nil {
		return err
	}
	if response.ServerTime > 0 {
		a.serverOffset.Store(response.ServerTime - time.Now().UnixMilli())
	}
	if response.Code != "" || response.Secret != "" {
		changed := (response.Code != "" && response.Code != cfg.Code) || (response.Secret != "" && response.Secret != cfg.Secret)
		if changed {
			if _, err := a.store.Update(func(current *config.Config) {
				if response.Code != "" {
					current.Code = response.Code
				}
				if response.Secret != "" {
					current.Secret = response.Secret
				}
			}); err != nil {
				a.log.Error("persist node identity failed", "error", err)
			}
		}
	}

	a.log.Info("login succeeded", "name", response.Name, "expire", response.Expire, "server_time", response.ServerTime)

	return nil
}

func (a *Agent) ping(ctx context.Context) (*protocol.PingResponse, error) {
	response, err := a.client.Ping(ctx, a.buildPingInfo(ctx))
	if err != nil {
		return nil, err
	}
	if response.ServerTime > 0 {
		a.serverOffset.Store(response.ServerTime - time.Now().UnixMilli())
	}

	return response, nil
}

func (a *Agent) notifyLoop(ctx context.Context) {
	backoff := client.NewBackoff()
	for {
		if ctx.Err() != nil {
			return
		}

		interval := time.Duration(a.period.Load()) * time.Second
		if interval <= 0 {
			interval = 60 * time.Second
		}
		err := a.client.RunNotify(ctx, client.NotifyHandler{
			Interval: interval,
			Logger:   a.log,
			OnCommand: func(ctx context.Context, cmd protocol.CommandModel) {
				a.exec.Handle(ctx, cmd, func(reply protocol.CommandReplyModel) { a.sendReply(ctx, reply) })
			},
		})
		if ctx.Err() != nil {
			return
		}
		a.log.Warn("notify connection closed, retrying", "error", err)
		if err := backoff.Wait(ctx); err != nil {
			return
		}
	}
}

func (a *Agent) sendReply(ctx context.Context, reply protocol.CommandReplyModel) {
	for attempt := 1; attempt <= 3; attempt++ {
		replyCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
		err := a.client.CommandReply(replyCtx, reply)
		cancel()
		if err == nil {
			a.log.Debug("command reply sent", "id", reply.Id, "status", reply.Status)
			return
		}

		a.log.Warn("command reply failed", "id", reply.Id, "attempt", attempt, "error", err)
		if ctx.Err() != nil {
			return
		}
		wait := time.Duration(attempt) * time.Second
		select {
		case <-ctx.Done():
			return
		case <-time.After(wait):
		}
	}

	a.log.Error("command reply dropped after retries", "id", reply.Id, "status", reply.Status)
}

func (a *Agent) flushEvents(ctx context.Context) {
	for attempt := 1; attempt <= 3; attempt++ {
		batch := a.events.Take(maxEventBatch)
		if len(batch) == 0 {
			return
		}

		count, err := a.client.PostEvents(ctx, batch)
		if err == nil {
			a.log.Debug("events posted", "count", count)
			if count >= 0 && count < len(batch) {
				a.events.PutFront(batch[count:])
			}
			if len(batch) < maxEventBatch {
				return
			}
			continue
		}

		a.events.PutFront(batch)
		a.log.Warn("post events failed", "count", len(batch), "attempt", attempt, "error", err)
		if ctx.Err() != nil {
			return
		}
		select {
		case <-ctx.Done():
			return
		case <-time.After(time.Duration(attempt) * time.Second):
		}
	}
}

func (a *Agent) checkUpgrade(ctx context.Context) {
	a.upgradeMu.Lock()
	if !a.lastUpgrade.IsZero() && time.Since(a.lastUpgrade) < upgradePeriod {
		a.upgradeMu.Unlock()
		return
	}
	a.lastUpgrade = time.Now()
	a.upgradeMu.Unlock()

	cfg := a.store.Snapshot()
	info, err := a.client.Upgrade(ctx, cfg.Channel)
	if err != nil {
		if ctx.Err() == nil {
			a.log.Debug("upgrade check failed", "error", err)
		}
		return
	}
	if info == nil || info.Version == a.version() {
		return
	}

	remark := fmt.Sprintf("available version %s", info.Version)
	if info.Source != "" {
		remark += " source=" + info.Source
	}
	a.log.Info("upgrade available, manual update required", "version", info.Version, "source", info.Source)
	a.enqueue("info", "Upgrade", remark)
}

func (a *Agent) migrate(ctx context.Context, newServer string) error {
	oldServer := a.client.BaseURL()
	cfg := a.store.Snapshot()
	a.log.Info("migrating to new server", "from", oldServer, "to", newServer)

	logoutCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
	_, _ = a.client.Logout(logoutCtx, "server migration")
	cancel()

	if err := a.client.SetBaseURL(newServer); err != nil {
		return fmt.Errorf("switch server: %w", err)
	}
	if _, err := a.store.Update(func(current *config.Config) { current.Server = newServer }); err != nil {
		_ = a.client.SetBaseURL(oldServer)
		return fmt.Errorf("persist new server: %w", err)
	}
	a.client.SetToken("", 0)

	if err := a.login(ctx); err != nil {
		_ = a.client.SetBaseURL(oldServer)
		_, _ = a.store.Update(func(current *config.Config) { current.Server = cfg.Server })
		a.client.SetToken("", 0)
		return fmt.Errorf("login to new server %s: %w", newServer, err)
	}

	return errMigrated
}

func (a *Agent) enqueue(kind, name, remark string) {
	event := protocol.EventModel{
		Time:   time.Now().UnixMilli(),
		Type:   kind,
		Name:   name,
		Remark: remark,
	}
	if err := a.events.Add(event); err != nil {
		a.log.Warn("event queue persist failed", "error", err)
	}
}

// Runtime 返回运行时描述，供 version 命令使用。
func Runtime() string { return runtime.GOOS + "/" + runtime.GOARCH }
