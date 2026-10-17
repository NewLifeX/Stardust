// SPDX-License-Identifier: MIT
#include "platform.hpp"

#include "output_buffer.hpp"

#include <algorithm>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <iterator>
#include <map>
#include <mutex>
#include <set>
#include <thread>

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0601
#endif
#include <winsock2.h>
#include <ws2ipdef.h>
#include <windows.h>

#include <io.h>
#include <iphlpapi.h>
#include <iptypes.h>
#include <netioapi.h>
#include <pdh.h>
#include <pdhmsg.h>
#include <tlhelp32.h>
#include <winioctl.h>
#include <ws2tcpip.h>

namespace staragent::platform {
namespace {

std::mutex g_mutex;
std::uint64_t g_prev_idle = 0;
std::uint64_t g_prev_kernel = 0;
std::uint64_t g_prev_user = 0;
bool g_has_cpu_sample = false;

PDH_HQUERY g_load_query = nullptr;
PDH_HCOUNTER g_load_counter = nullptr;
bool g_load_initialized = false;

std::string Utf8FromWide(const std::wstring& value)
{
    if (value.empty()) return {};
    const int size = ::WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (size <= 0) return {};
    std::string result(static_cast<std::size_t>(size - 1), '\0');
    ::WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, result.data(), size, nullptr, nullptr);
    return result;
}

std::wstring WideFromUtf8(const std::string& value)
{
    if (value.empty()) return {};
    const int size = ::MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, nullptr, 0);
    if (size <= 0) return {};
    std::wstring result(static_cast<std::size_t>(size - 1), L'\0');
    ::MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, result.data(), size);
    return result;
}

std::string GetEnv(const wchar_t* name)
{
    const DWORD size = ::GetEnvironmentVariableW(name, nullptr, 0);
    if (size == 0) return {};
    std::wstring value(size, L'\0');
    const DWORD written = ::GetEnvironmentVariableW(name, value.data(), size);
    if (written == 0 || written >= size) return {};
    value.resize(written);
    return Utf8FromWide(value);
}

std::string ReadRegistryString(HKEY root, const wchar_t* subkey, const wchar_t* name)
{
    HKEY key = nullptr;
    if (::RegOpenKeyExW(root, subkey, 0, KEY_READ | KEY_WOW64_64KEY, &key) != ERROR_SUCCESS) return {};

    DWORD type = 0;
    DWORD size = 0;
    if (::RegQueryValueExW(key, name, nullptr, &type, nullptr, &size) != ERROR_SUCCESS ||
        (type != REG_SZ && type != REG_EXPAND_SZ) || size < sizeof(wchar_t))
    {
        ::RegCloseKey(key);
        return {};
    }

    std::wstring buffer(size / sizeof(wchar_t), L'\0');
    const auto status = ::RegQueryValueExW(key, name, nullptr, &type,
                                           reinterpret_cast<LPBYTE>(buffer.data()), &size);
    ::RegCloseKey(key);
    if (status != ERROR_SUCCESS) return {};
    while (!buffer.empty() && buffer.back() == L'\0') buffer.pop_back();
    return Utf8FromWide(buffer);
}

DWORD ReadRegistryDword(HKEY root, const wchar_t* subkey, const wchar_t* name)
{
    HKEY key = nullptr;
    if (::RegOpenKeyExW(root, subkey, 0, KEY_READ | KEY_WOW64_64KEY, &key) != ERROR_SUCCESS) return 0;

    DWORD type = 0;
    DWORD value = 0;
    DWORD size = sizeof(value);
    const auto status = ::RegQueryValueExW(key, name, nullptr, &type, reinterpret_cast<LPBYTE>(&value), &size);
    ::RegCloseKey(key);

    return status == ERROR_SUCCESS && type == REG_DWORD ? value : 0;
}

ULONGLONG ReadRegistryQword(HKEY root, const wchar_t* subkey, const wchar_t* name)
{
    HKEY key = nullptr;
    if (::RegOpenKeyExW(root, subkey, 0, KEY_READ | KEY_WOW64_64KEY, &key) != ERROR_SUCCESS) return 0;

    DWORD type = 0;
    ULONGLONG value = 0;
    DWORD size = sizeof(value);
    const auto status = ::RegQueryValueExW(key, name, nullptr, &type, reinterpret_cast<LPBYTE>(&value), &size);
    ::RegCloseKey(key);

    return status == ERROR_SUCCESS && type == REG_QWORD ? value : 0;
}

