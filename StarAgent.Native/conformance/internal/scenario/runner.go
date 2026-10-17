package scenario

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"staragent-conformance/internal/fakeserver"
	"staragent-conformance/internal/mockagent"
	"staragent-conformance/internal/protocol"
)

// Options configures a conformance run.
type Options struct {
	AgentPath       string
	AgentArgs       string
	ConfigTemplate  string
	MockAgent       bool
	Scenario        string
	Timeout         time.Duration
	ScenarioTimeout time.Duration
	Keep            bool
	Verbose         bool
	WorkDir         string
	Out             io.Writer
	// MetricsInterval is written to generated configs when the template does
	// not set MetricsInterval. The heartbeat period is controlled separately by
	// the fake server's ping response (Period).
	MetricsInterval int
}

// Runner executes one or more scenarios against an agent.
type Runner struct {
	opts Options
}

// NewRunner applies defaults and returns a runner.
func NewRunner(opts Options) *Runner {
	if opts.Out == nil {
		opts.Out = os.Stdout
	}
	if opts.Timeout <= 0 {
		opts.Timeout = 5 * time.Minute
	}
	if opts.ScenarioTimeout <= 0 {
		opts.ScenarioTimeout = 90 * time.Second
	}
	if strings.TrimSpace(opts.Scenario) == "" {
		opts.Scenario = "all"
	}
	if opts.MetricsInterval <= 0 {
		opts.MetricsInterval = 5
	}

	return &Runner{opts: opts}
}

// Run executes the selected scenarios and returns a process exit code.
func (r *Runner) Run(ctx context.Context) int {
	defs, err := Select(r.opts.Scenario)
	if err != nil {
		fmt.Fprintf(r.opts.Out, "conformance: %v\n", err)

		return 2
	}
	if !r.opts.MockAgent && r.opts.AgentPath == "" {
		fmt.Fprintln(r.opts.Out, "conformance: --agent <path> is required unless --mock-agent is used")

		return 2
	}
	if !r.opts.MockAgent {
		agentPath, err := filepath.Abs(r.opts.AgentPath)
		if err != nil {
			fmt.Fprintf(r.opts.Out, "conformance: resolve agent path: %v\n", err)

			return 2
		}
		r.opts.AgentPath = agentPath
		if _, err := os.Stat(r.opts.AgentPath); err != nil {
			fmt.Fprintf(r.opts.Out, "conformance: agent binary: %v\n", err)

			return 2
		}
	}

	base := r.opts.WorkDir
	autoBase := false
	if base == "" {
		dir, err := os.MkdirTemp("", "staragent-conformance-")
		if err != nil {
			fmt.Fprintf(r.opts.Out, "conformance: create workdir: %v\n", err)

			return 2
		}
		base = dir
		autoBase = true
	} else if err := os.MkdirAll(base, 0o755); err != nil {
		fmt.Fprintf(r.opts.Out, "conformance: create workdir: %v\n", err)

		return 2
	}
	if abs, err := filepath.Abs(base); err == nil {
		base = abs
	}

	overallCtx, cancel := context.WithTimeout(ctx, r.opts.Timeout)
	defer cancel()

	agentName := r.opts.AgentPath
	if r.opts.MockAgent {
		agentName = "built-in mock agent"
	}
	fmt.Fprintf(r.opts.Out, "conformance: agent=%s scenarios=%d timeout=%s\n", agentName, len(defs), r.opts.Timeout)
	fmt.Fprintf(r.opts.Out, "conformance: workdir=%s\n\n", base)

	started := time.Now()
	passed, failed := 0, 0
	for i, def := range defs {
		if overallCtx.Err() != nil {
			for _, skipped := range defs[i:] {
				fmt.Fprintf(r.opts.Out, "[FAIL] %s: overall timeout reached\n", skipped.Name)
				failed++
			}

			break
		}

		fmt.Fprintf(r.opts.Out, "--- %s: %s\n", def.Name, def.Description)
		scenarioCtx := overallCtx
		cancelScenario := func() {}
		if r.opts.ScenarioTimeout > 0 {
			scenarioCtx, cancelScenario = context.WithTimeout(overallCtx, r.opts.ScenarioTimeout)
		}
		begin := time.Now()
		failure := r.runOne(scenarioCtx, def, base)
		elapsed := time.Since(begin).Round(100 * time.Millisecond)
		cancelScenario()
		if failure != nil {
			failed++
			fmt.Fprintf(r.opts.Out, "[FAIL] %s (%s)\n", def.Name, elapsed)
			r.printFailure(failure)
			fmt.Fprintln(r.opts.Out)
		} else {
			passed++
			fmt.Fprintf(r.opts.Out, "[PASS] %s (%s)\n\n", def.Name, elapsed)
		}
	}

	fmt.Fprintf(r.opts.Out, "%d passed, %d failed in %s\n", passed, failed, time.Since(started).Round(100*time.Millisecond))
	if autoBase && failed == 0 {
		_ = os.RemoveAll(base)
	}
	if failed > 0 {
		return 1
	}

	return 0
}

