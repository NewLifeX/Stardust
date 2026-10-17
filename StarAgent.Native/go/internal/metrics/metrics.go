package metrics

import (
	"context"
	"fmt"
	"runtime"
	"sort"
	"strings"
	"sync"
	"time"

	"github.com/shirou/gopsutil/v4/cpu"
	"github.com/shirou/gopsutil/v4/disk"
	"github.com/shirou/gopsutil/v4/host"
	"github.com/shirou/gopsutil/v4/load"
	"github.com/shirou/gopsutil/v4/mem"
	"github.com/shirou/gopsutil/v4/net"
	"github.com/shirou/gopsutil/v4/process"
)

// Snapshot 是一次指标采集结果，采集失败的字段保持 0。
type Snapshot struct {
	CPUPercent         float64
	Memory             uint64
	AvailableMemory    uint64
	FreeMemory         uint64
	TotalSize          uint64
	AvailableFreeSpace uint64
	DriveSize          uint64
	DriveInfo          string
	UplinkSpeed        uint64
	DownlinkSpeed      uint64
	ProcessCount       uint64
	Processes          string
	Uptime             uint64
	SystemLoad         float64
}

// MemoryStat 是内存指标。
type MemoryStat struct {
	Total     uint64
	Available uint64
	Free      uint64
}

// DiskStat 是磁盘容量指标。
type DiskStat struct {
	Total uint64
	Free  uint64
}

// Drive 是单个挂载点信息。
type Drive struct {
	Path  string
	Fs    string
	Total uint64
	Free  uint64
}

// Prober 抽象平台指标采集，便于测试失败降级。
type Prober interface {
	CPUPercent(ctx context.Context) (float64, error)
	Memory(ctx context.Context) (MemoryStat, error)
	DiskUsage(ctx context.Context, path string) (DiskStat, error)
	Drives(ctx context.Context) ([]Drive, error)
	NetTotals(ctx context.Context) (sent uint64, recv uint64, err error)
	ProcessCount(ctx context.Context) (uint64, error)
	ProcessNames(ctx context.Context) ([]string, error)
	Uptime(ctx context.Context) (uint64, error)
	Load(ctx context.Context) (float64, error)
}

// Collector 采集指标并计算网络速率。
type Collector struct {
	DataDir string

	prober    Prober
	now       func() time.Time
	collectMu sync.Mutex

	mu          sync.Mutex
	prevSent    uint64
	prevRecv    uint64
	prevNetTime time.Time
}

// New 创建使用系统采集器的指标收集器。
func New(dataDir string) *Collector {
	return NewWithProber(dataDir, systemProber{})
}

// NewWithProber 创建使用自定义采集器的指标收集器。
func NewWithProber(dataDir string, prober Prober) *Collector {
	return &Collector{DataDir: dataDir, prober: prober, now: time.Now}
}

// Collect 采集一次指标。任何单项失败都只让该项为 0，不影响整体。
func (c *Collector) Collect(ctx context.Context) Snapshot {
	c.collectMu.Lock()
	defer c.collectMu.Unlock()

	var snap Snapshot

	if p, err := c.prober.CPUPercent(ctx); err == nil {
		snap.CPUPercent = clamp(p, 0, 100)
	}
	if m, err := c.prober.Memory(ctx); err == nil {
		snap.Memory = m.Total
		snap.AvailableMemory = m.Available
		snap.FreeMemory = m.Free
	}
	if d, err := c.prober.DiskUsage(ctx, c.DataDir); err == nil {
		snap.TotalSize = d.Total
		snap.AvailableFreeSpace = d.Free
	}
	if drives, err := c.prober.Drives(ctx); err == nil {
		snap.DriveInfo, snap.DriveSize = describeDrives(drives)
	}
	if sent, recv, err := c.prober.NetTotals(ctx); err == nil {
		snap.UplinkSpeed, snap.DownlinkSpeed = c.speeds(sent, recv)
	}
	if count, err := c.prober.ProcessCount(ctx); err == nil {
		snap.ProcessCount = count
	}
	if names, err := c.prober.ProcessNames(ctx); err == nil {
		snap.Processes = joinProcessNames(names)
	}
	if uptime, err := c.prober.Uptime(ctx); err == nil {
		snap.Uptime = uptime
	}
	if value, err := c.prober.Load(ctx); err == nil {
		snap.SystemLoad = value
	}

	return snap
}

func (c *Collector) speeds(sent, recv uint64) (up, down uint64) {
	c.mu.Lock()
	defer c.mu.Unlock()

	now := c.now()
	if !c.prevNetTime.IsZero() {
		elapsed := now.Sub(c.prevNetTime).Seconds()
		if elapsed > 0 && sent >= c.prevSent && recv >= c.prevRecv {
			up = uint64(float64(sent-c.prevSent) / elapsed)
			down = uint64(float64(recv-c.prevRecv) / elapsed)
		}
	}

	c.prevSent = sent
	c.prevRecv = recv
	c.prevNetTime = now

	return up, down
}

