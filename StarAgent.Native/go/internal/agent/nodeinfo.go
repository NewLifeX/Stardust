package agent

import (
	"context"
	"fmt"
	"runtime"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/identity"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

func (a *Agent) buildNodeInfo(ctx context.Context, cfg config.Config) protocol.NodeInfo {
	ip, gateway, dns := a.networkSnapshot(ctx, true)
	snap := a.metrics.Collect(ctx)

	return protocol.NodeInfo{
		Version:            a.version(),
		Compile:            protocol.NewTime(a.opts.BuildTime),
		OSName:             a.system.OSName,
		OSVersion:          a.system.OSVersion,
		Architecture:       identity.Architecture(),
		MachineName:        a.system.Hostname,
		UserName:           identity.UserName(),
		IP:                 ip,
		Gateway:            gateway,
		Dns:                dns,
		ProcessorCount:     runtime.NumCPU(),
		Memory:             snap.Memory,
		AvailableMemory:    snap.AvailableMemory,
		FreeMemory:         snap.FreeMemory,
		TotalSize:          snap.TotalSize,
		AvailableFreeSpace: snap.AvailableFreeSpace,
		DriveSize:          snap.DriveSize,
		DriveInfo:          snap.DriveInfo,
		UUID:               a.hardware.UUID,
		MachineGuid:        a.hardware.MachineGuid,
		SerialNumber:       a.hardware.SerialNumber,
		Board:              a.hardware.Board,
		DiskID:             a.hardware.DiskID,
		Macs:               a.hardware.Macs,
		InstallPath:        identity.ExecutableDir(),
		Runtime:            runtime.Version(),
		Framework:          runtime.Version(),
		TimeZone:           time.Local.String(),
		Time:               protocol.NewTime(time.Now()),
		Plugins:            a.plugins(cfg),
	}
}

func (a *Agent) buildPingInfo(ctx context.Context) protocol.PingInfo {
	ip, gateway, dns := a.networkSnapshot(ctx, false)
	snap := a.metrics.Collect(ctx)
	delay := a.lastDelay.Load()
	if delay < 0 {
		delay = 0
	}

	return protocol.PingInfo{
		Time:               time.Now().UnixMilli(),
		IP:                 ip,
		Gateway:            gateway,
		Dns:                dns,
		Macs:               a.hardware.Macs,
		DriveInfo:          snap.DriveInfo,
		Framework:          runtime.Version(),
		Processes:          snap.Processes,
		ProcessCount:       snap.ProcessCount,
		Memory:             snap.Memory,
		AvailableMemory:    snap.AvailableMemory,
		FreeMemory:         snap.FreeMemory,
		TotalSize:          snap.TotalSize,
		AvailableFreeSpace: snap.AvailableFreeSpace,
		CpuRate:            snap.CPUPercent,
		UplinkSpeed:        snap.UplinkSpeed,
		DownlinkSpeed:      snap.DownlinkSpeed,
		Uptime:             snap.Uptime,
		Delay:              delay,
		SystemLoad:         snap.SystemLoad,
		ServerLatency:      uint64(delay),
	}
}

func (a *Agent) plugins(cfg config.Config) []string {
	return []string{
		"agent:go",
		fmt.Sprintf("cap:exec:%t", cfg.AllowExec),
		"cap:upgrade=false",
		"cap:deploy=false",
		"os:" + runtime.GOOS,
	}
}

func (a *Agent) networkSnapshot(ctx context.Context, force bool) (string, string, string) {
	a.networkMu.Lock()
	defer a.networkMu.Unlock()

	if !force && time.Since(a.networkAt) < 5*time.Minute {
		return a.ip, a.gateway, a.dns
	}

	a.ip = identity.LocalIP()
	a.gateway = identity.Gateway(ctx)
	a.dns = identity.DNS(ctx)
	a.networkAt = time.Now()

	return a.ip, a.gateway, a.dns
}

func (a *Agent) localIP() string {
	a.networkMu.Lock()
	defer a.networkMu.Unlock()

	return a.ip
}

func (a *Agent) version() string {
	if a.opts.Version != "" {
		return a.opts.Version
	}

	return defaultVersion
}

func (a *Agent) serverNow() time.Time {
	return time.Now().Add(time.Duration(a.serverOffset.Load()) * time.Millisecond)
}
