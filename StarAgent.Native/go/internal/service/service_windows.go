//go:build windows

package service

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"golang.org/x/sys/windows/svc"
	"golang.org/x/sys/windows/svc/mgr"
)

// Install 安装 Windows 服务。
func Install(opts InstallOptions) error {
	if opts.InstallDir == "" {
		programData := os.Getenv("ProgramData")
		if programData == "" {
			programData = `C:\ProgramData`
		}
		opts.InstallDir = filepath.Join(programData, "StarAgent", "bin")
	}
	if opts.ConfigPath == "" {
		opts.ConfigPath = filepath.Join(opts.InstallDir, "staragent.json")
	}
	if opts.DataDir == "" {
		opts.DataDir = filepath.Join(filepath.Dir(opts.InstallDir), "data")
	}
	var err error
	if opts.InstallDir, err = absolutePath(opts.InstallDir); err != nil {
		return err
	}
	if opts.ConfigPath, err = absolutePath(opts.ConfigPath); err != nil {
		return err
	}

	if err := os.MkdirAll(opts.InstallDir, 0o755); err != nil {
		return fmt.Errorf("create install directory: %w", err)
	}
	exePath := filepath.Join(opts.InstallDir, "staragent.exe")
	if err := copyExecutable("", exePath); err != nil {
		return err
	}
	if err := ensureConfig(opts.ConfigPath, opts.Server, opts.DataDir); err != nil {
		return err
	}

	manager, err := mgr.Connect()
	if err != nil {
		return fmt.Errorf("connect service manager (run as administrator): %w", err)
	}
	defer manager.Disconnect()

	if existing, err := manager.OpenService(ServiceName); err == nil {
		_, _ = existing.Control(svc.Stop)
		waitForStop(existing, 20*time.Second)
		_ = existing.Delete()
		_ = existing.Close()
		time.Sleep(500 * time.Millisecond)
	}

	service, err := manager.CreateService(
		ServiceName,
		exePath,
		mgr.Config{
			DisplayName: "StarAgent Native Agent",
			Description: "StarAgent 原生 Go 代理",
			StartType:   mgr.StartAutomatic,
		},
		"run", "-config", opts.ConfigPath,
	)
	if err != nil {
		return fmt.Errorf("create service: %w", err)
	}
	defer service.Close()

	if err := service.Start(); err != nil {
		return fmt.Errorf("start service: %w", err)
	}

	return nil
}

// Uninstall 停止并删除 Windows 服务。
func Uninstall() error {
	manager, err := mgr.Connect()
	if err != nil {
		return fmt.Errorf("connect service manager (run as administrator): %w", err)
	}
	defer manager.Disconnect()

	service, err := manager.OpenService(ServiceName)
	if err != nil {
		return nil
	}
	defer service.Close()

	_, _ = service.Control(svc.Stop)
	waitForStop(service, 20*time.Second)
	if err := service.Delete(); err != nil {
		return fmt.Errorf("delete service: %w", err)
	}

	return nil
}

// Start 启动 Windows 服务。
func Start() error {
	manager, err := mgr.Connect()
	if err != nil {
		return fmt.Errorf("connect service manager: %w", err)
	}
	defer manager.Disconnect()

	service, err := manager.OpenService(ServiceName)
	if err != nil {
		return fmt.Errorf("open service %s: %w", ServiceName, err)
	}
	defer service.Close()

	if err := service.Start(); err != nil {
		return fmt.Errorf("start service: %w", err)
	}

	return nil
}

// Stop 停止 Windows 服务。
func Stop() error {
	manager, err := mgr.Connect()
	if err != nil {
		return fmt.Errorf("connect service manager: %w", err)
	}
	defer manager.Disconnect()

	service, err := manager.OpenService(ServiceName)
	if err != nil {
		return fmt.Errorf("open service %s: %w", ServiceName, err)
	}
	defer service.Close()

	if _, err := service.Control(svc.Stop); err != nil {
		return fmt.Errorf("stop service: %w", err)
	}
	waitForStop(service, 30*time.Second)

	return nil
}

// Status 返回 Windows 服务状态。
func Status() (string, error) {
	manager, err := mgr.Connect()
	if err != nil {
		return "", fmt.Errorf("connect service manager: %w", err)
	}
	defer manager.Disconnect()

	service, err := manager.OpenService(ServiceName)
	if err != nil {
		return "not installed", err
	}
	defer service.Close()

	status, err := service.Query()
	if err != nil {
		return "", fmt.Errorf("query service: %w", err)
	}

	return stateText(status.State), nil
}

// IsWindowsService 判断当前进程是否由 SCM 启动。
func IsWindowsService() bool {
	ok, err := svc.IsWindowsService()
	return err == nil && ok
}

// RunService 以 Windows 服务方式运行。
func RunService(run func(context.Context) error) error {
	return svc.Run(ServiceName, &serviceHandler{run: run})
}

type serviceHandler struct {
	run func(context.Context) error
}

func (h *serviceHandler) Execute(_ []string, requests <-chan svc.ChangeRequest, changes chan<- svc.Status) (bool, uint32) {
	changes <- svc.Status{State: svc.StartPending}

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	done := make(chan error, 1)
	go func() { done <- h.run(ctx) }()

	changes <- svc.Status{State: svc.Running, Accepts: svc.AcceptStop | svc.AcceptShutdown}

	for {
		select {
		case request := <-requests:
			switch request.Cmd {
			case svc.Interrogate:
				changes <- request.CurrentStatus
			case svc.Stop, svc.Shutdown:
				changes <- svc.Status{State: svc.StopPending}
				cancel()
				select {
				case <-done:
				case <-time.After(20 * time.Second):
				}
				return false, 0
			}
		case err := <-done:
			if err != nil {
				return false, 1
			}
			return false, 0
		}
	}
}

func waitForStop(service *mgr.Service, timeout time.Duration) {
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		status, err := service.Query()
		if err != nil || status.State == svc.Stopped {
			return
		}
		time.Sleep(500 * time.Millisecond)
	}
}

func stateText(state svc.State) string {
	switch state {
	case svc.Stopped:
		return "stopped"
	case svc.StartPending:
		return "start pending"
	case svc.StopPending:
		return "stop pending"
	case svc.Running:
		return "running"
	case svc.ContinuePending:
		return "continue pending"
	case svc.PausePending:
		return "pause pending"
	case svc.Paused:
		return "paused"
	default:
		return fmt.Sprintf("state %d", state)
	}
}
