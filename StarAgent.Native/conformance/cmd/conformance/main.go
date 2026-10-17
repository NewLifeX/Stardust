// Command conformance runs language-agnostic StarAgent protocol scenarios
// against an Agent binary using a fake StarServer.
package main

import (
	"context"
	"flag"
	"fmt"
	"os"
	"os/signal"
	"strings"
	"syscall"
	"time"

	"staragent-conformance/internal/mockagent"
	"staragent-conformance/internal/scenario"
)

func main() {
	if len(os.Args) > 1 && strings.EqualFold(os.Args[1], "mock-agent") {
		os.Exit(mockagent.Main(os.Args[2:]))
	}
	os.Exit(run(os.Args[1:]))
}

func run(args []string) int {
	fs := flag.NewFlagSet("conformance", flag.ContinueOnError)
	var opts scenario.Options
	fs.StringVar(&opts.AgentPath, "agent", "", "path to the Agent executable under test")
	fs.StringVar(&opts.AgentArgs, "agent-args", "", `arguments passed to the Agent (default "run --config <file>"; placeholders {config} {server} {data} {workdir})`)
	fs.StringVar(&opts.ConfigTemplate, "config", "", "optional config template copied for each scenario")
	fs.BoolVar(&opts.MockAgent, "mock-agent", false, "use the built-in mock agent instead of an external binary")
	fs.StringVar(&opts.Scenario, "scenario", "all", "scenario name, comma-separated list, or all")
	fs.DurationVar(&opts.Timeout, "timeout", 5*time.Minute, "overall run timeout")
	fs.DurationVar(&opts.ScenarioTimeout, "scenario-timeout", 90*time.Second, "timeout for each scenario")
	fs.BoolVar(&opts.Keep, "keep", false, "keep per-scenario work directories after success")
	fs.BoolVar(&opts.Verbose, "verbose", false, "stream agent output while running")
	fs.StringVar(&opts.WorkDir, "workdir", "", "base directory for scenario artifacts (default: temporary directory)")
	fs.IntVar(&opts.MetricsInterval, "metrics-interval", 5, "MetricsInterval written to generated configs (template value wins; valid range 5-3600)")
	list := fs.Bool("list", false, "list scenarios and exit")

	if err := fs.Parse(args); err != nil {
		if err == flag.ErrHelp {
			return 0
		}

		return 2
	}
	if *list {
		fmt.Println("scenarios:")
		for _, def := range scenario.Definitions() {
			fmt.Printf("  %-14s %s\n", def.Name, def.Description)
		}

		return 0
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	return scenario.NewRunner(opts).Run(ctx)
}