type runFailure struct {
	Err     error
	State   *fakeserver.State
	Logs    string
	WorkDir string
	LogPath string
}

func (r *Runner) runOne(ctx context.Context, def Definition, base string) *runFailure {
	workDir := filepath.Join(base, def.Name)
	if err := os.RemoveAll(workDir); err != nil {
		return &runFailure{Err: fmt.Errorf("clean workdir: %w", err), WorkDir: workDir}
	}
	if err := os.MkdirAll(workDir, 0o755); err != nil {
		return &runFailure{Err: fmt.Errorf("create workdir: %w", err), WorkDir: workDir}
	}

	server := fakeserver.New(fakeserver.Options{
		Period:           1,
		AutoPingInterval: 500 * time.Millisecond,
	})
	if err := server.Start(); err != nil {
		return &runFailure{Err: fmt.Errorf("start fake server: %w", err), WorkDir: workDir}
	}
	defer server.Close()

	configPath, dataDir, err := prepareConfig(server.URL(), workDir, r.opts.ConfigTemplate, r.opts.MetricsInterval)
	if err != nil {
		return &runFailure{Err: err, State: server.State(), WorkDir: workDir}
	}

	logPath := filepath.Join(workDir, "agent.log")
	logFile, err := os.Create(logPath)
	if err != nil {
		return &runFailure{Err: fmt.Errorf("create agent log: %w", err), WorkDir: workDir}
	}
	defer logFile.Close()

	var tee io.Writer = logFile
	if r.opts.Verbose {
		tee = io.MultiWriter(logFile, r.opts.Out)
	}
	sink := newLogSink(tee)
	env := &Env{
		Ctx:        ctx,
		Options:    r.opts,
		Server:     server,
		Control:    NewControl(server.URL()),
		WorkDir:    workDir,
		ConfigPath: configPath,
		DataDir:    dataDir,
		logs:       sink,
		out:        r.opts.Out,
	}
	defer env.StopAgent()

	if err := def.Run(ctx, env); err != nil {
		env.StopAgent()
		_ = logFile.Close()

		return &runFailure{
			Err:     err,
			State:   server.State(),
			Logs:    sink.String(),
			WorkDir: workDir,
			LogPath: logPath,
		}
	}
	env.StopAgent()
	_ = logFile.Close()
	if !r.opts.Keep {
		_ = os.RemoveAll(workDir)
	}

	return nil
}

func (r *Runner) printFailure(failure *runFailure) {
	fmt.Fprintf(r.opts.Out, "  reason: %v\n", failure.Err)
	fmt.Fprintf(r.opts.Out, "  state: %s\n", summarizeState(failure.State))
	if failure.State != nil {
		fmt.Fprintf(r.opts.Out, "  requests: %s\n", summarizeRequests(failure.State, 12))
		fmt.Fprintf(r.opts.Out, "  replies: %s\n", summarizeReplies(failure.State, 8))
		fmt.Fprintf(r.opts.Out, "  deliveries: %s\n", summarizeDeliveries(failure.State))
		fmt.Fprintf(r.opts.Out, "  event attempts: %s\n", summarizeEventAttempts(failure.State))
	}
	if failure.Logs != "" {
		fmt.Fprintf(r.opts.Out, "  agent log (%s):\n%s\n", failure.LogPath, indentTail(failure.Logs, 40))
	}
	fmt.Fprintf(r.opts.Out, "  artifacts: %s\n", failure.WorkDir)
}

