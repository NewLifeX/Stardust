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
			info.OSName = stat.Platform
		}
		info.OSVersion = stat.PlatformVersion
		if info.Hostname == "" {
			info.Hostname = stat.Hostname
		}
	}

	if runtime.GOOS == "linux" {
		if name := linuxOSName(); name != "" {
			info.OSName = name
		}
	}

	return info
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