func describeDrives(drives []Drive) (string, uint64) {
	if len(drives) == 0 {
		return "", 0
	}

	items := make([]string, 0, len(drives))
	var total uint64
	for _, drive := range drives {
		if drive.Total == 0 || strings.EqualFold(drive.Fs, "cdfs") || strings.EqualFold(drive.Fs, "iso9660") || strings.EqualFold(drive.Fs, "udf") {
			continue
		}
		total += drive.Total
		path := drive.Path
		if runtime.GOOS == "windows" && len(path) == 2 && path[1] == ':' {
			path += `\`
		}
		items = append(items, fmt.Sprintf("%s[%s]=%s/%s", path, drive.Fs, FormatSize(drive.Free), FormatSize(drive.Total)))
	}
	sort.Strings(items)

	return strings.Join(items, ","), total
}

var ignoredProcessNames = map[string]bool{
	"System":              true,
	"System Idle Process": true,
	"Idle":                true,
	"Registry":            true,
	"Memory Compression":  true,
}

func joinProcessNames(names []string) string {
	seen := make(map[string]bool, len(names))
	result := make([]string, 0, len(names))
	for _, name := range names {
		name = strings.TrimSpace(name)
		if name == "" || ignoredProcessNames[name] || strings.HasSuffix(name, "$") {
			continue
		}
		key := strings.ToLower(name)
		if seen[key] {
			continue
		}
		seen[key] = true
		result = append(result, name)
		if len(result) >= 200 {
			break
		}
	}
	sort.Strings(result)

	return strings.Join(result, ",")
}

// FormatSize 将字节数格式化为 1024 进制的人类可读文本。
func FormatSize(value uint64) string {
	const (
		k = 1024
		m = k * 1024
		g = m * 1024
		t = g * 1024
	)

	switch {
	case value >= t:
		return formatUnit(value, t, "T")
	case value >= g:
		return formatUnit(value, g, "G")
	case value >= m:
		return formatUnit(value, m, "M")
	case value >= k:
		return formatUnit(value, k, "K")
	default:
		return fmt.Sprintf("%dB", value)
	}
}

func formatUnit(value, unit uint64, suffix string) string {
	if value%unit == 0 {
		return fmt.Sprintf("%d%s", value/unit, suffix)
	}

	return fmt.Sprintf("%.1f%s", float64(value)/float64(unit), suffix)
}

func clamp(value, min, max float64) float64 {
	if value < min {
		return min
	}
	if value > max {
		return max
	}

	return value
}

type systemProber struct{}

func (systemProber) CPUPercent(ctx context.Context) (float64, error) {
	values, err := cpu.PercentWithContext(ctx, 0, false)
	if err != nil {
		return 0, err
	}
	if len(values) == 0 {
		return 0, fmt.Errorf("no cpu samples")
	}

	return values[0], nil
}

func (systemProber) Memory(ctx context.Context) (MemoryStat, error) {
	value, err := mem.VirtualMemoryWithContext(ctx)
	if err != nil {
		return MemoryStat{}, err
	}

	return MemoryStat{Total: value.Total, Available: value.Available, Free: value.Free}, nil
}

func (systemProber) DiskUsage(ctx context.Context, path string) (DiskStat, error) {
	value, err := disk.UsageWithContext(ctx, path)
	if err != nil {
		if runtime.GOOS == "windows" {
			return DiskStat{}, err
		}
		value, err = disk.UsageWithContext(ctx, "/")
		if err != nil {
			return DiskStat{}, err
		}
	}

	return DiskStat{Total: value.Total, Free: value.Free}, nil
}

func (systemProber) Drives(ctx context.Context) ([]Drive, error) {
	partitions, err := disk.PartitionsWithContext(ctx, false)
	if err != nil {
		return nil, err
	}

	drives := make([]Drive, 0, len(partitions))
	for _, partition := range partitions {
		usage, err := disk.UsageWithContext(ctx, partition.Mountpoint)
		if err != nil || usage.Total == 0 {
			continue
		}
		drives = append(drives, Drive{
			Path:  partition.Mountpoint,
			Fs:    partition.Fstype,
			Total: usage.Total,
			Free:  usage.Free,
		})
	}

	return drives, nil
}

func (systemProber) NetTotals(ctx context.Context) (uint64, uint64, error) {
	counters, err := net.IOCountersWithContext(ctx, false)
	if err != nil {
		return 0, 0, err
	}
	if len(counters) == 0 {
		return 0, 0, nil
	}

	return counters[0].BytesSent, counters[0].BytesRecv, nil
}

func (systemProber) ProcessCount(ctx context.Context) (uint64, error) {
	processes, err := process.ProcessesWithContext(ctx)
	if err != nil {
		return 0, err
	}

	return uint64(len(processes)), nil
}

func (systemProber) ProcessNames(ctx context.Context) ([]string, error) {
	processes, err := process.ProcessesWithContext(ctx)
	if err != nil {
		return nil, err
	}

	names := make([]string, 0, len(processes))
	for _, p := range processes {
		name, err := p.NameWithContext(ctx)
		if err != nil || name == "" {
			continue
		}
		names = append(names, name)
	}

	return names, nil
}

func (systemProber) Uptime(ctx context.Context) (uint64, error) {
	return host.UptimeWithContext(ctx)
}

func (systemProber) Load(ctx context.Context) (float64, error) {
	value, err := load.AvgWithContext(ctx)
	if err != nil {
		return 0, err
	}

	return value.Load1, nil
}