std::string ReadSmbiosUuid()
{
    const DWORD size = ::GetSystemFirmwareTable('RSMB', 0, nullptr, 0);
    if (size < 8) return {};

    std::vector<BYTE> buffer(size);
    if (::GetSystemFirmwareTable('RSMB', 0, buffer.data(), size) != size) return {};

    DWORD length = 0;
    std::memcpy(&length, buffer.data() + 4, sizeof(length));
    if (length < 4 || length > size - 8) return {};
    const BYTE* table = buffer.data() + 8;

    std::size_t offset = 0;
    while (offset + 4 <= length)
    {
        const BYTE type = table[offset];
        const BYTE struct_length = table[offset + 1];
        if (struct_length < 4) break;

        if (type == 1 && struct_length >= 24 && offset + 24 <= length)
        {
            const BYTE* uuid = table + offset + 8;
            const std::uint32_t d1 = static_cast<std::uint32_t>(uuid[0]) |
                                     (static_cast<std::uint32_t>(uuid[1]) << 8) |
                                     (static_cast<std::uint32_t>(uuid[2]) << 16) |
                                     (static_cast<std::uint32_t>(uuid[3]) << 24);
            const std::uint32_t d2 = static_cast<std::uint32_t>(uuid[4]) |
                                     (static_cast<std::uint32_t>(uuid[5]) << 8);
            const std::uint32_t d3 = static_cast<std::uint32_t>(uuid[6]) |
                                     (static_cast<std::uint32_t>(uuid[7]) << 8);
            char text[64];
            std::snprintf(text, sizeof(text),
                          "%08X-%04X-%04X-%02X%02X-%02X%02X%02X%02X%02X%02X", d1, d2, d3, uuid[8], uuid[9],
                          uuid[10], uuid[11], uuid[12], uuid[13], uuid[14], uuid[15]);
            return text;
        }

        std::size_t next = offset + struct_length;
        if (next + 1 < length && table[next] == 0 && table[next + 1] == 0)
        {
            next += 2;
        }
        else
        {
            while (next + 1 < length && !(table[next] == 0 && table[next + 1] == 0)) next++;
            next += 2;
        }
        if (next <= offset) break;
        offset = next;
    }

    return {};
}

std::string ReadDiskSerial(int index)
{
    wchar_t path[64] = {};
    std::swprintf(path, std::size(path), L"\\\\.\\PhysicalDrive%d", index);
    HANDLE handle = ::CreateFileW(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr,
                                  OPEN_EXISTING, 0, nullptr);
    if (handle == INVALID_HANDLE_VALUE)
    {
        handle = ::CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                               nullptr, OPEN_EXISTING, 0, nullptr);
    }
    if (handle == INVALID_HANDLE_VALUE) return {};

    STORAGE_PROPERTY_QUERY query{};
    query.PropertyId = StorageDeviceProperty;
    query.QueryType = PropertyStandardQuery;
    STORAGE_DESCRIPTOR_HEADER header{};
    DWORD returned = 0;
    if (!::DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY, &query, sizeof(query), &header, sizeof(header),
                           &returned, nullptr) ||
        header.Size < sizeof(STORAGE_DEVICE_DESCRIPTOR))
    {
        ::CloseHandle(handle);
        return {};
    }

    std::vector<BYTE> buffer(header.Size);
    if (!::DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY, &query, sizeof(query), buffer.data(),
                           static_cast<DWORD>(buffer.size()), &returned, nullptr))
    {
        ::CloseHandle(handle);
        return {};
    }
    ::CloseHandle(handle);

    const auto* descriptor = reinterpret_cast<const STORAGE_DEVICE_DESCRIPTOR*>(buffer.data());
    if (descriptor->RemovableMedia) return {};
    if (descriptor->SerialNumberOffset == 0 || descriptor->SerialNumberOffset >= buffer.size()) return {};
    const char* text = reinterpret_cast<const char*>(buffer.data() + descriptor->SerialNumberOffset);
    return Trim(text);
}

std::string ReadDiskSerials()
{
    std::set<std::string> serials;
    for (int index = 0; index < 32; index++)
    {
        const auto serial = ReadDiskSerial(index);
        if (!serial.empty()) serials.insert(serial);
    }

    return Join(std::vector<std::string>(serials.begin(), serials.end()), ",");
}

template <typename Callback>
void ForEachAdapter(Callback&& callback)
{
    ULONG size = 16 * 1024;
    std::vector<BYTE> buffer;
    for (int attempt = 0; attempt < 4; attempt++)
    {
        buffer.resize(size);
        const ULONG flags = GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST | GAA_FLAG_INCLUDE_GATEWAYS;
        const ULONG result = ::GetAdaptersAddresses(AF_UNSPEC, flags, nullptr,
                                                    reinterpret_cast<IP_ADAPTER_ADDRESSES*>(buffer.data()), &size);
        if (result == ERROR_SUCCESS)
        {
            // The adapter rows contain pointers into buffer, so the callback
            // must run while the buffer is still alive.
            auto* current = reinterpret_cast<IP_ADAPTER_ADDRESSES*>(buffer.data());
            while (current != nullptr)
            {
                callback(*current);
                current = current->Next;
            }
            return;
        }
        if (result != ERROR_BUFFER_OVERFLOW) break;
        size += 4096;
    }
}

bool HasIPv4(const IP_ADAPTER_ADDRESSES& adapter)
{
    for (auto* unicast = adapter.FirstUnicastAddress; unicast != nullptr; unicast = unicast->Next)
    {
        if (unicast->Address.lpSockaddr == nullptr || unicast->Address.lpSockaddr->sa_family != AF_INET) continue;
        const auto* ipv4 = reinterpret_cast<const sockaddr_in*>(unicast->Address.lpSockaddr);
        const auto host = ::ntohl(ipv4->sin_addr.s_addr);
        if ((host >> 24) == 127 || (host >> 16) == 0xA9FE) continue;
        return true;
    }

    return false;
}