type logSink struct {
	mu  sync.Mutex
	buf []byte
	tee io.Writer
}

func newLogSink(tee io.Writer) *logSink {
	return &logSink{tee: tee}
}

func (l *logSink) Write(p []byte) (int, error) {
	l.mu.Lock()
	defer l.mu.Unlock()
	l.buf = append(l.buf, p...)
	const max = 128 * 1024
	if len(l.buf) > max {
		l.buf = append([]byte(nil), l.buf[len(l.buf)-max:]...)
	}
	if l.tee != nil {
		_, _ = l.tee.Write(p)
	}

	return len(p), nil
}

func (l *logSink) String() string {
	l.mu.Lock()
	defer l.mu.Unlock()

	return string(l.buf)
}

type agentProcess struct {
	mock     bool
	cmd      *exec.Cmd
	cancel   context.CancelFunc
	exited   chan struct{}
	stopOnce sync.Once

	mu      sync.Mutex
	exitErr error
}

func (a *agentProcess) waitExit() error {
	a.mu.Lock()
	defer a.mu.Unlock()

	return a.exitErr
}

func (a *agentProcess) Exited() (bool, error) {
	select {
	case <-a.exited:
		return true, a.waitExit()
	default:
		return false, nil
	}
}

func (a *agentProcess) Stop() {
	a.stopOnce.Do(func() {
		if a.cancel != nil {
			a.cancel()
		}
		if a.cmd != nil && a.cmd.Process != nil {
			_ = a.cmd.Process.Kill()
		}
		select {
		case <-a.exited:
		case <-time.After(5 * time.Second):
		}
	})
}

// Env is the per-scenario environment handed to a Definition.
type Env struct {
	Ctx        context.Context
	Options    Options
	Server     *fakeserver.Server
	Control    *Control
	WorkDir    string
	ConfigPath string
	DataDir    string

	logs *logSink
	out  io.Writer

	agentMu sync.Mutex
	agent   *agentProcess
}

// Logf writes a timestamped line to the agent log.
func (e *Env) Logf(format string, args ...any) {
	msg := fmt.Sprintf(format, args...)
	_, _ = fmt.Fprintf(e.logs, "%s %s\n", time.Now().Format("15:04:05.000"), msg)
}

// StartAgent launches either the external agent or the built-in mock agent.
func (e *Env) StartAgent() error {
	e.agentMu.Lock()
	defer e.agentMu.Unlock()
	if e.agent != nil {
		return fmt.Errorf("agent already running")
	}

	if e.Options.MockAgent {
		ctx, cancel := context.WithCancel(e.Ctx)
		agent := &agentProcess{mock: true, cancel: cancel, exited: make(chan struct{})}
		e.agent = agent
		go func() {
			err := mockagent.Run(ctx, e.ConfigPath, func(format string, args ...any) {
				e.Logf("mock-agent: "+format, args...)
			})
			agent.mu.Lock()
			agent.exitErr = err
			agent.mu.Unlock()
			close(agent.exited)
		}()
		e.watchAgent(agent)

		return nil
	}

	args := e.agentArgs()
	cmd := exec.Command(e.Options.AgentPath, args...)
	cmd.Dir = e.WorkDir
	cmd.Env = append(os.Environ(), e.agentEnv()...)
	cmd.Stdout = e.logs
	cmd.Stderr = e.logs
	if err := cmd.Start(); err != nil {
		return err
	}
	agent := &agentProcess{cmd: cmd, exited: make(chan struct{})}
	e.agent = agent
	go func() {
		err := cmd.Wait()
		agent.mu.Lock()
		agent.exitErr = err
		agent.mu.Unlock()
		close(agent.exited)
	}()
	e.watchAgent(agent)
	e.Logf("started %s %s", e.Options.AgentPath, strings.Join(args, " "))

	return nil
}

