// SPDX-License-Identifier: MIT
// Linux metrics, identity and command execution.
#include "platform.hpp"

#include "config.hpp"
#include "output_buffer.hpp"
#include "platform_posix.hpp"

#include <algorithm>
#include <arpa/inet.h>
#include <cctype>
#include <cerrno>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <dirent.h>
#include <fcntl.h>
#include <fstream>
#include <ifaddrs.h>
#include <mutex>
#include <net/if.h>
#include <netinet/in.h>
#include <netpacket/packet.h>
#include <pwd.h>
#include <set>
#include <sstream>
#include <sys/stat.h>
#include <sys/statvfs.h>
#include <sys/sysinfo.h>
#include <sys/time.h>
#include <sys/types.h>
#include <sys/utsname.h>
#include <sys/wait.h>
#include <thread>
#include <unistd.h>

#if defined(__GLIBC__)
#include <gnu/libc-version.h>
#endif
#include <sys/resource.h>

namespace staragent::platform {
namespace {

std::mutex g_mutex;
std::uint64_t g_prev_idle = 0;
std::uint64_t g_prev_total = 0;
bool g_has_cpu_sample = false;

// 以下解析/读取逻辑与 macOS 共用，实现见 platform_posix.cpp。
using internal::CleanId;
using internal::FormatMac;
using internal::IsVirtualInterface;
using internal::SockaddrToIp;

std::string ReadFile(const std::string& path, std::size_t max = 1024 * 1024)
{
    return internal::ReadSysFile(path, max);
}

std::string ReadTrim(const std::string& path) { return internal::ReadSysTrim(path); }

std::string LinuxGateway()
{
    std::istringstream stream(ReadFile("/proc/net/route"));
    std::string line;
    std::getline(stream, line);
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string interface, destination, gateway;
        if (!(fields >> interface >> destination >> gateway)) continue;
        if (destination != "00000000") continue;
        const auto value = static_cast<std::uint32_t>(std::strtoul(gateway.c_str(), nullptr, 16));
        sockaddr_in address{};
        address.sin_family = AF_INET;
        address.sin_addr.s_addr = value;
        return SockaddrToIp(reinterpret_cast<const sockaddr*>(&address));
    }
    return {};
}

std::string LinuxDns() { return internal::DnsServers(); }

std::string LinuxDiskSerial()
{
    DIR* directory = ::opendir("/sys/block");
    if (directory == nullptr) return {};
    std::string result;
    while (auto* entry = ::readdir(directory))
    {
        const std::string name = entry->d_name;
        if (name == "." || name == "..") continue;
        if (StartsWith(name, "loop") || StartsWith(name, "ram") || StartsWith(name, "dm-") ||
            StartsWith(name, "md"))
        {
            continue;
        }
        auto serial = ReadTrim("/sys/block/" + name + "/device/serial");
        if (serial.empty()) serial = ReadTrim("/sys/block/" + name + "/serial");
        if (StartsWith(serial, "0x") || StartsWith(serial, "0X")) serial.erase(0, 2);
        if (!serial.empty())
        {
            result = serial;
            break;
        }
    }
    ::closedir(directory);
    return result;
}

std::string ReadOsRelease(const std::string& key)
{
    std::istringstream stream(ReadFile("/etc/os-release"));
    std::string line;
    while (std::getline(stream, line))
    {
        const auto position = line.find('=');
        if (position == std::string::npos) continue;
        if (line.substr(0, position) != key) continue;
        auto value = line.substr(position + 1);
        if (value.size() >= 2 && value.front() == '"' && value.back() == '"')
        {
            value = value.substr(1, value.size() - 2);
        }
        return value;
    }
    return {};
}

} // namespace

double CpuPercent()
{
    const auto content = ReadFile("/proc/stat", 4096);
    const auto newline = content.find('\n');
    if (!StartsWith(content, "cpu ")) return 0;
    std::istringstream stream(content.substr(0, newline));
    std::string label;
    std::uint64_t values[10] = {};
    stream >> label;
    for (auto& value : values) stream >> value;
    if (!stream) return 0;

    const std::uint64_t idle = values[3] + values[4];
    const std::uint64_t total = values[0] + values[1] + values[2] + values[3] + values[4] + values[5] + values[6] +
                                values[7] + values[8] + values[9];

    std::lock_guard<std::mutex> lock(g_mutex);
    if (!g_has_cpu_sample)
    {
        g_prev_idle = idle;
        g_prev_total = total;
        g_has_cpu_sample = true;
        return 0;
    }
    const auto idle_delta = idle - g_prev_idle;
    const auto total_delta = total - g_prev_total;
    g_prev_idle = idle;
    g_prev_total = total;
    if (total_delta == 0) return 0;
    const auto busy = total_delta > idle_delta ? total_delta - idle_delta : 0;
    return std::max(0.0, std::min(100.0, 100.0 * static_cast<double>(busy) / static_cast<double>(total_delta)));
}