bool IsVirtualAdapter(const IP_ADAPTER_ADDRESSES& adapter)
{
    const auto description = ToLower(Utf8FromWide(adapter.Description ? adapter.Description : L""));
    const auto name = ToLower(Utf8FromWide(adapter.FriendlyName ? adapter.FriendlyName : L""));
    const char* words[] = {"virtual", "vmware", "hyper-v", "vbox", "loopback", "tap", "tun", "vpn",
                           "docker",  "wsl",    "bluetooth", "pseudo", "teredo", "isatap"};
    for (const char* word : words)
    {
        if (description.find(word) != std::string::npos || name.find(word) != std::string::npos) return true;
    }
    return false;
}

std::string SockaddrToString(const sockaddr* address)
{
    if (address == nullptr) return {};
    char text[INET6_ADDRSTRLEN] = {};
    if (address->sa_family == AF_INET)
    {
        const auto* ipv4 = reinterpret_cast<const sockaddr_in*>(address);
        if (::InetNtopA(AF_INET, const_cast<IN_ADDR*>(&ipv4->sin_addr), text, sizeof(text)) == nullptr) return {};
        return text;
    }
    if (address->sa_family == AF_INET6)
    {
        const auto* ipv6 = reinterpret_cast<const sockaddr_in6*>(address);
        if (::InetNtopA(AF_INET6, const_cast<IN6_ADDR*>(&ipv6->sin6_addr), text, sizeof(text)) == nullptr) return {};
        return text;
    }
    return {};
}

std::string FormatMac(const BYTE* data, ULONG length)
{
    if (length == 0) return {};
    std::string result;
    char item[4];
    for (ULONG i = 0; i < length; i++)
    {
        std::snprintf(item, sizeof(item), "%02X", data[i]);
        if (i > 0) result += '-';
        result += item;
    }
    return result;
}

std::string ReadWmicValue(const std::string& arguments)
{
    std::atomic<bool> cancelled{false};
    const auto result = RunShell("wmic " + arguments, 8000, cancelled);
    for (const auto& line : Split(result.Output, '\n'))
    {
        const auto index = line.find('=');
        if (index != std::string::npos)
        {
            const auto value = Trim(line.substr(index + 1));
            if (!value.empty()) return value;
        }
    }

    return {};
}

std::string ReadDpi()
{
    using GetDpiForSystemFn = UINT(WINAPI*)();
    auto user32 = ::GetModuleHandleW(L"user32.dll");
    if (user32 == nullptr) user32 = ::LoadLibraryW(L"user32.dll");
    const auto get_dpi = user32 == nullptr
                             ? nullptr
                             : reinterpret_cast<GetDpiForSystemFn>(::GetProcAddress(user32, "GetDpiForSystem"));
    const auto dpi = get_dpi == nullptr ? 96u : get_dpi();
    return std::to_string(dpi) + "*" + std::to_string(dpi);
}

std::string ReadResolution()
{
    const auto width = ::GetSystemMetrics(SM_CXSCREEN);
    const auto height = ::GetSystemMetrics(SM_CYSCREEN);
    if (width <= 0 || height <= 0) return {};
    return std::to_string(width) + "*" + std::to_string(height);
}

std::string ReadVCRuntimeVersions()
{
    std::set<std::string> versions;
    const wchar_t* bases[] = {
        L"SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes",
        L"SOFTWARE\\WOW6432Node\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes",
    };
    for (const auto* base : bases)
    {
        HKEY base_key = nullptr;
        if (::RegOpenKeyExW(HKEY_LOCAL_MACHINE, base, 0, KEY_READ | KEY_WOW64_64KEY, &base_key) != ERROR_SUCCESS)
        {
            continue;
        }
        for (DWORD index = 0;; index++)
        {
            wchar_t name[128] = {};
            DWORD name_size = static_cast<DWORD>(std::size(name));
            if (::RegEnumKeyExW(base_key, index, name, &name_size, nullptr, nullptr, nullptr, nullptr) !=
                ERROR_SUCCESS)
            {
                break;
            }
            std::wstring subkey = base;
            subkey += L"\\";
            subkey += name;
            const auto major = ReadRegistryDword(HKEY_LOCAL_MACHINE, subkey.c_str(), L"Major");
            if (major == 0) continue;
            const auto minor = ReadRegistryDword(HKEY_LOCAL_MACHINE, subkey.c_str(), L"Minor");
            const auto year = major >= 14 ? std::string("VC2015-2022")
                              : major == 12 ? std::string("VC2013")
                              : major == 11 ? std::string("VC2012")
                              : major == 10 ? std::string("VC2010")
                              : major == 9  ? std::string("VC2008")
                                            : "VC" + std::to_string(major);
            auto text = year + " " + std::to_string(major);
            if (minor > 0) text += "." + std::to_string(minor);
            versions.insert(text);
        }
        ::RegCloseKey(base_key);
    }

    for (const auto* path : {L"SOFTWARE\\Microsoft\\VisualStudio\\10.0\\VC\\VCRedist\\x64",
                             L"SOFTWARE\\WOW6432Node\\Microsoft\\VisualStudio\\10.0\\VC\\VCRedist\\x64"})
    {
        HKEY key = nullptr;
        if (::RegOpenKeyExW(HKEY_LOCAL_MACHINE, path, 0, KEY_READ | KEY_WOW64_64KEY, &key) == ERROR_SUCCESS)
        {
            versions.insert("VC2010 10.0");
            ::RegCloseKey(key);
        }
    }

    return Join(std::vector<std::string>(versions.rbegin(), versions.rend()), ";");
}

