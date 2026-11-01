// SPDX-License-Identifier: MIT
// StarAgent Native C++ - platform abstraction.
#pragma once

#include "util.hpp"

#include <atomic>
#include <cstdint>
#include <string>
#include <vector>

namespace staragent::platform {

struct MemoryInfo
{
    std::uint64_t Total = 0;
    std::uint64_t Available = 0;
    std::uint64_t Free = 0;
};

struct DiskInfo
{
    std::uint64_t Total = 0;
    std::uint64_t Free = 0;
};

struct DriveEntry
{
    std::string Path;
    std::string FileSystem;
    std::uint64_t Total = 0;
    std::uint64_t Free = 0;
};

struct NetTotals
{
    bool Valid = false;
    std::uint64_t Sent = 0;
    std::uint64_t Received = 0;
};

// TCP 连接状态计数，对应心跳中的 TcpConnections/TcpTimeWait/TcpCloseWait。
struct TcpCounts
{
    std::uint64_t Established = 0;
    std::uint64_t TimeWait = 0;
    std::uint64_t CloseWait = 0;
};

// 磁盘 IO 采样，对应心跳中的 DiskIOPS 与 DiskActiveTime（百分比）。
struct DiskIoSample
{
    std::uint64_t Iops = 0;
    double ActivePercent = 0;
};

// ICMP 探测结果，对应心跳中的网络质量字段。
struct ProbeResult
{
    bool Valid = false;
    int Latency = 0;     // 平均延迟（毫秒）
    double LossRate = 0; // 丢包率 0~1
    double Score = 0;    // 综合评分 0~1
};

struct HardwareInfo
{
    std::string UUID;
    std::string MachineGuid;
    std::string SerialNumber;
    std::string Board;
    std::string DiskID;
    std::string Macs;
    std::string Product;
    std::string Vendor;
    std::string Processor;
    std::string GPU;
    std::string Dpi;
    std::string Resolution;
    std::string CLibVersion;
};

struct SystemInfo
{
    std::string OSName;
    std::string OSVersion;
    std::string HostName;
    std::string UserName;
    std::string TimeZone;
};

struct NetworkInfo
{
    std::string IP;
    std::string Gateway;
    std::string Dns;
};

struct ShellResult
{
    std::string Output;
    int ExitCode = -1;
    bool Truncated = false;
    bool TimedOut = false;
    bool Cancelled = false;
    std::string Error;
};

double CpuPercent();
MemoryInfo Memory();
DiskInfo DiskUsage(const std::string& path);
std::vector<DriveEntry> Drives();
NetTotals NetTotalsSinceStart();
std::uint64_t ProcessCount();
std::vector<std::string> ProcessNames();
std::uint64_t UptimeSeconds();
double LoadAverage();
// 统计本机 TCP 连接状态分布；不支持统计的平台返回全 0。
TcpCounts TcpStates();
// 采集 CPU 温度（摄氏度）；无温度传感器时返回 0。
double Temperature();
// 采集电池电量百分比（0~100）；无电池或未知时返回 0。
double BatteryPercent();
// 采集无线信号强度（0~100）；有线或无无线网卡时返回 0。
int WirelessSignal();
// 采样磁盘 IO 速率；首次调用返回 0，之后按两次采样的间隔计算。
DiskIoSample DiskIo();
// 当前进程可打开文件句柄上限；平台无此概念时返回 0。
std::uint64_t MaxOpenFiles();
// 对目标主机做 ICMP 探测，用于评估网关/DNS/服务端网络质量。
ProbeResult PingProbe(const std::string& host, int timeout_ms, int count);
// 重启或关机本机。reboot=true 重启，false 关机；返回命令是否成功提交。
bool PowerAction(bool reboot);

HardwareInfo Hardware();
SystemInfo System();
NetworkInfo Network();
std::string LocalIP();
std::string Gateway();
std::string Dns();
std::string Architecture();

std::string ExecutablePath();
std::string ExecutableDir();
int ProcessId();
bool IsRoot();
std::string HomeDir();
std::string DefaultDataDir();
std::string TimeZoneName();
bool SetSystemTime(TimePoint value);

// Runs a shell script with a hard timeout. The merged stdout/stderr output is
// capped at 64KB (head and tail are preserved).
ShellResult RunShell(const std::string& script, int timeout_ms, const std::atomic<bool>& cancelled);

// Starts a detached copy of the current executable with the supplied
// arguments. Used by node/restart.
bool RestartSelf(const std::vector<std::string>& arguments);

// Sleeps for the given duration, returning false when cancelled becomes true.
bool SleepCancelable(int milliseconds, const std::atomic<bool>& cancelled);

} // namespace staragent::platform