MemoryInfo Memory()
{
    MemoryInfo result;
    std::istringstream stream(ReadFile("/proc/meminfo"));
    std::string line;
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string key;
        std::uint64_t value = 0;
        std::string unit;
        if (!(fields >> key >> value)) continue;
        fields >> unit;
        const std::uint64_t bytes = value * 1024;
        if (key == "MemTotal:") result.Total = bytes;
        else if (key == "MemAvailable:") result.Available = bytes;
        else if (key == "MemFree:") result.Free = bytes;
    }
    if (result.Available == 0) result.Available = result.Free;
    return result;
}

DiskInfo DiskUsage(const std::string& path)
{
    struct statvfs stats{};
    if (::statvfs(path.c_str(), &stats) != 0) return {};
    DiskInfo result;
    result.Total = static_cast<std::uint64_t>(stats.f_blocks) * stats.f_frsize;
    result.Free = static_cast<std::uint64_t>(stats.f_bavail) * stats.f_frsize;
    return result;
}

std::vector<DriveEntry> Drives()
{
    std::vector<DriveEntry> result;
    std::istringstream stream(ReadFile("/proc/mounts"));
    std::string line;
    static const std::set<std::string> ignored = {
        "proc",        "sysfs",     "devtmpfs", "devpts",      "cgroup",     "cgroup2",    "securityfs",
        "pstore",      "efivarfs",  "bpf",      "tracefs",     "debugfs",    "configfs",   "fusectl",
        "mqueue",      "hugetlbfs", "ramfs",    "autofs",      "binfmt_misc", "rpc_pipefs", "nsfs",
        "tmpfs",       "zram",      "zram0",    "none"};
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string device, path, file_system;
        if (!(fields >> device >> path >> file_system)) continue;
        if (ignored.count(file_system) > 0) continue;
        if (StartsWith(path, "/sys") || StartsWith(path, "/proc") || StartsWith(path, "/dev") ||
            StartsWith(path, "/run"))
        {
            continue;
        }
        // 容器/嵌入式常见只读根文件系统：仅保留根挂载点，其余镜像层不参与磁盘统计
        if ((file_system == "overlay" || file_system == "squashfs") && path != "/") continue;
        struct statvfs stats{};
        if (::statvfs(path.c_str(), &stats) != 0) continue;
        const auto total = static_cast<std::uint64_t>(stats.f_blocks) * stats.f_frsize;
        if (total == 0) continue;
        DriveEntry entry;
        entry.Path = path;
        entry.FileSystem = file_system;
        entry.Total = total;
        entry.Free = static_cast<std::uint64_t>(stats.f_bavail) * stats.f_frsize;
        result.push_back(std::move(entry));
    }
    return result;
}

NetTotals NetTotalsSinceStart()
{
    NetTotals result;
    std::istringstream stream(ReadFile("/proc/net/dev"));
    std::string line;
    std::getline(stream, line);
    std::getline(stream, line);
    while (std::getline(stream, line))
    {
        const auto colon = line.find(':');
        if (colon == std::string::npos) continue;
        auto interface = Trim(line.substr(0, colon));
        if (interface == "lo" || StartsWith(interface, "lo:")) continue;
        std::istringstream fields(line.substr(colon + 1));
        std::uint64_t receive = 0, transmit = 0;
        std::uint64_t values[16] = {};
        for (auto& value : values)
        {
            if (!(fields >> value)) break;
        }
        receive = values[0];
        transmit = values[8];
        result.Received += receive;
        result.Sent += transmit;
        result.Valid = true;
    }
    return result;
}

std::uint64_t ProcessCount()
{
    DIR* directory = ::opendir("/proc");
    if (directory == nullptr) return 0;
    std::uint64_t count = 0;
    while (auto* entry = ::readdir(directory))
    {
        if (entry->d_name[0] >= '0' && entry->d_name[0] <= '9') count++;
    }
    ::closedir(directory);
    return count;
}