std::string ReadGpuInfo()
{
    constexpr auto class_key = L"SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}";
    HKEY key = nullptr;
    if (::RegOpenKeyExW(HKEY_LOCAL_MACHINE, class_key, 0, KEY_READ | KEY_WOW64_64KEY, &key) != ERROR_SUCCESS) return {};

    std::vector<std::string> gpus;
    for (DWORD index = 0;; index++)
    {
        wchar_t name[256] = {};
        DWORD name_size = static_cast<DWORD>(std::size(name));
        if (::RegEnumKeyExW(key, index, name, &name_size, nullptr, nullptr, nullptr, nullptr) != ERROR_SUCCESS) break;

        std::wstring subkey = class_key;
        subkey += L"\\";
        subkey += name;
        const auto description = ReadRegistryString(HKEY_LOCAL_MACHINE, subkey.c_str(), L"DriverDesc");
        if (description.empty() || description.find("Microsoft Basic") != std::string::npos) continue;

        const auto memory = ReadRegistryQword(HKEY_LOCAL_MACHINE, subkey.c_str(),
                                              L"HardwareInformation.qwMemorySize");
        if (memory < 1024ull * 1024 * 1024) continue;

        auto text = description;
        text += "(";
        text += std::to_string((memory + 512ull * 1024 * 1024) / (1024ull * 1024 * 1024));
        text += "G)";
        if (std::find(gpus.begin(), gpus.end(), text) == gpus.end()) gpus.push_back(text);
    }
    ::RegCloseKey(key);

    return Join(gpus, ",");
}

} // namespace

double CpuPercent()
{
    FILETIME idle_time{}, kernel_time{}, user_time{};
    if (!::GetSystemTimes(&idle_time, &kernel_time, &user_time)) return 0;

    auto to_uint64 = [](const FILETIME& value) {
        return (static_cast<std::uint64_t>(value.dwHighDateTime) << 32) | value.dwLowDateTime;
    };
    const auto idle = to_uint64(idle_time);
    const auto kernel = to_uint64(kernel_time);
    const auto user = to_uint64(user_time);

    std::lock_guard<std::mutex> lock(g_mutex);
    if (!g_has_cpu_sample)
    {
        g_prev_idle = idle;
        g_prev_kernel = kernel;
        g_prev_user = user;
        g_has_cpu_sample = true;
        return 0;
    }

    const auto idle_delta = idle - g_prev_idle;
    const auto kernel_delta = kernel - g_prev_kernel;
    const auto user_delta = user - g_prev_user;
    g_prev_idle = idle;
    g_prev_kernel = kernel;
    g_prev_user = user;

    const auto total = kernel_delta + user_delta;
    if (total == 0) return 0;
    const auto busy = total > idle_delta ? total - idle_delta : 0;
    const double rate = 100.0 * static_cast<double>(busy) / static_cast<double>(total);
    return std::max(0.0, std::min(100.0, rate));
}

MemoryInfo Memory()
{
    MEMORYSTATUSEX status{};
    status.dwLength = sizeof(status);
    if (!::GlobalMemoryStatusEx(&status)) return {};
    MemoryInfo result;
    result.Total = status.ullTotalPhys;
    result.Available = status.ullAvailPhys;
    result.Free = status.ullAvailPhys;
    return result;
}

DiskInfo DiskUsage(const std::string& path)
{
    const auto wide = WideFromUtf8(path);
    ULARGE_INTEGER free_bytes{}, total_bytes{}, total_free{};
    if (!::GetDiskFreeSpaceExW(wide.c_str(), &free_bytes, &total_bytes, &total_free)) return {};
    DiskInfo result;
    result.Total = total_bytes.QuadPart;
    result.Free = free_bytes.QuadPart;
    return result;
}

