// SPDX-License-Identifier: MIT
// StarAgent Native C++ - protocol models and JSON mapping.
#pragma once

#include "util.hpp"

#include <cstdint>
#include <stdexcept>
#include <string>
#include <vector>

#include <json.hpp>

namespace staragent {

using Json = nlohmann::json;

// Command status values defined by the protocol.
enum class CommandStatus : int
{
    Ready = 0,
    Processing = 1,
    Completed = 2,
    Cancelled = 3,
    Error = 4,
};

struct ApiError : std::runtime_error
{
    ApiError(int value, const std::string& message)
        : std::runtime_error(message.empty() ? "server returned code " + std::to_string(value) : message),
          Code(value)
    {
    }

    int Code;
};

struct ProtocolError : std::runtime_error
{
    explicit ProtocolError(const std::string& message) : std::runtime_error(message) {}
};

struct NodeInfo
{
    std::string Version;
    ProtocolTime Compile;
    std::string OSName;
    std::string OSVersion;
    std::string Architecture;
    std::string MachineName;
    std::string UserName;
    std::string IP;
    std::string Gateway;
    std::string Dns;
    int ProcessorCount = 0;
    std::uint64_t Memory = 0;
    std::uint64_t AvailableMemory = 0;
    std::uint64_t FreeMemory = 0;
    std::uint64_t TotalSize = 0;
    std::uint64_t AvailableFreeSpace = 0;
    std::uint64_t DriveSize = 0;
    std::string DriveInfo;
    std::uint64_t MaxOpenFiles = 0;
    std::string CLibVersion;
    std::string Dpi;
    std::string Resolution;
    std::string Product;
    std::string Vendor;
    std::string Processor;
    std::string GPU;
    std::string UUID;
    std::string MachineGuid;
    std::string SerialNumber;
    std::string Board;
    std::string DiskID;
    std::string Macs;
    std::string InstallPath;
    std::string Runtime;
    std::string Framework;
    std::string TimeZone;
    ProtocolTime Time;
    std::vector<std::string> Plugins;
};

struct LoginInfo
{
    std::string Code;
    std::string Secret;
    std::string ProductCode;
    std::string ClientId;
    std::string Project;
    NodeInfo Node;
};

struct LoginResponse
{
    std::string Code;
    std::string Secret;
    std::string Name;
    std::string Token;
    int Expire = 0;
    std::int64_t Time = 0;
    std::int64_t ServerTime = 0;
};

struct CommandModel
{
    std::int64_t Id = 0;
    std::string Command;
    std::string Argument;
    ProtocolTime StartTime;
    ProtocolTime Expire;
    std::string TraceId;
    int Status = 0;
};

struct PingInfo
{
    std::int64_t Time = 0;
    std::string IP;
    std::string Gateway;
    std::string Dns;
    std::string Macs;
    std::string DriveInfo;
    std::string Framework;
    std::string Processes;
    std::uint64_t ProcessCount = 0;
    std::uint64_t Memory = 0;
    std::uint64_t AvailableMemory = 0;
    std::uint64_t FreeMemory = 0;
    std::uint64_t TotalSize = 0;
    std::uint64_t AvailableFreeSpace = 0;
    double CpuRate = 0;
    double Temperature = 0;
    double Battery = 0;
    int Signal = 0;
    std::uint64_t UplinkSpeed = 0;
    std::uint64_t DownlinkSpeed = 0;
    std::uint64_t Uptime = 0;
    std::int64_t Delay = 0;
    std::uint64_t TcpConnections = 0;
    std::uint64_t TcpTimeWait = 0;
    std::uint64_t TcpCloseWait = 0;
    double IntranetScore = 0;
    double InternetScore = 0;
    double SystemLoad = 0;
    std::uint64_t DiskIOPS = 0;
    double DiskActiveTime = 0;
    std::uint64_t GatewayLatency = 0;
    double GatewayLossRate = 0;
    std::uint64_t DnsLatency = 0;
    double DnsLossRate = 0;
    std::uint64_t ServerLatency = 0;
    double ServerLossRate = 0;
};

struct PingResponse
{
    std::int64_t Time = 0;
    std::int64_t ServerTime = 0;
    int Period = 0;
    std::string Token;
    std::string NewServer;
    int SyncTime = 0;
    std::vector<CommandModel> Commands;
};

struct CommandReplyModel
{
    std::int64_t Id = 0;
    int Status = 0;
    std::string Data;
};

struct EventModel
{
    std::int64_t Time = 0;
    std::string Type;
    std::string Name;
    std::string Remark;
};

struct UpgradeInfo
{
    std::string Version;
    std::string Source;
    std::string FileHash;
    std::int64_t FileSize = 0;
    std::string Preinstall;
    std::string Executor;
    bool Force = false;
    std::string Description;
};

Json ToJson(const NodeInfo& value);
Json ToJson(const LoginInfo& value);
Json ToJson(const PingInfo& value);
Json ToJson(const CommandReplyModel& value);
Json ToJson(const EventModel& value);

LoginResponse ParseLoginResponse(const Json& value);
PingResponse ParsePingResponse(const Json& value);
CommandModel ParseCommand(const Json& value);
UpgradeInfo ParseUpgradeInfo(const Json& value);
int ParseEventCount(const Json& value);

// Parses a bare response or an ApiFilter wrapper ({"code":0,"data":...}).
Json UnwrapResponse(const Json& value);

std::string TruncateOutput(const std::string& text, std::size_t max_bytes = 64 * 1024);
std::string CompactJson(const Json& value);

} // namespace staragent
