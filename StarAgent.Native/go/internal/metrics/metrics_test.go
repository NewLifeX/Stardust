package metrics

import (
	"context"
	"errors"
	"strings"
	"testing"
	"time"
)

type fakeProber struct {
	cpu       float64
	cpuErr    error
	memory    MemoryStat
	memoryErr error
	disk      DiskStat
	diskErr   error
	drives    []Drive
	drivesErr error
	sent      uint64
	recv      uint64
	netErr    error
	procCount uint64
	procErr   error
	procNames []string
	namesErr  error
	uptime    uint64
	uptimeErr error
	load      float64
	loadErr   error
}

func (f *fakeProber) CPUPercent(context.Context) (float64, error) { return f.cpu, f.cpuErr }
func (f *fakeProber) Memory(context.Context) (MemoryStat, error)  { return f.memory, f.memoryErr }
func (f *fakeProber) DiskUsage(context.Context, string) (DiskStat, error) {
	return f.disk, f.diskErr
}
func (f *fakeProber) Drives(context.Context) ([]Drive, error) { return f.drives, f.drivesErr }
func (f *fakeProber) NetTotals(context.Context) (uint64, uint64, error) {
	return f.sent, f.recv, f.netErr
}
func (f *fakeProber) ProcessCount(context.Context) (uint64, error) { return f.procCount, f.procErr }
func (f *fakeProber) ProcessNames(context.Context) ([]string, error) {
	return f.procNames, f.namesErr
}
func (f *fakeProber) Uptime(context.Context) (uint64, error) { return f.uptime, f.uptimeErr }
func (f *fakeProber) Load(context.Context) (float64, error)  { return f.load, f.loadErr }

func TestCollectFailureFallsBackToZero(t *testing.T) {
	err := errors.New("not supported")
	prober := &fakeProber{
		cpuErr:    err,
		memoryErr: err,
		diskErr:   err,
		drivesErr: err,
		netErr:    err,
		procErr:   err,
		namesErr:  err,
		uptimeErr: err,
		loadErr:   err,
	}
	collector := NewWithProber(t.TempDir(), prober)

	snap := collector.Collect(context.Background())
	if snap != (Snapshot{}) {
		t.Fatalf("expected zero snapshot on failures, got %+v", snap)
	}
}

func TestCollectValues(t *testing.T) {
	prober := &fakeProber{
		cpu:       85.5,
		memory:    MemoryStat{Total: 1000, Available: 400, Free: 200},
		disk:      DiskStat{Total: 5000, Free: 2500},
		drives:    []Drive{{Path: "/", Fs: "ext4", Total: 1 << 30, Free: 1 << 29}, {Path: "/data", Fs: "xfs", Total: 0, Free: 0}},
		sent:      1000,
		recv:      2000,
		procCount: 42,
		procNames: []string{"nginx", "dotnet", "nginx", "systemd", "os$"},
		uptime:    3600,
		load:      0.75,
	}
	collector := NewWithProber("/data", prober)
	now := time.Unix(1700000000, 0)
	collector.now = func() time.Time { return now }

	first := collector.Collect(context.Background())
	if first.CPUPercent != 85.5 || first.Memory != 1000 || first.AvailableMemory != 400 || first.FreeMemory != 200 {
		t.Fatalf("unexpected cpu/memory: %+v", first)
	}
	if first.TotalSize != 5000 || first.AvailableFreeSpace != 2500 {
		t.Fatalf("unexpected disk: %+v", first)
	}
	if first.DriveSize != 1<<30 {
		t.Fatalf("DriveSize = %d", first.DriveSize)
	}
	if !strings.Contains(first.DriveInfo, "/[ext4]=") {
		t.Fatalf("DriveInfo = %q", first.DriveInfo)
	}
	if first.UplinkSpeed != 0 || first.DownlinkSpeed != 0 {
		t.Fatalf("first sample should have zero speeds: %+v", first)
	}
	if first.ProcessCount != 42 || first.Processes != "dotnet,nginx,systemd" {
		t.Fatalf("unexpected processes: %q count=%d", first.Processes, first.ProcessCount)
	}
	if first.Uptime != 3600 || first.SystemLoad != 0.75 {
		t.Fatalf("unexpected uptime/load: %+v", first)
	}

	now = now.Add(10 * time.Second)
	prober.sent += 1024
	prober.recv += 2048
	second := collector.Collect(context.Background())
	if second.UplinkSpeed != 102 || second.DownlinkSpeed != 204 {
		t.Fatalf("unexpected speeds: up=%d down=%d", second.UplinkSpeed, second.DownlinkSpeed)
	}
}

func TestCollectClampsCPU(t *testing.T) {
	prober := &fakeProber{cpu: 150}
	collector := NewWithProber(t.TempDir(), prober)
	if got := collector.Collect(context.Background()).CPUPercent; got != 100 {
		t.Fatalf("CPUPercent = %v, want 100", got)
	}
}

func TestFormatSize(t *testing.T) {
	cases := map[uint64]string{
		0:          "0B",
		512:        "512B",
		1536:       "1.5K",
		2 << 20:    "2M",
		50 << 30:   "50G",
		1024 << 30: "1T",
	}
	for input, want := range cases {
		if got := FormatSize(input); got != want {
			t.Fatalf("FormatSize(%d) = %q, want %q", input, got, want)
		}
	}
}
