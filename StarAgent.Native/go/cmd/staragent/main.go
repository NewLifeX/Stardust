package main

import (
	"context"
	"errors"
	"flag"
	"fmt"
	"os"
	"os/signal"
	"path/filepath"
	"runtime"
	"runtime/debug"
	"strings"
	"syscall"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/agent"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/logging"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/service"
)

// 这两个变量可在构建时通过 -ldflags 覆盖。
var (
	Version   = "1.0.2026.1017"
	BuildTime = ""
)

func main() {
	if len(os.Args) < 2 {
		printUsage(os.Stderr)
		os.Exit(2)
	}

	var err error
	switch strings.ToLower(os.Args[1]) {
	case "run":
		err = runCommand(os.Args[2:], false)
	case "once":
		err = runCommand(os.Args[2:], true)
	case "version":
		err = versionCommand(os.Args[2:])
	case "install":
		err = installCommand(os.Args[2:])
	case "uninstall":
		err = service.Uninstall()
	case "start":
		err = service.Start()
	case "stop":
		err = service.Stop()
	case "status":
		var status string
		status, err = service.Status()
		if status != "" {
			fmt.Print(status)
			if !strings.HasSuffix(status, "\n") {
				fmt.Println()
			}
		}
	case "help", "-h", "--help":
		printUsage(os.Stdout)
	default:
		fmt.Fprintf(os.Stderr, "unknown command %q\n\n", os.Args[1])
		printUsage(os.Stderr)
		os.Exit(2)
	}

	if err != nil {
		fmt.Fprintln(os.Stderr, "staragent:", err)
		os.Exit(1)
	}
}

