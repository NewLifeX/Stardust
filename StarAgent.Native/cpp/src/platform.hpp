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