std::vector<std::string> ProcessNames()
{
    std::vector<std::string> result;
    DIR* directory = ::opendir("/proc");
    if (directory == nullptr) return result;
    while (auto* entry = ::readdir(directory))
    {
        if (entry->d_name[0] < '0' || entry->d_name[0] > '9') continue;
        auto name = ReadTrim(std::string("/proc/") + entry->d_name + "/comm");
        if (!name.empty()) result.push_back(name);
    }
    ::closedir(directory);
    return result;
}

std::uint64_t UptimeSeconds()
{
    const auto text = ReadTrim("/proc/uptime");
    return static_cast<std::uint64_t>(std::strtod(text.c_str(), nullptr));
}

namespace {

std::vector<std::string> ListDir(const std::string& path)
{
    std::vector<std::string> names;
    DIR* directory = ::opendir(path.c_str());
    if (directory == nullptr) return names;
    while (auto* entry = ::readdir(directory))
    {
        const std::string name = entry->d_name;
        if (name == "." || name == "..") continue;
        names.push_back(name);
    }
    ::closedir(directory);
    std::sort(names.begin(), names.end());
    return names;
}

std::string CpuModel()
{
    std::istringstream stream(ReadFile("/proc/cpuinfo", 256 * 1024));
    std::string line;
    std::string model;
    std::string hardware;
    while (std::getline(stream, line))
    {
        const auto position = line.find(':');
        if (position == std::string::npos) continue;
        const auto key = Trim(line.substr(0, position));
        const auto value = Trim(line.substr(position + 1));
        if (value.empty()) continue;
        if (key == "model name" && model.empty()) model = value;
        else if (key == "Processor" && model.empty()) model = value;
        else if (key == "Hardware" && hardware.empty()) hardware = value;
    }
    return model.empty() ? hardware : model;
}

std::string DeviceTreeString(const std::string& path)
{
    auto value = ReadFile(path, 4096);
    const auto end = value.find('\0');
    if (end != std::string::npos) value.resize(end);
    return Trim(value);
}

std::string CLibVersion()
{
#if defined(__GLIBC__)
    const auto* version = ::gnu_get_libc_version();
    if (version != nullptr) return std::string("glibc ") + version;
#endif
    return {};
}

double ReadTemperatureFile(const std::string& path)
{
    const auto text = ReadTrim(path);
    if (text.empty()) return 0;
    const auto value = std::strtod(text.c_str(), nullptr);
    if (value <= 0) return 0;
    // 多数平台为毫摄氏度，少数直接给摄氏度
    const auto celsius = value > 1000 ? value / 1000.0 : value;
    // 保留一位小数，避免上报出 27.850000000000023 这类浮点尾数
    return celsius > 0 && celsius < 150 ? std::round(celsius * 10) / 10 : 0;
}

bool IsPhysicalDisk(const std::string& name)
{
    if (StartsWith(name, "loop") || StartsWith(name, "ram") || StartsWith(name, "dm-") ||
        StartsWith(name, "md") || StartsWith(name, "sr") || StartsWith(name, "fd"))
    {
        return false;
    }
    if (StartsWith(name, "nvme") || StartsWith(name, "mmcblk")) return name.find('p') == std::string::npos;
    if (StartsWith(name, "sd") || StartsWith(name, "vd") || StartsWith(name, "xvd") ||
        StartsWith(name, "hd") || StartsWith(name, "ublk"))
    {
        const auto last = name.back();
        return last < '0' || last > '9';
    }
    return false;
}

std::uint64_t g_disk_ios = 0;
std::uint64_t g_disk_ticks = 0;
std::int64_t g_disk_time_ms = 0;
bool g_has_disk_sample = false;

} // namespace

double Temperature()
{
    for (const auto& name : ListDir("/sys/class/thermal"))
    {
        if (!StartsWith(name, "thermal_zone")) continue;
        const auto value = ReadTemperatureFile("/sys/class/thermal/" + name + "/temp");
        if (value > 0) return value;
    }
    for (const auto& name : ListDir("/sys/class/hwmon"))
    {
        const auto base = "/sys/class/hwmon/" + name;
        for (const auto& file : ListDir(base))
        {
            if (!StartsWith(file, "temp") || !EndsWith(file, "_input")) continue;
            const auto value = ReadTemperatureFile(base + "/" + file);
            if (value > 0) return value;
        }
    }
    return 0;
}