std::vector<DriveEntry> Drives()
{
    std::vector<DriveEntry> result;
    const DWORD length = ::GetLogicalDriveStringsW(0, nullptr);
    if (length == 0) return result;
    std::wstring buffer(length, L'\0');
    if (::GetLogicalDriveStringsW(length, buffer.data()) == 0) return result;

    for (std::size_t offset = 0; offset < buffer.size() && buffer[offset] != L'\0';)
    {
        const std::wstring root = &buffer[offset];
        offset += root.size() + 1;

        const UINT type = ::GetDriveTypeW(root.c_str());
        if (type == DRIVE_NO_ROOT_DIR || type == DRIVE_UNKNOWN || type == DRIVE_CDROM) continue;

        ULARGE_INTEGER free_bytes{}, total_bytes{}, total_free{};
        if (!::GetDiskFreeSpaceExW(root.c_str(), &free_bytes, &total_bytes, &total_free)) continue;
        if (total_bytes.QuadPart == 0) continue;

        wchar_t file_system[MAX_PATH + 1] = {};
        ::GetVolumeInformationW(root.c_str(), nullptr, 0, nullptr, nullptr, nullptr, file_system, MAX_PATH);

        DriveEntry entry;
        entry.Path = Utf8FromWide(root);
        entry.FileSystem = Utf8FromWide(file_system);
        entry.Total = total_bytes.QuadPart;
        entry.Free = free_bytes.QuadPart;
        result.push_back(std::move(entry));
    }
    return result;
}

NetTotals NetTotalsSinceStart()
{
    PMIB_IF_TABLE2 table = nullptr;
    if (::GetIfTable2(&table) != NO_ERROR || table == nullptr) return {};
    NetTotals result;
    for (ULONG i = 0; i < table->NumEntries; i++)
    {
        const auto& row = table->Table[i];
        if (row.Type == IF_TYPE_SOFTWARE_LOOPBACK || row.Type == IF_TYPE_TUNNEL) continue;
        if (row.OperStatus != IfOperStatusUp) continue;
        result.Sent += row.OutOctets;
        result.Received += row.InOctets;
        result.Valid = true;
    }
    ::FreeMibTable(table);
    return result;
}

std::uint64_t ProcessCount()
{
    HANDLE snapshot = ::CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) return 0;
    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(entry);
    std::uint64_t count = 0;
    if (::Process32FirstW(snapshot, &entry))
    {
        do
        {
            count++;
        } while (::Process32NextW(snapshot, &entry));
    }
    ::CloseHandle(snapshot);
    return count;
}

std::vector<std::string> ProcessNames()
{
    std::vector<std::string> result;
    HANDLE snapshot = ::CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) return result;
    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(entry);
    if (::Process32FirstW(snapshot, &entry))
    {
        do
        {
            result.push_back(Utf8FromWide(entry.szExeFile));
        } while (::Process32NextW(snapshot, &entry));
    }
    ::CloseHandle(snapshot);
    return result;
}

std::uint64_t UptimeSeconds() { return ::GetTickCount64() / 1000; }

double LoadAverage()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    if (!g_load_initialized)
    {
        g_load_initialized = true;
        if (::PdhOpenQueryW(nullptr, 0, &g_load_query) == ERROR_SUCCESS)
        {
            if (::PdhAddEnglishCounterW(g_load_query, L"\\System\\Processor Queue Length", 0, &g_load_counter) !=
                ERROR_SUCCESS)
            {
                ::PdhCloseQuery(g_load_query);
                g_load_query = nullptr;
                g_load_counter = nullptr;
            }
            else
            {
                ::PdhCollectQueryData(g_load_query);
            }
        }
    }
    if (g_load_query == nullptr || g_load_counter == nullptr) return 0;

    if (::PdhCollectQueryData(g_load_query) != ERROR_SUCCESS) return 0;
    PDH_FMT_COUNTERVALUE value{};
    if (::PdhGetFormattedCounterValue(g_load_counter, PDH_FMT_DOUBLE, nullptr, &value) != ERROR_SUCCESS) return 0;
    if (value.CStatus != PDH_CSTATUS_VALID_DATA && value.CStatus != PDH_CSTATUS_NEW_DATA) return 0;
    return value.doubleValue;
}

