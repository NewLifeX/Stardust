package identity

import (
	"context"
	"runtime"
	"strings"

	"github.com/shirou/gopsutil/v4/host"
)

// SystemInfo 是节点操作系统信息。
type SystemInfo struct {
	OSName    string
	OSVersion string
	Hostname  string
}

// System 采集操作系统信息，失败时退化为 runtime 信息。
func System(ctx context.Context) SystemInfo {
	info := SystemInfo{
		OSName:   runtime.GOOS,
		Hostname: HostName(),
	}

	if stat, err := host.InfoWithContext(ctx); err == nil {
		if stat.Platform != "" {
			info.OSName = strings.TrimSpace(strings.TrimPrefix(stat.Platform, "Microsoft "))
		}
		info.OSVersion = stat.PlatformVersion
		if info.Hostname == "" {
			info.Hostname = stat.Hostname
		}
	}

	if runtime.GOOS == "windows" {
		// Windows 服务端按 OSName 前缀和 OSVersion 数值识别系统类型，22H2 这类显示版本会导致误判。
		if version, err := host.KernelVersionWithContext(ctx); err == nil && version != "" {
			info.OSVersion = normalizeWindowsVersion(version)
		}
	}

	if runtime.GOOS == "linux" {
		if name := linuxOSName(); name != "" {
			info.OSName = name
		}
	}

	return info
}

func normalizeWindowsVersion(value string) string {
	value = strings.TrimSpace(value)
	if index := strings.IndexByte(value, ' '); index > 0 {
		value = value[:index]
	}
	parts := strings.Split(value, ".")
	if len(parts) > 3 {
		parts = parts[:3]
	}

	return strings.Join(parts, ".")
}

func linuxOSName() string {
	data := readTrim("/etc/os-release")
	for _, line := range strings.Split(data, "\n") {
		if strings.HasPrefix(line, "PRETTY_NAME=") {
			return strings.Trim(strings.TrimPrefix(line, "PRETTY_NAME="), `"`)
		}
	}
	for _, line := range strings.Split(data, "\n") {
		if strings.HasPrefix(line, "NAME=") {
			return strings.Trim(strings.TrimPrefix(line, "NAME="), `"`)
		}
	}

	return ""
}