double BatteryPercent()
{
    // /sys/class/power_supply/BAT*/capacity 为 0~100 的整数百分比
    for (const auto& name : ListDir("/sys/class/power_supply"))
    {
        if (!StartsWith(name, "BAT")) continue;
        const auto text = ReadTrim("/sys/class/power_supply/" + name + "/capacity");
        if (text.empty()) continue;
        const auto value = std::strtod(text.c_str(), nullptr);
        if (value > 0 && value <= 100) return value;
    }
    return 0;
}

int WirelessSignal()
{
    // /proc/net/wireless：链路质量通常以 70 为满值，-1 表示未连接
    std::istringstream stream(ReadFile("/proc/net/wireless"));
    std::string line;
    std::getline(stream, line);
    std::getline(stream, line);
    int best = 0;
    while (std::getline(stream, line))
    {
        const auto colon = line.find(':');
        if (colon == std::string::npos) continue;
        std::istringstream fields(line.substr(colon + 1));
        double status = 0;
        double link = 0;
        double level = 0;
        if (!(fields >> status >> link >> level)) continue;
        if (link <= 0) continue;
        // 不同驱动满值不同：传统 wireless-extensions 为 70，cfg80211 多为 100
        auto percent = link <= 70 ? static_cast<int>(link * 100.0 / 70.0 + 0.5) : static_cast<int>(link + 0.5);
        if (percent > 100) percent = 100;
        if (percent > best) best = percent;
    }
    return best;
}

DiskIoSample DiskIo()
{
    DiskIoSample result;
    std::istringstream stream(ReadFile("/proc/diskstats", 256 * 1024));
    std::string line;
    std::uint64_t ios = 0;
    std::uint64_t ticks = 0;
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::vector<std::string> tokens;
        std::string token;
        while (fields >> token) tokens.push_back(token);
        if (tokens.size() < 14) continue;
        if (!IsPhysicalDisk(tokens[2])) continue;
        ios += std::strtoull(tokens[3].c_str(), nullptr, 10);
        ios += std::strtoull(tokens[7].c_str(), nullptr, 10);
        ticks += std::strtoull(tokens[12].c_str(), nullptr, 10);
    }

    const auto now = SteadyMsNow();
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_has_disk_sample && now > g_disk_time_ms && ios >= g_disk_ios && ticks >= g_disk_ticks)
    {
        const auto elapsed = static_cast<double>(now - g_disk_time_ms);
        if (elapsed > 0)
        {
            result.Iops = static_cast<std::uint64_t>(static_cast<double>(ios - g_disk_ios) * 1000.0 / elapsed + 0.5);
            const auto active = static_cast<double>(ticks - g_disk_ticks) * 100.0 / elapsed;
            result.ActivePercent = std::min(100.0, std::max(0.0, active));
        }
    }
    g_disk_ios = ios;
    g_disk_ticks = ticks;
    g_disk_time_ms = now;
    g_has_disk_sample = true;
    return result;
}

std::uint64_t MaxOpenFiles()
{
    struct rlimit limit{};
    if (::getrlimit(RLIMIT_NOFILE, &limit) != 0) return 0;
    if (limit.rlim_cur == RLIM_INFINITY) return 0;
    return static_cast<std::uint64_t>(limit.rlim_cur);
}

ProbeResult PingProbe(const std::string& host, int timeout_ms, int count){
    ProbeResult result;
    if (host.empty() || count <= 0) return result;

    static const std::atomic<bool> never_cancelled{false};
    const auto timeout_sec = std::max(1, (timeout_ms + 999) / 1000);
    const auto command = "ping -c " + std::to_string(count) + " -W " + std::to_string(timeout_sec) + " " + host;
    const auto shell = RunShell(command, (count + 2) * 1000 + 2000, never_cancelled);
    if (shell.Output.empty()) return result;

    // 统计 "time=12.3 ms" 出现次数，兼容 busybox 与 iputils 输出
    int success = 0;
    double total = 0;
    std::size_t position = 0;
    while ((position = shell.Output.find("time=", position)) != std::string::npos)
    {
        position += 5;
        auto end = position;
        while (end < shell.Output.size() &&
               (std::isdigit(static_cast<unsigned char>(shell.Output[end])) || shell.Output[end] == '.'))
        {
            end++;
        }
        if (end > position)
        {
            success++;
            total += std::strtod(shell.Output.substr(position, end - position).c_str(), nullptr);
        }
    }

    result.Valid = true;
    if (success == 0)
    {
        result.LossRate = 1;
        return result;
    }
    result.Latency = static_cast<int>(std::lround(total / success));
    result.LossRate = std::round((1.0 - static_cast<double>(success) / count) * 1000.0) / 1000.0;
    result.Score = ComputePingScore(result.Latency, success, count);
    return result;
}