func (e *Env) watchAgent(agent *agentProcess) {
	go func() {
		select {
		case <-e.Ctx.Done():
			agent.Stop()
		case <-agent.exited:
		}
	}()
}

// StopAgent stops the current agent process, if any.
func (e *Env) StopAgent() {
	e.agentMu.Lock()
	agent := e.agent
	e.agent = nil
	e.agentMu.Unlock()
	if agent != nil {
		agent.Stop()
	}
}

// RestartAgent stops and relaunches the agent with the same config and data dir.
func (e *Env) RestartAgent() error {
	e.StopAgent()
	if !sleepContext(e.Ctx, 250*time.Millisecond) {
		return e.Ctx.Err()
	}

	return e.StartAgent()
}

func (e *Env) agentExited() (bool, error) {
	e.agentMu.Lock()
	agent := e.agent
	e.agentMu.Unlock()
	if agent == nil {
		return false, nil
	}

	return agent.Exited()
}

func (e *Env) agentArgs() []string {
	raw := strings.TrimSpace(e.Options.AgentArgs)
	if raw == "" {
		return []string{"run", "--config", e.ConfigPath}
	}
	raw = strings.NewReplacer(
		"{config}", e.ConfigPath,
		"{server}", e.Server.URL(),
		"{data}", e.DataDir,
		"{workdir}", e.WorkDir,
	).Replace(raw)

	return strings.Fields(raw)
}

func (e *Env) agentEnv() []string {
	server := e.Server.URL()
	return []string{
		"STARAGENT_SERVER=" + server,
		"STARAGENT_SERVER_URL=" + server,
		"STARAGENT_DATA_DIR=" + e.DataDir,
		"STARAGENT_DATADIR=" + e.DataDir,
		"STARAGENT_CONFIG=" + e.ConfigPath,
		"STARAGENT_CONFIG_FILE=" + e.ConfigPath,
		"STARAGENT_PRODUCTCODE=StarAgent-Conformance",
		"STARAGENT_PRODUCT_CODE=StarAgent-Conformance",
		"STARAGENT_WORKDIR=" + e.WorkDir,
	}
}

// WaitFor polls the fake server until predicate returns true.
func (e *Env) WaitFor(desc string, predicate func(*fakeserver.State) bool) (*fakeserver.State, error) {
	ticker := time.NewTicker(50 * time.Millisecond)
	defer ticker.Stop()

	var last *fakeserver.State
	var lastErr error
	for {
		st, err := e.Control.State(e.Ctx)
		if err != nil {
			lastErr = err
		} else {
			last = st
			if predicate(st) {
				return st, nil
			}
		}

		if e.Ctx.Err() == nil {
			if exited, exitErr := e.agentExited(); exited {
				return last, fmt.Errorf("%s: agent exited unexpectedly: %v", desc, exitErr)
			}
		}

		select {
		case <-e.Ctx.Done():
			msg := "timeout"
			if e.Ctx.Err() == context.DeadlineExceeded {
				msg = "timeout (scenario deadline)"
			}
			if lastErr != nil {
				msg += fmt.Sprintf("; control error: %v", lastErr)
			}

			return last, fmt.Errorf("%s: %s (%s)", desc, msg, summarizeState(last))
		case <-ticker.C:
		}
	}
}

// WaitForReply waits until at least min replies were recorded for a command.
func (e *Env) WaitForReply(id int64, min int) (*fakeserver.State, []fakeserver.ReplyRecord, error) {
	st, err := e.WaitFor(fmt.Sprintf("reply for command %d", id), func(s *fakeserver.State) bool {
		return countReplies(s, id) >= min
	})
	if err != nil {
		return st, nil, err
	}

	return st, repliesFor(st, id), nil
}

