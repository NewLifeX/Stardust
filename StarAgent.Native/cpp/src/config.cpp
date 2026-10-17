// SPDX-License-Identifier: MIT
#include "config.hpp"

#include "platform.hpp"
#include "util.hpp"

#include <algorithm>
#include <cerrno>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <stdexcept>

#include <json.hpp>

#ifdef _WIN32
#include <io.h>
#include <windows.h>
#else
#include <sys/stat.h>
#endif

namespace staragent {
namespace {

using Json = nlohmann::json;
namespace fs = std::filesystem;

std::string GetStringField(const Json& object, const std::string& name, const std::string& fallback = {})
{
    for (auto it = object.begin(); it != object.end(); ++it)
    {
        if (EqualsIgnoreCase(it.key(), name))
        {
            if (it->is_string()) return it->get<std::string>();
            if (it->is_null()) return {};
            if (it->is_number() || it->is_boolean()) return it->dump();
            return fallback;
        }
    }
    return fallback;
}

int GetIntField(const Json& object, const std::string& name, int fallback)
{
    for (auto it = object.begin(); it != object.end(); ++it)
    {
        if (!EqualsIgnoreCase(it.key(), name)) continue;
        try
        {
            if (it->is_number_integer()) return it->get<int>();
            if (it->is_number_unsigned()) return static_cast<int>(it->get<unsigned>());
            if (it->is_number_float()) return static_cast<int>(it->get<double>());
            if (it->is_string()) return std::atoi(Trim(it->get<std::string>()).c_str());
        }
        catch (...)
        {
        }
        return fallback;
    }
    return fallback;
}

bool GetBoolField(const Json& object, const std::string& name, bool fallback)
{
    for (auto it = object.begin(); it != object.end(); ++it)
    {
        if (!EqualsIgnoreCase(it.key(), name)) continue;
        if (it->is_boolean()) return it->get<bool>();
        if (it->is_number()) return it->get<double>() != 0;
        if (it->is_string())
        {
            const auto text = ToLower(Trim(it->get<std::string>()));
            if (text == "true" || text == "1" || text == "yes" || text == "on") return true;
            if (text == "false" || text == "0" || text == "no" || text == "off") return false;
        }
        return fallback;
    }
    return fallback;
}

void SetPermissions600(const std::string& path)
{
#ifdef _WIN32
    // The Windows CRT only exposes the read-only bit. The file lives in the
    // agent data directory, which is created with owner-only inheritance.
    ::_chmod(path.c_str(), _S_IREAD | _S_IWRITE);
#else
    ::chmod(path.c_str(), 0600);
#endif
}

std::wstring Utf8ToWide(const std::string& value)
{
#ifdef _WIN32
    if (value.empty()) return {};
    const int size = ::MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, nullptr, 0);
    if (size <= 0) return {};
    std::wstring result(static_cast<std::size_t>(size - 1), L'\0');
    ::MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, result.data(), size);
    return result;
#else
    return {};
#endif
}

