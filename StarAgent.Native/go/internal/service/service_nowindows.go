//go:build !windows

package service

import "context"

// IsWindowsService 在非 Windows 平台恒为 false。
func IsWindowsService() bool { return false }

// RunService 在非 Windows 平台直接运行处理函数。
func RunService(run func(context.Context) error) error { return run(context.Background()) }