HardwareInfo Hardware()
{
    HardwareInfo result;
    result.UUID = ReadSmbiosUuid();
    result.MachineGuid = ReadRegistryString(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\Cryptography",
                                            L"MachineGuid");
    result.SerialNumber = ReadRegistryString(HKEY_LOCAL_MACHINE, L"HARDWARE\\DESCRIPTION\\System\\BIOS",
                                             L"SystemSerialNumber");
    result.Board = ReadRegistryString(HKEY_LOCAL_MACHINE, L"HARDWARE\\DESCRIPTION\\System\\BIOS",
                                      L"BaseBoardSerialNumber");
    result.Product = ReadRegistryString(HKEY_LOCAL_MACHINE, L"HARDWARE\\DESCRIPTION\\System\\BIOS",
                                       L"BaseBoardProduct");
    result.Vendor = ReadRegistryString(HKEY_LOCAL_MACHINE, L"HARDWARE\\DESCRIPTION\\System\\BIOS",
                                      L"SystemManufacturer");
    result.Processor = ReadRegistryString(HKEY_LOCAL_MACHINE,
                                         L"HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0",
                                         L"ProcessorNameString");
    if (result.Board.empty()) result.Board = ReadWmicValue("baseboard get serialnumber /value");
    result.GPU = ReadGpuInfo();
    result.DiskID = ReadDiskSerials();
    result.Dpi = ReadDpi();
    result.Resolution = ReadResolution();
    result.CLibVersion = ReadVCRuntimeVersions();

    std::set<std::string> macs;
    ForEachAdapter([&](const IP_ADAPTER_ADDRESSES& adapter) {
        if (adapter.IfType == IF_TYPE_SOFTWARE_LOOPBACK || adapter.PhysicalAddressLength == 0) return;
        if (adapter.OperStatus != IfOperStatusUp || !HasIPv4(adapter)) return;
        if (IsVirtualAdapter(adapter)) return;
        const auto mac = FormatMac(adapter.PhysicalAddress, adapter.PhysicalAddressLength);
        if (!mac.empty() && mac != "00-00-00-00-00-00") macs.insert(mac);
    });
    result.Macs = Join(std::vector<std::string>(macs.begin(), macs.end()), ",");

    static const std::set<std::string> invalid = {"", "none", "unknown", "not specified", "not settable",
                                                  "to be filled by o.e.m.", "default string",
                                                  "system serial number", "0", "00000000-0000-0000-0000-000000000000",
                                                  "ffffffff-ffff-ffff-ffff-ffffffffffff"};
    auto clean = [](std::string value) {
        value = Trim(value);
        if (invalid.count(ToLower(value)) > 0) return std::string();
        return value;
    };
    result.UUID = clean(result.UUID);
    result.MachineGuid = clean(result.MachineGuid);
    result.SerialNumber = clean(result.SerialNumber);
    result.Board = clean(result.Board);
    result.DiskID = clean(result.DiskID);
    result.Product = clean(result.Product);
    result.Vendor = clean(result.Vendor);
    result.Processor = clean(result.Processor);
    result.GPU = clean(result.GPU);
    result.Dpi = clean(result.Dpi);
    result.Resolution = clean(result.Resolution);
    result.CLibVersion = clean(result.CLibVersion);
    return result;
}