// Command enqueues a command through the control API.
func (e *Env) Command(cmd protocol.CommandModel, via string) error {
	return e.Control.Command(e.Ctx, cmd, via)
}

// Sleep waits for d or until the scenario context ends.
func (e *Env) Sleep(d time.Duration) bool { return sleepContext(e.Ctx, d) }

func sleepContext(ctx context.Context, d time.Duration) bool {
	timer := time.NewTimer(d)
	defer timer.Stop()
	select {
	case <-ctx.Done():
		return false
	case <-timer.C:
		return true
	}
}

func prepareConfig(serverURL, workDir, templatePath string, metricsInterval int) (string, string, error) {
	if metricsInterval <= 0 {
		metricsInterval = 5
	}
	dataDir := filepath.Join(workDir, "data")
	if err := os.MkdirAll(dataDir, 0o755); err != nil {
		return "", "", fmt.Errorf("create data dir: %w", err)
	}
	configPath := filepath.Join(workDir, "staragent.json")

	wsURL := strings.Replace(strings.Replace(serverURL, "https://", "wss://", 1), "http://", "ws://", 1) + "/node/notify"
	replacer := strings.NewReplacer(
		"{{SERVER}}", serverURL,
		"{{SERVER_URL}}", serverURL,
		"{{WS_URL}}", wsURL,
		"{{DATA_DIR}}", dataDir,
		"{{CONFIG_DIR}}", workDir,
		"{{WORK_DIR}}", workDir,
	)

	var text string
	if templatePath != "" {
		data, err := os.ReadFile(templatePath)
		if err != nil {
			return "", "", fmt.Errorf("read config template: %w", err)
		}
		var parsed map[string]any
		if err := json.Unmarshal(data, &parsed); err == nil {
			replacePlaceholders(parsed, replacer)
			parsed["Server"] = serverURL
			parsed["DataDir"] = dataDir
			if v, _ := parsed["ProductCode"].(string); v == "" {
				parsed["ProductCode"] = "StarAgent-Conformance"
			}
			if _, ok := parsed["MetricsInterval"]; !ok {
				parsed["MetricsInterval"] = metricsInterval
			}
			out, err := json.MarshalIndent(parsed, "", "  ")
			if err != nil {
				return "", "", err
			}
			text = string(append(out, '\n'))
		} else {
			text = replacer.Replace(string(data))
			if strings.Contains(text, "{{") {
				return "", "", fmt.Errorf("config template %s has unresolved placeholders or invalid JSON: %w", templatePath, err)
			}
			// Non-JSON templates are passed through; Server/DataDir must come
			// from placeholders or the STARAGENT_* environment variables.
			if err := os.WriteFile(configPath, []byte(text), 0o600); err != nil {
				return "", "", err
			}

			return configPath, dataDir, nil
		}
	} else {
		cfg := map[string]any{
			"Server":          serverURL,
			"ProductCode":     "StarAgent-Conformance",
			"Code":            "",
			"Secret":          "",
			"Project":         "",
			"Channel":         "Release",
			"DataDir":         dataDir,
			"LogLevel":        "info",
			"MetricsInterval": metricsInterval,
			"AllowExec":       false,
			"AllowedCommands": []string{},
			"CommandTimeout":  30,
			"TlsVerify":       true,
		}
		out, _ := json.MarshalIndent(cfg, "", "  ")
		text = string(append(out, '\n'))
	}

	if err := os.WriteFile(configPath, []byte(text), 0o600); err != nil {
		return "", "", fmt.Errorf("write config: %w", err)
	}

	return configPath, dataDir, nil
}

func replacePlaceholders(value any, replacer *strings.Replacer) {
	switch v := value.(type) {
	case map[string]any:
		for key, item := range v {
			if text, ok := item.(string); ok {
				v[key] = replacer.Replace(text)
				continue
			}
			replacePlaceholders(item, replacer)
		}
	case []any:
		for i, item := range v {
			if text, ok := item.(string); ok {
				v[i] = replacer.Replace(text)
				continue
			}
			replacePlaceholders(item, replacer)
		}
	}
}