bool PowerAction(bool reboot)
{
    // 依赖系统 init 执行真正的重启/关机，需要有权限（通常为 root）
    static const std::atomic<bool> never_cancelled{false};
    const auto shell = RunShell(reboot ? "reboot" : "poweroff", 5000, never_cancelled);
    return shell.ExitCode == 0;
}

namespace {

void CountTcpFile(const char* path, TcpCounts& result)
{
    std::istringstream stream(ReadFile(path, 256 * 1024));
    std::string line;
    std::getline(stream, line); // 表头
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string sl, local, remote, state;
        if (!(fields >> sl >> local >> remote >> state)) continue;

        // st 为十六进制：01 ESTABLISHED、06 TIME_WAIT、08 CLOSE_WAIT
        const auto value = static_cast<unsigned>(std::strtoul(state.c_str(), nullptr, 16));
        switch (value)
        {
        case 0x01: result.Established++; break;
        case 0x06: result.TimeWait++; break;
        case 0x08: result.CloseWait++; break;
        default: break;
        }
    }
}

} // namespace

TcpCounts TcpStates()
{
    TcpCounts result;
    CountTcpFile("/proc/net/tcp", result);
    CountTcpFile("/proc/net/tcp6", result);
    return result;
}

double LoadAverage()
{
    const auto text = ReadTrim("/proc/loadavg");
    return std::strtod(text.c_str(), nullptr);
}

HardwareInfo Hardware()
{
    HardwareInfo result;
    // 板级标识优先取 /etc/os-release 中设备端写入的字段（与 MachineInfo 约定一致），
    // 缺失时依次回落 DMI 与设备树，避免 ARM 板只报内核设备名而丢失厂商/整机型号。
    result.Vendor = CleanId(ReadOsRelease("Vendor"));
    if (result.Vendor.empty()) result.Vendor = CleanId(ReadTrim("/sys/class/dmi/id/sys_vendor"));
    if (result.Vendor.empty()) result.Vendor = CleanId(DeviceTreeString("/proc/device-tree/vendor"));

    result.Product = CleanId(ReadOsRelease("Product"));
    if (result.Product.empty()) result.Product = CleanId(DeviceTreeString("/proc/device-tree/model"));
    if (result.Product.empty()) result.Product = CleanId(ReadTrim("/sys/class/dmi/id/product_name"));
    if (result.Product.empty()) result.Product = CleanId(ReadTrim("/sys/class/dmi/id/board_name"));

    result.SerialNumber = CleanId(ReadOsRelease("Serial"));
    if (result.SerialNumber.empty()) result.SerialNumber = CleanId(ReadTrim("/sys/class/dmi/id/product_serial"));

    result.Board = CleanId(ReadOsRelease("Board"));
    if (result.Board.empty()) result.Board = CleanId(ReadTrim("/sys/class/dmi/id/board_serial"));
    if (result.Board.empty()) result.Board = CleanId(ReadTrim("/sys/class/dmi/id/board_name"));
    if (result.Board.empty()) result.Board = CleanId(DeviceTreeString("/proc/device-tree/model"));

    result.UUID = CleanId(ReadTrim("/etc/uuid"));
    if (result.UUID.empty()) result.UUID = CleanId(ReadTrim("/sys/class/dmi/id/product_uuid"));
    result.MachineGuid = CleanId(ReadTrim("/etc/machine-id"));
    if (result.MachineGuid.empty()) result.MachineGuid = CleanId(ReadTrim("/var/lib/dbus/machine-id"));
    result.DiskID = CleanId(LinuxDiskSerial());

    // 处理器：x86 取 model name，ARM 板取设备树 model，均缺失时退回整机型号
    result.Processor = CpuModel();
    if (result.Processor.empty()) result.Processor = CleanId(DeviceTreeString("/proc/device-tree/model"));
    if (result.Processor.empty()) result.Processor = result.Product;
    result.CLibVersion = CLibVersion();

    std::set<std::string> macs;
    struct ifaddrs* addresses = nullptr;
    if (::getifaddrs(&addresses) == 0)
    {
        for (auto* item = addresses; item != nullptr; item = item->ifa_next)
        {
            if (item->ifa_addr == nullptr || item->ifa_addr->sa_family != AF_PACKET) continue;
            if (item->ifa_name == nullptr || IsVirtualInterface(item->ifa_name)) continue;
            const auto* link = reinterpret_cast<const sockaddr_ll*>(item->ifa_addr);
            if (link->sll_halen != 6) continue;
            const auto mac = FormatMac(link->sll_addr, link->sll_halen);
            if (!mac.empty() && mac != "00-00-00-00-00-00") macs.insert(mac);
        }
        ::freeifaddrs(addresses);
    }
    result.Macs = Join(std::vector<std::string>(macs.begin(), macs.end()), ",");
    return result;
}

