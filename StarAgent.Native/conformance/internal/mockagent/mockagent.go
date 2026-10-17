// Package mockagent implements the smallest possible Agent used to validate
// the conformance fixture itself. It is not a reference implementation.
package mockagent

import (
	"context"
	"flag"
	"fmt"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	"staragent-conformance/internal/protocol"
)

// Run starts the mock agent and blocks until ctx is cancelled.
func Run(ctx context.Context, configPath string, logf Logger) error {
	cfg, err := loadConfig(configPath)
	if err != nil {
		return err
	}
	sess := newSession(cfg, logf)
	if sess.server == "" {
		return fmt.Errorf("config Server is empty")
	}
	if err := sess.loginWithRetry(ctx); err != nil {
		return err
	}
	if ctx.Err() != nil {
		return nil
	}

	queue := newEventQueue()
	queue.push(protocol.EventModel{
		Time:   protocol.NowMillis(),
		Type:   "info",
		Name:   "Startup",
		Remark: "mock agent started",
	})
	handler := newCommandHandler(sess, sess.allowExec, logf)

	runCtx, cancel := context.WithCancel(ctx)
	defer cancel()

	var wg sync.WaitGroup
	wg.Add(3)
	go func() {
		defer wg.Done()
		sess.pingLoop(runCtx, handler)
	}()
	go func() {
		defer wg.Done()
		sess.wsLoop(runCtx, handler)
	}()
	go func() {
		defer wg.Done()
		sess.eventLoop(runCtx, queue)
	}()
	wg.Wait()

	return nil
}

// Main implements the "mock-agent" subcommand. It is useful when the fixture
// itself should exercise the external-process code path.
func Main(args []string) int {
	fs := flag.NewFlagSet("mock-agent", flag.ContinueOnError)
	fs.SetOutput(os.Stderr)
	configPath := fs.String("config", "", "path to staragent.json")
	server := fs.String("server", "", "override Server URL")
	dataDir := fs.String("data-dir", "", "override DataDir")
	if err := fs.Parse(args); err != nil {
		return 2
	}
	if *configPath == "" {
		*configPath = envFirst("STARAGENT_CONFIG", "STARAGENT_CONFIG_FILE")
	}
	if *configPath == "" {
		fmt.Fprintln(os.Stderr, "mock-agent: --config is required")
		return 2
	}
	if *server != "" {
		_ = os.Setenv("STARAGENT_SERVER", *server)
	}
	if *dataDir != "" {
		_ = os.Setenv("STARAGENT_DATA_DIR", *dataDir)
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	logf := func(format string, args ...any) {
		msg := fmt.Sprintf(format, args...)
		fmt.Printf("%s mock-agent: %s\n", time.Now().Format("15:04:05.000"), msg)
	}
	if err := Run(ctx, *configPath, logf); err != nil {
		fmt.Fprintf(os.Stderr, "mock-agent: %v\n", err)
		return 1
	}

	return 0
}
