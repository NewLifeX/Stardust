// SPDX-License-Identifier: MIT
#include "protocol.hpp"

#include <algorithm>
#include <cstdlib>

namespace staragent {
namespace {

const Json* FindCi(const Json& object, const std::string& name)
{
    if (!object.is_object()) return nullptr;
    const auto direct = object.find(name);
    if (direct != object.end()) return &*direct;
    for (auto it = object.begin(); it != object.end(); ++it)
    {
        if (EqualsIgnoreCase(it.key(), name)) return &*it;
    }
    return nullptr;
}

std::string GetString(const Json& object, const std::string& name)
{
    const auto* value = FindCi(object, name);
    if (value == nullptr || value->is_null()) return {};
    if (value->is_string()) return value->get<std::string>();
    if (value->is_boolean()) return value->get<bool>() ? "true" : "false";
    if (value->is_number()) return value->dump();
    return {};
}

std::int64_t GetInt64(const Json& object, const std::string& name)
{
    const auto* value = FindCi(object, name);
    if (value == nullptr || value->is_null()) return 0;
    try
    {
        if (value->is_number_integer()) return value->get<std::int64_t>();
        if (value->is_number_unsigned()) return static_cast<std::int64_t>(value->get<std::uint64_t>());
        if (value->is_number_float()) return static_cast<std::int64_t>(value->get<double>());
        if (value->is_boolean()) return value->get<bool>() ? 1 : 0;
        if (value->is_string())
        {
            const auto text = Trim(value->get<std::string>());
            if (text.empty()) return 0;
            return std::strtoll(text.c_str(), nullptr, 10);
        }
    }
    catch (...)
    {
    }
    return 0;
}

double GetDouble(const Json& object, const std::string& name)
{
    const auto* value = FindCi(object, name);
    if (value == nullptr || value->is_null()) return 0;
    try
    {
        if (value->is_number()) return value->get<double>();
        if (value->is_boolean()) return value->get<bool>() ? 1 : 0;
        if (value->is_string())
        {
            const auto text = Trim(value->get<std::string>());
            if (text.empty()) return 0;
            return std::strtod(text.c_str(), nullptr);
        }
    }
    catch (...)
    {
    }
    return 0;
}

bool GetBool(const Json& object, const std::string& name)
{
    const auto* value = FindCi(object, name);
    if (value == nullptr || value->is_null()) return false;
    if (value->is_boolean()) return value->get<bool>();
    if (value->is_number()) return value->get<double>() != 0;
    if (value->is_string())
    {
        const auto text = ToLower(Trim(value->get<std::string>()));
        return text == "true" || text == "1" || text == "yes" || text == "on";
    }
    return false;
}

ProtocolTime GetTime(const Json& object, const std::string& name)
{
    const auto* value = FindCi(object, name);
    if (value == nullptr || value->is_null()) return {};
    if (value->is_string()) return ProtocolTime::Parse(value->get<std::string>());
    if (value->is_number()) return ProtocolTime::FromUnixMs(GetInt64(object, name));
    return {};
}

void PutString(Json& object, const char* name, const std::string& value, bool always = false)
{
    if (always || !value.empty()) object[name] = value;
}

bool IsUtf8Continuation(unsigned char value)
{
    return (value & 0xC0) == 0x80;
}

} // namespace

Json ToJson(const NodeInfo& value)
{
    Json node = Json::object();
    PutString(node, "Version", value.Version);
    node["Compile"] = value.Compile.Text();
    PutString(node, "OSName", value.OSName);
    PutString(node, "OSVersion", value.OSVersion);
    PutString(node, "Architecture", value.Architecture);
    PutString(node, "MachineName", value.MachineName);
    PutString(node, "UserName", value.UserName);
    PutString(node, "IP", value.IP);
    PutString(node, "Gateway", value.Gateway);
    PutString(node, "Dns", value.Dns);
    node["ProcessorCount"] = value.ProcessorCount;
    node["Memory"] = value.Memory;
    node["AvailableMemory"] = value.AvailableMemory;
    node["FreeMemory"] = value.FreeMemory;
    node["TotalSize"] = value.TotalSize;
    node["AvailableFreeSpace"] = value.AvailableFreeSpace;
    node["DriveSize"] = value.DriveSize;
    PutString(node, "DriveInfo", value.DriveInfo);
    if (value.MaxOpenFiles != 0) node["MaxOpenFiles"] = value.MaxOpenFiles;
    PutString(node, "CLibVersion", value.CLibVersion);
    PutString(node, "Dpi", value.Dpi);
    PutString(node, "Resolution", value.Resolution);
    PutString(node, "Product", value.Product);
    PutString(node, "Vendor", value.Vendor);
    PutString(node, "Processor", value.Processor);
    PutString(node, "GPU", value.GPU);
    PutString(node, "UUID", value.UUID);
    PutString(node, "MachineGuid", value.MachineGuid);
    PutString(node, "SerialNumber", value.SerialNumber);
    PutString(node, "Board", value.Board);
    PutString(node, "DiskID", value.DiskID);
    PutString(node, "Macs", value.Macs);
    PutString(node, "InstallPath", value.InstallPath);
    PutString(node, "Runtime", value.Runtime);
    PutString(node, "Framework", value.Framework);
    PutString(node, "TimeZone", value.TimeZone);
    node["Time"] = value.Time.Text();
    if (!value.Plugins.empty()) node["Plugins"] = value.Plugins;
    return node;
}

Json ToJson(const LoginInfo& value)
{
    Json result = Json::object();
    result["Code"] = value.Code;
    result["Secret"] = value.Secret;
    result["ProductCode"] = value.ProductCode;
    PutString(result, "ClientId", value.ClientId);
    PutString(result, "Project", value.Project);
    result["Node"] = ToJson(value.Node);
    return result;
}

Json ToJson(const PingInfo& value)
{
    Json result = Json::object();
    result["Time"] = value.Time;
    PutString(result, "IP", value.IP);
    PutString(result, "Gateway", value.Gateway);
    PutString(result, "Dns", value.Dns);
    PutString(result, "Macs", value.Macs);
    PutString(result, "DriveInfo", value.DriveInfo);
    PutString(result, "Framework", value.Framework);
    PutString(result, "Processes", value.Processes);
    result["ProcessCount"] = value.ProcessCount;
    result["Memory"] = value.Memory;
    result["AvailableMemory"] = value.AvailableMemory;
    result["FreeMemory"] = value.FreeMemory;
    result["TotalSize"] = value.TotalSize;
    result["AvailableFreeSpace"] = value.AvailableFreeSpace;
    result["CpuRate"] = value.CpuRate;
    result["Temperature"] = value.Temperature;
    result["Battery"] = value.Battery;
    result["Signal"] = value.Signal;
    result["UplinkSpeed"] = value.UplinkSpeed;
    result["DownlinkSpeed"] = value.DownlinkSpeed;
    result["Uptime"] = value.Uptime;
    result["Delay"] = value.Delay;
    result["TcpConnections"] = value.TcpConnections;
    result["TcpTimeWait"] = value.TcpTimeWait;
    result["TcpCloseWait"] = value.TcpCloseWait;
    result["IntranetScore"] = value.IntranetScore;
    result["InternetScore"] = value.InternetScore;
    result["SystemLoad"] = value.SystemLoad;
    result["DiskIOPS"] = value.DiskIOPS;
    result["DiskActiveTime"] = value.DiskActiveTime;
    result["GatewayLatency"] = value.GatewayLatency;
    result["GatewayLossRate"] = value.GatewayLossRate;
    result["DnsLatency"] = value.DnsLatency;
    result["DnsLossRate"] = value.DnsLossRate;
    result["ServerLatency"] = value.ServerLatency;
    result["ServerLossRate"] = value.ServerLossRate;
    return result;
}

Json ToJson(const CommandReplyModel& value)
{
    Json result = Json::object();
    result["Id"] = value.Id;
    result["Status"] = value.Status;
    PutString(result, "Data", value.Data);
    return result;
}

Json ToJson(const EventModel& value)
{
    Json result = Json::object();
    result["Time"] = value.Time;
    result["Type"] = value.Type;
    result["Name"] = value.Name;
    PutString(result, "Remark", value.Remark);
    return result;
}

LoginResponse ParseLoginResponse(const Json& value)
{
    LoginResponse result;
    result.Code = GetString(value, "Code");
    result.Secret = GetString(value, "Secret");
    result.Name = GetString(value, "Name");
    result.Token = GetString(value, "Token");
    result.Expire = static_cast<int>(GetInt64(value, "Expire"));
    result.Time = GetInt64(value, "Time");
    result.ServerTime = GetInt64(value, "ServerTime");
    return result;
}

CommandModel ParseCommand(const Json& value)
{
    if (!value.is_object()) throw ProtocolError("command is not a JSON object");
    CommandModel result;
    result.Id = GetInt64(value, "Id");
    result.Command = GetString(value, "Command");
    result.Argument = GetString(value, "Argument");
    result.StartTime = GetTime(value, "StartTime");
    result.Expire = GetTime(value, "Expire");
    result.TraceId = GetString(value, "TraceId");
    result.Status = static_cast<int>(GetInt64(value, "Status"));
    return result;
}

PingResponse ParsePingResponse(const Json& value)
{
    PingResponse result;
    result.Time = GetInt64(value, "Time");
    result.ServerTime = GetInt64(value, "ServerTime");
    result.Period = static_cast<int>(GetInt64(value, "Period"));
    result.Token = GetString(value, "Token");
    result.NewServer = GetString(value, "NewServer");
    result.SyncTime = static_cast<int>(GetInt64(value, "SyncTime"));
    const auto* commands = FindCi(value, "Commands");
    if (commands != nullptr && commands->is_array())
    {
        for (const auto& item : *commands)
        {
            if (item.is_object()) result.Commands.push_back(ParseCommand(item));
        }
    }
    return result;
}

UpgradeInfo ParseUpgradeInfo(const Json& value)
{
    UpgradeInfo result;
    result.Version = GetString(value, "Version");
    result.Source = GetString(value, "Source");
    result.FileHash = GetString(value, "FileHash");
    result.FileSize = GetInt64(value, "FileSize");
    result.Preinstall = GetString(value, "Preinstall");
    result.Executor = GetString(value, "Executor");
    result.Force = GetBool(value, "Force");
    result.Description = GetString(value, "Description");
    return result;
}

int ParseEventCount(const Json& value)
{
    if (value.is_number_integer()) return static_cast<int>(value.get<std::int64_t>());
    if (value.is_number_unsigned()) return static_cast<int>(value.get<std::uint64_t>());
    if (value.is_number_float()) return static_cast<int>(value.get<double>());
    if (value.is_string())
    {
        const auto text = Trim(value.get<std::string>());
        if (text.empty()) return 0;
        return std::atoi(text.c_str());
    }
    if (value.is_null()) return 0;
    if (value.is_object())
    {
        const auto unwrapped = UnwrapResponse(value);
        return ParseEventCount(unwrapped);
    }
    throw ProtocolError("unsupported event count payload");
}

Json UnwrapResponse(const Json& value)
{
    if (!value.is_object()) return value;

    // Only a JSON number is an ApiFilter error code. Login responses use
    // "Code" for the node code, which may itself be all digits; a string Code
    // must stay part of the payload and never be interpreted as an error.
    const auto* code_value = FindCi(value, "code");
    if (code_value != nullptr && (code_value->is_number_integer() || code_value->is_number_unsigned()))
    {
        const auto raw_code = code_value->is_number_unsigned() ? code_value->get<std::uint64_t>()
                                                               : static_cast<std::uint64_t>(code_value->get<std::int64_t>());
        const int code = static_cast<int>(raw_code);
        if (code != 0)
        {
            std::string message = GetString(value, "msg");
            if (message.empty()) message = GetString(value, "message");
            throw ApiError(code, message);
        }

        const auto* data = FindCi(value, "data");
        if (data != nullptr && !data->is_null()) return *data;
    }

    return value;
}

std::string TruncateOutput(const std::string& text, std::size_t max_bytes)
{
    if (text.size() <= max_bytes) return text;

    static const std::string marker = "\n...<output truncated>...\n";
    if (max_bytes <= marker.size()) return text.substr(0, max_bytes);

    std::size_t keep = (max_bytes - marker.size()) / 2;
    std::size_t head_size = keep;
    while (head_size > 0 && IsUtf8Continuation(static_cast<unsigned char>(text[head_size]))) head_size--;

    std::size_t tail_start = text.size() - keep;
    while (tail_start < text.size() && IsUtf8Continuation(static_cast<unsigned char>(text[tail_start]))) tail_start++;

    return text.substr(0, head_size) + marker + text.substr(tail_start);
}

std::string CompactJson(const Json& value)
{
    try
    {
        return value.dump();
    }
    catch (...)
    {
        return "{}";
    }
}

} // namespace staragent