void ReplaceFile(const std::string& temporary, const std::string& target)
{
#ifdef _WIN32
    const auto source = Utf8ToWide(temporary);
    const auto destination = Utf8ToWide(target);
    if (!::MoveFileExW(source.c_str(), destination.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
    {
        throw std::runtime_error("rename temporary file failed");
    }
#else
    std::error_code error;
    fs::rename(temporary, target, error);
    if (error) throw std::runtime_error("rename temporary file failed: " + error.message());
#endif
}

bool ParseBool(const std::string& value, bool& result)
{
    const auto text = ToLower(Trim(value));
    if (text == "true" || text == "1" || text == "yes" || text == "on")
    {
        result = true;
        return true;
    }
    if (text == "false" || text == "0" || text == "no" || text == "off")
    {
        result = false;
        return true;
    }
    return false;
}

std::vector<std::string> SplitCommandList(const std::string& value)
{
    std::vector<std::string> result;
    for (const auto& item : SplitAny(value, ",;"))
    {
        const auto trimmed = Trim(item);
        if (!trimmed.empty()) result.push_back(trimmed);
    }
    return result;
}

} // namespace

Config DefaultConfig()
{
    Config config;
    config.ProductCode = "StarAgent-Cpp";
    config.Channel = "Release";
    config.DataDir = platform::DefaultDataDir();
    config.LogLevel = "info";
    config.MetricsInterval = 60;
    config.AllowExec = false;
    config.CommandTimeout = 30;
    config.TlsVerify = true;
    return config;
}

std::string DefaultConfigPath()
{
    const auto value = EnvironmentValue({"STARAGENT_CONFIG", "STARAGENT_CONFIG_FILE"});
    return value.empty() ? "staragent.json" : value;
}

std::string EnvironmentValue(const std::vector<std::string>& names)
{
    for (const auto& name : names)
    {
        const char* value = std::getenv(name.c_str());
        if (value != nullptr) return value;
    }
    return {};
}

Config LoadConfig(const std::string& path)
{
    Config config = DefaultConfig();

    std::error_code error;
    const bool exists = fs::exists(path, error);
    if (exists)
    {
        std::ifstream stream(path, std::ios::binary);
        if (!stream) throw std::runtime_error("read config failed: " + path);
        Json value;
        try
        {
            stream >> value;
        }
        catch (const std::exception& ex)
        {
            throw std::runtime_error("decode config " + path + ": " + ex.what());
        }
        if (!value.is_object()) throw std::runtime_error("decode config " + path + ": not a JSON object");

        config.Server = GetStringField(value, "Server", config.Server);
        config.ProductCode = GetStringField(value, "ProductCode", config.ProductCode);
        config.Code = GetStringField(value, "Code", config.Code);
        config.Secret = GetStringField(value, "Secret", config.Secret);
        config.Project = GetStringField(value, "Project", config.Project);
        config.Channel = GetStringField(value, "Channel", config.Channel);
        config.DataDir = GetStringField(value, "DataDir", config.DataDir);
        config.LogLevel = GetStringField(value, "LogLevel", config.LogLevel);
        config.MetricsInterval = GetIntField(value, "MetricsInterval", config.MetricsInterval);
        config.AllowExec = GetBoolField(value, "AllowExec", config.AllowExec);
        config.CommandTimeout = GetIntField(value, "CommandTimeout", config.CommandTimeout);
        config.TlsVerify = GetBoolField(value, "TlsVerify", config.TlsVerify);
        if (value.contains("AllowedCommands") && value["AllowedCommands"].is_array())
        {
            config.AllowedCommands.clear();
            for (const auto& item : value["AllowedCommands"])
            {
                if (item.is_string()) config.AllowedCommands.push_back(item.get<std::string>());
            }
        }
    }

    ApplyEnvironment(config);
    Normalize(config);
    return config;
}

void ApplyEnvironment(Config& config)
{
    auto set_string = [&](const std::vector<std::string>& names, std::string& target) {
        const auto value = EnvironmentValue(names);
        if (!EnvironmentValue(names).empty() || std::getenv(names[0].c_str()) != nullptr) target = value;
    };

    set_string({"STARAGENT_SERVER", "STARAGENT_SERVER_URL"}, config.Server);
    set_string({"STARAGENT_PRODUCTCODE", "STARAGENT_PRODUCT_CODE"}, config.ProductCode);
    set_string({"STARAGENT_CODE"}, config.Code);
    set_string({"STARAGENT_SECRET"}, config.Secret);
    set_string({"STARAGENT_PROJECT"}, config.Project);
    set_string({"STARAGENT_CHANNEL"}, config.Channel);
    set_string({"STARAGENT_DATADIR", "STARAGENT_DATA_DIR"}, config.DataDir);
    set_string({"STARAGENT_LOGLEVEL", "STARAGENT_LOG_LEVEL"}, config.LogLevel);

    auto set_int = [&](const std::vector<std::string>& names, int& target) {
        const auto value = EnvironmentValue(names);
        if (!value.empty()) target = std::atoi(Trim(value).c_str());
    };
    set_int({"STARAGENT_METRICSINTERVAL", "STARAGENT_METRICS_INTERVAL"}, config.MetricsInterval);
    set_int({"STARAGENT_COMMANDTIMEOUT", "STARAGENT_COMMAND_TIMEOUT"}, config.CommandTimeout);

    auto set_bool = [&](const std::vector<std::string>& names, bool& target) {
        const auto value = EnvironmentValue(names);
        bool parsed = false;
        if (!value.empty() && ParseBool(value, parsed)) target = parsed;
    };
    set_bool({"STARAGENT_ALLOWEXEC", "STARAGENT_ALLOW_EXEC"}, config.AllowExec);
    set_bool({"STARAGENT_TLSVERIFY", "STARAGENT_TLS_VERIFY"}, config.TlsVerify);

    const auto allowed = EnvironmentValue({"STARAGENT_ALLOWEDCOMMANDS", "STARAGENT_ALLOWED_COMMANDS"});
    if (!allowed.empty()) config.AllowedCommands = SplitCommandList(allowed);
}

void Normalize(Config& config)
{
    while (!config.Server.empty() && config.Server.back() == '/') config.Server.pop_back();
    config.Server = Trim(config.Server);
    config.ProductCode = Trim(config.ProductCode);
    if (config.ProductCode.empty()) config.ProductCode = "StarAgent-Cpp";
    config.Channel = Trim(config.Channel);
    if (config.Channel.empty()) config.Channel = "Release";
    config.LogLevel = Trim(config.LogLevel);
    if (config.LogLevel.empty()) config.LogLevel = "info";
    config.DataDir = Trim(config.DataDir);
    if (config.DataDir.empty()) config.DataDir = platform::DefaultDataDir();
    if (config.MetricsInterval <= 0) config.MetricsInterval = 60;
    if (config.CommandTimeout <= 0) config.CommandTimeout = 30;
    for (auto& item : config.AllowedCommands) item = Trim(item);
    config.AllowedCommands.erase(
        std::remove_if(config.AllowedCommands.begin(), config.AllowedCommands.end(),
                       [](const std::string& item) { return item.empty(); }),
        config.AllowedCommands.end());
}

void Validate(const Config& config)
{
    if (config.Server.empty())
    {
        throw std::runtime_error("Server is required (configure staragent.json or STARAGENT_SERVER)");
    }
    const auto scheme_end = config.Server.find("://");
    if (scheme_end == std::string::npos)
    {
        throw std::runtime_error("invalid Server: " + config.Server);
    }
    const auto scheme = ToLower(config.Server.substr(0, scheme_end));
    if (scheme != "http" && scheme != "https")
    {
        throw std::runtime_error("unsupported Server scheme: " + scheme);
    }
    if (config.Server.size() <= scheme_end + 3)
    {
        throw std::runtime_error("invalid Server: " + config.Server);
    }
    if (config.ProductCode.empty()) throw std::runtime_error("ProductCode is required");
    if (config.MetricsInterval < 5 || config.MetricsInterval > 3600)
    {
        throw std::runtime_error("MetricsInterval must be between 5 and 3600 seconds");
    }
    if (config.CommandTimeout < 1 || config.CommandTimeout > 300)
    {
        throw std::runtime_error("CommandTimeout must be between 1 and 300 seconds");
    }
}

void SaveConfigAtomic(const std::string& path, const Config& source)
{
    Config config = source;
    Normalize(config);

    Json value = Json::object();
    value["Server"] = config.Server;
    value["ProductCode"] = config.ProductCode;
    value["Code"] = config.Code;
    value["Secret"] = config.Secret;
    value["Project"] = config.Project;
    value["Channel"] = config.Channel;
    value["DataDir"] = config.DataDir;
    value["LogLevel"] = config.LogLevel;
    value["MetricsInterval"] = config.MetricsInterval;
    value["AllowExec"] = config.AllowExec;
    value["AllowedCommands"] = config.AllowedCommands;
    value["CommandTimeout"] = config.CommandTimeout;
    value["TlsVerify"] = config.TlsVerify;

    WriteFileAtomic(path, value.dump(2) + "\n");
}

void WriteFileAtomic(const std::string& path, const std::string& data)
{
    const fs::path target(path);
    const fs::path directory = target.has_parent_path() ? target.parent_path() : fs::path(".");
    std::error_code error;
    fs::create_directories(directory, error);
    if (error) throw std::runtime_error("create directory failed: " + directory.string());

    const fs::path temporary =
        directory / (".staragent-" + std::to_string(platform::ProcessId()) + "-" +
                     std::to_string(SteadyMsNow()) + ".tmp");

    {
        std::ofstream stream(temporary, std::ios::binary | std::ios::trunc);
        if (!stream) throw std::runtime_error("create temporary file failed: " + temporary.string());
        stream.write(data.data(), static_cast<std::streamsize>(data.size()));
        stream.flush();
        if (!stream) throw std::runtime_error("write temporary file failed: " + temporary.string());
    }

    SetPermissions600(temporary.string());
    ReplaceFile(temporary.string(), target.string());
    SetPermissions600(target.string());
}

ConfigStore::ConfigStore(std::string path, Config config) : path_(std::move(path)), config_(std::move(config))
{
    Normalize(config_);
}

Config ConfigStore::Snapshot() const
{
    std::lock_guard<std::mutex> lock(mutex_);
    return config_;
}

Config ConfigStore::Update(const std::function<void(Config&)>& fn)
{
    std::lock_guard<std::mutex> lock(mutex_);
    Config next = config_;
    fn(next);
    Normalize(next);
    SaveConfigAtomic(path_, next);
    config_ = next;
    return next;
}

} // namespace staragent