SystemInfo System()
{
    SystemInfo result;
    result.OSName = ReadOsRelease("NAME");
    const auto pretty = ReadOsRelease("PRETTY_NAME");
    if (!pretty.empty()) result.OSName = pretty;
    result.OSVersion = ReadOsRelease("VERSION_ID");

    struct utsname name{};
    if (::uname(&name) == 0)
    {
        if (result.OSName.empty()) result.OSName = name.sysname;
        if (result.OSVersion.empty()) result.OSVersion = name.release;
        result.HostName = name.nodename;
    }

    char hostname[256] = {};
    if (::gethostname(hostname, sizeof(hostname)) == 0) result.HostName = hostname;

    if (const auto* user = ::getpwuid(::geteuid())) result.UserName = user->pw_name;
    if (result.UserName.empty()) result.UserName = staragent::EnvironmentValue({"USER"});
    result.TimeZone = TimeZoneName();
    return result;
}

NetworkInfo Network()
{
    NetworkInfo result;
    struct ifaddrs* addresses = nullptr;
    if (::getifaddrs(&addresses) == 0)
    {
        for (auto* item = addresses; item != nullptr; item = item->ifa_next)
        {
            if (item->ifa_addr == nullptr || item->ifa_addr->sa_family != AF_INET) continue;
            if (item->ifa_name == nullptr || StartsWith(item->ifa_name, "lo")) continue;
            if ((item->ifa_flags & IFF_UP) == 0) continue;
            const auto address = SockaddrToIp(item->ifa_addr);
            if (address.empty() || StartsWith(address, "127.") || StartsWith(address, "169.254.")) continue;
            result.IP = address;
            break;
        }
        ::freeifaddrs(addresses);
    }
    result.Gateway = LinuxGateway();
    result.Dns = LinuxDns();
    if (result.Dns.size() > 50) result.Dns.resize(50);
    return result;
}

std::string LocalIP() { return Network().IP; }
std::string Gateway() { return LinuxGateway(); }
std::string Dns() { return LinuxDns(); }

std::string Architecture()
{
#if defined(__x86_64__) || defined(__amd64__)
    return "X64";
#elif defined(__i386__)
    return "X86";
#elif defined(__aarch64__)
    return "Arm64";
#elif defined(__arm__)
    return "Arm";
#elif defined(__riscv) && __riscv_xlen == 64
    return "RiscV64";
#elif defined(__loongarch64)
    return "LA64";
#elif defined(__mips64)
    return "Mips64";
#else
    return "Unknown";
#endif
}

std::string ExecutablePath()
{
    char buffer[4096] = {};
    const auto length = ::readlink("/proc/self/exe", buffer, sizeof(buffer) - 1);
    if (length <= 0) return {};
    return std::string(buffer, static_cast<std::size_t>(length));
}

std::string DefaultDataDir()
{
    if (IsRoot()) return "/var/lib/staragent";
    const auto home = HomeDir();
    return home.empty() ? "data" : home + "/.staragent";
}

std::string TimeZoneName()
{
    auto timezone = ReadTrim("/etc/timezone");
    if (!timezone.empty()) return timezone;
    const auto env = staragent::EnvironmentValue({"TZ"});
    return env;
}

} // namespace staragent::platform