func runCommand(args []string, once bool) error {
	name := "run"
	if once {
		name = "once"
	}
	flags := flag.NewFlagSet(name, flag.ExitOnError)
	configPath := flags.String("config", config.DefaultPath(), "配置文件路径")
	server := flags.String("server", "", "覆盖服务端地址")
	dataDir := flags.String("data", "", "覆盖数据目录")
	logLevel := flags.String("log-level", "", "覆盖日志级别")
	allowExec := flags.Bool("allow-exec", false, "启用 shell 命令执行")
	console := flags.Bool("console", !once, "同时输出日志到 stderr")
	_ = flags.Parse(args)

	cfg, err := config.Load(*configPath)
	if err != nil {
		return err
	}
	if *server != "" {
		cfg.Server = *server
	}
	if *dataDir != "" {
		cfg.DataDir = *dataDir
	}
	if *logLevel != "" {
		cfg.LogLevel = *logLevel
	}
	if *allowExec {
		cfg.AllowExec = true
	}
	cfg.Normalize()
	if err := cfg.Validate(); err != nil {
		return err
	}

	logger, err := newLogger(cfg, *console)
	if err != nil {
		fmt.Fprintln(os.Stderr, "staragent: log setup failed, falling back to stderr:", err)
		logger = logging.NewConsole(cfg.LogLevel)
	}
	defer logger.Close()

	readyCtx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	instance, err := agent.New(readyCtx, agent.Options{
		ConfigPath: *configPath,
		Config:     cfg,
		Logger:     logger.Logger,
		Version:    Version,
		BuildTime:  buildTime(),
	})
	cancel()
	if err != nil {
		return err
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	if once {
		return instance.Once(ctx)
	}

	if service.IsWindowsService() {
		return service.RunService(instance.Run)
	}

	return instance.Run(ctx)
}

func installCommand(args []string) error {
	flags := flag.NewFlagSet("install", flag.ExitOnError)
	configPath := flags.String("config", config.DefaultPath(), "配置文件路径")
	installDir := flags.String("dir", "", "安装目录")
	dataDir := flags.String("data", "", "数据目录")
	user := flags.String("user", "", "服务运行用户（Linux）")
	server := flags.String("server", "", "服务端地址（首次安装必填）")
	logLevel := flags.String("log-level", "", "日志级别")
	_ = flags.Parse(args)

	cfgPath, err := filepath.Abs(*configPath)
	if err != nil {
		return err
	}
	if _, statErr := os.Stat(cfgPath); errors.Is(statErr, os.ErrNotExist) && *server == "" {
		return errors.New("-server is required when config file does not exist")
	} else if statErr == nil {
		existing, err := config.Load(cfgPath)
		if err != nil {
			return err
		}
		if *server != "" {
			existing.Server = *server
		}
		if err := existing.Validate(); err != nil {
			return fmt.Errorf("existing config is not runnable: %w", err)
		}
	}

	if *logLevel != "" || *server != "" || *dataDir != "" {
		if _, statErr := os.Stat(cfgPath); statErr == nil {
			cfg, err := config.Load(cfgPath)
			if err != nil {
				return err
			}
			if *server != "" {
				cfg.Server = *server
			}
			if *dataDir != "" {
				cfg.DataDir = *dataDir
			}
			if *logLevel != "" {
				cfg.LogLevel = *logLevel
			}
			cfg.Normalize()
			if err := cfg.Validate(); err != nil {
				return err
			}
			if err := config.Save(cfgPath, cfg); err != nil {
				return err
			}
		}
	}

	if err := service.Install(service.InstallOptions{
		ConfigPath:  cfgPath,
		InstallDir:  *installDir,
		DataDir:     *dataDir,
		ServiceUser: *user,
		Server:      *server,
	}); err != nil {
		return err
	}

	fmt.Printf("StarAgent installed. config=%s\n", cfgPath)

	return nil
}

func versionCommand(args []string) error {
	flags := flag.NewFlagSet("version", flag.ExitOnError)
	_ = flags.Parse(args)

	fmt.Printf("StarAgent Native Go\nVersion: %s\nBuildTime: %s\nRuntime: %s %s/%s\n",
		Version, buildTime().UTC().Format(time.RFC3339), runtime.Version(), runtime.GOOS, runtime.GOARCH)

	return nil
}

func newLogger(cfg *config.Config, console bool) (*logging.Logger, error) {
	return logging.New(filepath.Join(cfg.DataDir, "logs"), cfg.LogLevel, console)
}

func buildTime() time.Time {
	if BuildTime != "" {
		for _, layout := range []string{time.RFC3339, "2006-01-02 15:04:05", "2006-01-02"} {
			if value, err := time.Parse(layout, BuildTime); err == nil {
				return value.UTC()
			}
		}
	}
	if info, ok := debug.ReadBuildInfo(); ok {
		for _, setting := range info.Settings {
			if setting.Key == "vcs.time" {
				if value, err := time.Parse(time.RFC3339, setting.Value); err == nil {
					return value.UTC()
				}
			}
		}
	}

	return time.Now().UTC()
}

func printUsage(w *os.File) {
	fmt.Fprintf(w, `StarAgent Native Go %s

Usage:
  staragent <command> [options]

Commands:
  run       以长连接方式运行（Linux/systemd、Windows 服务、macOS/launchd）
  once      登录、心跳并处理一次命令后退出
  version   显示版本信息
  install   安装为系统服务
  uninstall 卸载系统服务
  start     启动服务
  stop      停止服务
  status    查看服务状态

Run options:
  -config string      配置文件路径（默认 %s）
  -server string      覆盖 Server
  -data string        覆盖 DataDir
  -log-level string   覆盖 LogLevel
  -allow-exec         启用命令执行（默认关闭）
  -console            输出日志到 stderr

Install options:
  -config string      配置文件路径
  -dir string         安装目录
  -data string        数据目录
  -user string        systemd 服务用户
  -server string      首次安装时写入的服务端地址
  -log-level string   日志级别

环境变量覆盖：STARAGENT_SERVER / STARAGENT_PRODUCTCODE / STARAGENT_CODE / STARAGENT_SECRET /
STARAGENT_PROJECT / STARAGENT_CHANNEL / STARAGENT_DATADIR / STARAGENT_LOGLEVEL /
STARAGENT_METRICSINTERVAL / STARAGENT_ALLOWEXEC / STARAGENT_ALLOWEDCOMMANDS /
STARAGENT_COMMANDTIMEOUT / STARAGENT_TLSVERIFY
`, Version, config.DefaultPath())
}
