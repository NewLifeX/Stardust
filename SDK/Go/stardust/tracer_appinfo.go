package stardust

import (
	"os"
	"os/user"
	"runtime"
	"strings"
	"time"
)

// AppInfo 进程与应用信息，用于心跳与监控上报
type AppInfo struct {
	Id               int     `json:"Id"`
	Name             string  `json:"Name"`
	Version          string  `json:"Version"`
	CommandLine      string  `json:"CommandLine"`
	UserName         string  `json:"UserName"`
	MachineName      string  `json:"MachineName"`
	IP               string  `json:"IP"`
	StartTime        string  `json:"StartTime"`
	ProcessorTime    int64   `json:"ProcessorTime"`
	CpuUsage         float64 `json:"CpuUsage"`
	WorkingSet       int64   `json:"WorkingSet"`
	HeapSize         int64   `json:"HeapSize"`
	Threads          int     `json:"Threads"`
	WorkerThreads    int     `json:"WorkerThreads"`
	IOThreads        int     `json:"IOThreads"`
	AvailableThreads int     `json:"AvailableThreads"`
	PendingItems     int64   `json:"PendingItems"`
	CompletedItems   int64   `json:"CompletedItems"`
	Handles          int     `json:"Handles"`
	Connections      int     `json:"Connections"`
	GCCount          int     `json:"GCCount"`
	Listens          string  `json:"Listens"`
	Time             int64   `json:"Time"`
}

// appInfo 采集当前进程的应用信息
func (t *Tracer) appInfo() AppInfo {
	var mem runtime.MemStats
	runtime.ReadMemStats(&mem)

	// GCCount 为自上次采集以来的 GC 次数增量
	t.gcMu.Lock()
	gcCount := int(mem.NumGC - t.lastGC)
	t.lastGC = mem.NumGC
	t.gcMu.Unlock()

	name, version, ip := t.identity()

	userName := ""
	if u, err := user.Current(); err == nil && u != nil {
		userName = u.Username
	}

	machineName, err := os.Hostname()
	if err != nil {
		machineName = ""
	}
	if ip == "" {
		ip = getLocalIP()
	}

	startTime := t.startTime
	if startTime.IsZero() {
		startTime = time.Now()
	}

	return AppInfo{
		Id:          os.Getpid(),
		Name:        name,
		Version:     version,
		CommandLine: strings.Join(os.Args, " "),
		UserName:    userName,
		MachineName: machineName,
		IP:          ip,
		// 服务端按日期字符串解析开始时间
		StartTime:  startTime.Format(time.RFC3339),
		WorkingSet: int64(mem.Sys),
		HeapSize:   int64(mem.HeapAlloc),
		Threads:    runtime.NumGoroutine(),
		GCCount:    gcCount,
		Time:       time.Now().UnixMilli(),
	}
}