SystemInfo System()
{
    SystemInfo result;
    result.OSName = ReadRegistryString(HKEY_LOCAL_MACHINE,
                                       L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", L"ProductName");
    if (StartsWith(result.OSName, "Microsoft ")) result.OSName = result.OSName.substr(10);

    const auto major = ReadRegistryDword(HKEY_LOCAL_MACHINE,
                                         L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion",
                                         L"CurrentMajorVersionNumber");
    const auto minor = ReadRegistryDword(HKEY_LOCAL_MACHINE,
                                         L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion",
                                         L"CurrentMinorVersionNumber");
    const auto build = ReadRegistryString(HKEY_LOCAL_MACHINE,
                                          L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion",
                                          L"CurrentBuildNumber");
    if (major > 0 && !build.empty())
    {
        result.OSVersion = std::to_string(major) + "." + std::to_string(minor) + "." + build;
    }
    else
    {
        const auto current = ReadRegistryString(HKEY_LOCAL_MACHINE,
                                                L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion",
                                                L"CurrentVersion");
        if (!current.empty() && !build.empty()) result.OSVersion = current + "." + build;
    }
    if (result.OSName.empty()) result.OSName = "Windows";

    wchar_t buffer[256] = {};
    DWORD size = static_cast<DWORD>(std::size(buffer));
    if (::GetComputerNameW(buffer, &size)) result.HostName = Utf8FromWide(buffer);
    size = static_cast<DWORD>(std::size(buffer));
    if (::GetUserNameW(buffer, &size)) result.UserName = Utf8FromWide(buffer);

    TIME_ZONE_INFORMATION zone{};
    const auto state = ::GetTimeZoneInformation(&zone);
    auto bias = zone.Bias;
    if (state == TIME_ZONE_ID_DAYLIGHT) bias += zone.DaylightBias;
    else if (state == TIME_ZONE_ID_STANDARD) bias += zone.StandardBias;
    const auto offset = -bias;
    const auto sign = offset >= 0 ? '+' : '-';
    const auto minutes = offset >= 0 ? offset : -offset;
    char text[16] = {};
    std::snprintf(text, sizeof(text), "GMT%c%02d:%02d", sign, minutes / 60, minutes % 60);
    result.TimeZone = text;
    return result;
}

NetworkInfo Network()
{
    NetworkInfo result;
    std::set<std::string> dns;
    std::set<std::string> seen_ips;
    std::vector<std::string> ips;
    ForEachAdapter([&](const IP_ADAPTER_ADDRESSES& adapter) {
        if (adapter.IfType == IF_TYPE_SOFTWARE_LOOPBACK) return;

        if (adapter.OperStatus == IfOperStatusUp)
        {
            for (auto* unicast = adapter.FirstUnicastAddress; unicast != nullptr; unicast = unicast->Next)
            {
                if (unicast->Address.lpSockaddr == nullptr || unicast->Address.lpSockaddr->sa_family != AF_INET)
                {
                    continue;
                }
                const auto address = SockaddrToString(unicast->Address.lpSockaddr);
                if (address.empty() || StartsWith(address, "127.") || StartsWith(address, "169.254.")) continue;
                if (seen_ips.insert(address).second) ips.push_back(address);
            }
        }

        if (result.Gateway.empty() && adapter.FirstGatewayAddress != nullptr)
        {
            const auto gateway = SockaddrToString(adapter.FirstGatewayAddress->Address.lpSockaddr);
            if (!gateway.empty() && gateway != "0.0.0.0")
            {
                const auto mac = FormatMac(adapter.PhysicalAddress, adapter.PhysicalAddressLength);
                result.Gateway = mac.empty() ? gateway : gateway + "/" + mac;
            }
        }

        for (auto* server = adapter.FirstDnsServerAddress; server != nullptr; server = server->Next)
        {
            if (server->Address.lpSockaddr == nullptr || server->Address.lpSockaddr->sa_family != AF_INET) continue;
            const auto address = SockaddrToString(server->Address.lpSockaddr);
            if (!address.empty()) dns.insert(address);
        }
    });
    result.IP = Join(ips, ",");
    result.Dns = Join(std::vector<std::string>(dns.begin(), dns.end()), ",");
    // StarServer 的 Node.Dns 字段长度限制为 50，超长会导致节点校验失败
    if (result.Dns.size() > 50) result.Dns.resize(50);
    return result;
}

std::string LocalIP() { return Network().IP; }
std::string Gateway() { return Network().Gateway; }
std::string Dns() { return Network().Dns; }

std::string Architecture()
{
#if defined(_M_ARM64)
    return "Arm64";
#elif defined(_M_ARM)
    return "Arm";
#elif defined(_M_X64)
    return "X64";
#elif defined(_M_IX86)
    return "X86";
#else
    return "Unknown";
#endif
}

std::string ExecutablePath()
{
    std::wstring buffer(MAX_PATH, L'\0');
    for (;;)
    {
        const DWORD written = ::GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
        if (written == 0) return {};
        if (written < buffer.size()) return Utf8FromWide(buffer.substr(0, written));
        buffer.resize(buffer.size() * 2);
    }
}

std::string ExecutableDir()
{
    const auto path = ExecutablePath();
    if (path.empty()) return {};
    const auto position = path.find_last_of("\\/");
    return position == std::string::npos ? std::string(".") : path.substr(0, position);
}

int ProcessId() { return static_cast<int>(::GetCurrentProcessId()); }

bool IsRoot()
{
    BOOL is_admin = FALSE;
    SID_IDENTIFIER_AUTHORITY authority = SECURITY_NT_AUTHORITY;
    PSID administrators = nullptr;
    if (::AllocateAndInitializeSid(&authority, 2, SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS, 0, 0, 0,
                                   0, 0, 0, &administrators))
    {
        ::CheckTokenMembership(nullptr, administrators, &is_admin);
        ::FreeSid(administrators);
    }
    return is_admin != FALSE;
}

std::string HomeDir()
{
    auto value = GetEnv(L"USERPROFILE");
    if (!value.empty()) return value;
    const auto drive = GetEnv(L"HOMEDRIVE");
    const auto path = GetEnv(L"HOMEPATH");
    if (!drive.empty() && !path.empty()) return drive + path;
    return ".";
}

std::string DefaultDataDir()
{
    auto program_data = GetEnv(L"ProgramData");
    if (!program_data.empty()) return program_data + "\\StarAgent\\data";
    auto local = GetEnv(L"LOCALAPPDATA");
    if (!local.empty()) return local + "\\StarAgent\\data";
    return "data";
}

std::string TimeZoneName() { return System().TimeZone; }

bool SetSystemTime(TimePoint value)
{
    const auto milliseconds =
        std::chrono::duration_cast<std::chrono::milliseconds>(value.time_since_epoch()).count();
    const std::time_t seconds = static_cast<std::time_t>(milliseconds / 1000);
    std::tm tm{};
    if (gmtime_s(&tm, &seconds) != 0) return false;

    SYSTEMTIME time{};
    time.wYear = static_cast<WORD>(tm.tm_year + 1900);
    time.wMonth = static_cast<WORD>(tm.tm_mon + 1);
    time.wDay = static_cast<WORD>(tm.tm_mday);
    time.wHour = static_cast<WORD>(tm.tm_hour);
    time.wMinute = static_cast<WORD>(tm.tm_min);
    time.wSecond = static_cast<WORD>(tm.tm_sec);
    time.wMilliseconds = static_cast<WORD>(milliseconds % 1000);
    return ::SetSystemTime(&time) != FALSE;
}

ShellResult RunShell(const std::string& script, int timeout_ms, const std::atomic<bool>& cancelled)
{
    ShellResult result;
    if (timeout_ms <= 0) timeout_ms = 30000;

    SECURITY_ATTRIBUTES attributes{};
    attributes.nLength = sizeof(attributes);
    attributes.bInheritHandle = TRUE;

    HANDLE read_pipe = nullptr;
    HANDLE write_pipe = nullptr;
    if (!::CreatePipe(&read_pipe, &write_pipe, &attributes, 0))
    {
        result.Error = "CreatePipe failed";
        return result;
    }
    ::SetHandleInformation(read_pipe, HANDLE_FLAG_INHERIT, 0);

    HANDLE null_input = ::CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &attributes,
                                      OPEN_EXISTING, 0, nullptr);

    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    startup.wShowWindow = SW_HIDE;
    startup.hStdOutput = write_pipe;
    startup.hStdError = write_pipe;
    startup.hStdInput = null_input;

    auto comspec = GetEnv(L"COMSPEC");
    if (comspec.empty()) comspec = "C:\\Windows\\System32\\cmd.exe";
    const auto command_line_text = "\"" + comspec + "\" /d /s /c " + script;
    std::wstring command_line = WideFromUtf8(command_line_text);

    PROCESS_INFORMATION process{};
    const BOOL created = ::CreateProcessW(nullptr, command_line.data(), nullptr, nullptr, TRUE,
                                          CREATE_NO_WINDOW | CREATE_SUSPENDED, nullptr, nullptr, &startup,
                                          &process);
    ::CloseHandle(write_pipe);
    if (null_input != INVALID_HANDLE_VALUE) ::CloseHandle(null_input);

    if (!created)
    {
        ::CloseHandle(read_pipe);
        result.Error = "CreateProcess failed (" + std::to_string(::GetLastError()) + ")";
        return result;
    }

    HANDLE job = ::CreateJobObjectW(nullptr, nullptr);
    if (job != nullptr)
    {
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION information{};
        information.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        ::SetInformationJobObject(job, JobObjectExtendedLimitInformation, &information, sizeof(information));
        ::AssignProcessToJobObject(job, process.hProcess);
    }
    ::ResumeThread(process.hThread);

    HeadTailBuffer buffer(64 * 1024);
    const auto started = SteadyMsNow();
    bool killed = false;

    auto kill_tree = [&]() {
        if (job != nullptr)
        {
            ::TerminateJobObject(job, 1);
        }
        else
        {
            ::TerminateProcess(process.hProcess, 1);
        }
        killed = true;
    };

    auto drain = [&]() {
        char chunk[4096];
        for (;;)
        {
            DWORD available = 0;
            if (!::PeekNamedPipe(read_pipe, nullptr, 0, nullptr, &available, nullptr) || available == 0) break;
            DWORD read = 0;
            if (!::ReadFile(read_pipe, chunk, std::min<DWORD>(available, sizeof(chunk)), &read, nullptr) || read == 0)
            {
                break;
            }
            buffer.Append(chunk, read);
        }
    };

    for (;;)
    {
        drain();
        if (::WaitForSingleObject(process.hProcess, 0) == WAIT_OBJECT_0) break;
        if (cancelled.load())
        {
            result.Cancelled = true;
            kill_tree();
            break;
        }
        if (SteadyMsNow() - started > timeout_ms)
        {
            result.TimedOut = true;
            kill_tree();
            break;
        }
        if (!::PeekNamedPipe(read_pipe, nullptr, 0, nullptr, nullptr, nullptr))
        {
            // The pipe closed; wait for the process to finish.
            ::WaitForSingleObject(process.hProcess, 50);
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }

    ::WaitForSingleObject(process.hProcess, 5000);
    drain();

    DWORD exit_code = 1;
    ::GetExitCodeProcess(process.hProcess, &exit_code);
    result.ExitCode = killed ? -1 : static_cast<int>(exit_code);
    result.Output = buffer.ToString();
    result.Truncated = buffer.Truncated();

    ::CloseHandle(read_pipe);
    if (job != nullptr) ::CloseHandle(job);
    ::CloseHandle(process.hThread);
    ::CloseHandle(process.hProcess);
    return result;
}

bool RestartSelf(const std::vector<std::string>& arguments)
{
    if (arguments.empty()) return false;

    auto quote = [](const std::string& value) {
        if (value.find_first_of(" \t\"") == std::string::npos) return value;
        std::string result = "\"";
        for (const char c : value)
        {
            if (c == '"') result += "\\\"";
            else result += c;
        }
        result += "\"";
        return result;
    };

    std::string command_line;
    for (const auto& argument : arguments)
    {
        if (!command_line.empty()) command_line += ' ';
        command_line += quote(argument);
    }

    auto wide = WideFromUtf8(command_line);
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    const BOOL created = ::CreateProcessW(nullptr, wide.data(), nullptr, nullptr, FALSE,
                                          CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS, nullptr, nullptr, &startup,
                                          &process);
    if (!created) return false;
    ::CloseHandle(process.hThread);
    ::CloseHandle(process.hProcess);
    return true;
}

bool SleepCancelable(int milliseconds, const std::atomic<bool>& cancelled)
{
    const auto deadline = SteadyMsNow() + milliseconds;
    while (!cancelled.load() && SteadyMsNow() < deadline)
    {
        const auto remaining = deadline - SteadyMsNow();
        std::this_thread::sleep_for(std::chrono::milliseconds(std::min<std::int64_t>(50, std::max<std::int64_t>(1, remaining))));
    }
    return !cancelled.load();
}

} // namespace staragent::platform
