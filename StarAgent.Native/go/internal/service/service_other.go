//go:build !linux && !darwin && !windows

package service

import (
	"fmt"
	"runtime"
)

// Install 在不支持的平台返回错误。
func Install(InstallOptions) error {
	return fmt.Errorf("service installation is not supported on %s", runtime.GOOS)
}

// Uninstall 在不支持的平台返回错误。
func Uninstall() error {
	return fmt.Errorf("service uninstall is not supported on %s", runtime.GOOS)
}

// Start 在不支持的平台返回错误。
func Start() error {
	return fmt.Errorf("service start is not supported on %s", runtime.GOOS)
}

// Stop 在不支持的平台返回错误。
func Stop() error {
	return fmt.Errorf("service stop is not supported on %s", runtime.GOOS)
}

// Status 在不支持的平台返回错误。
func Status() (string, error) {
	return "", fmt.Errorf("service status is not supported on %s", runtime.GOOS)
}
