// SPDX-License-Identifier: MIT
#include "config.hpp"
#include "events.hpp"
#include "executor.hpp"
#include "http_client.hpp"
#include "protocol.hpp"
#include "util.hpp"

#include <cstdlib>
#include <filesystem>
#include <iostream>
#include <string>

namespace {

int g_failures = 0;
int g_checks = 0;

void Check(bool condition, const std::string& name)
{
    g_checks++;
    if (!condition)
    {
        g_failures++;
        std::cerr << "[FAIL] " << name << "\n";
    }
}

void SetEnv(const char* name, const char* value)
{
#ifdef _WIN32
    _putenv_s(name, value == nullptr ? "" : value);
#else
    if (value == nullptr) ::unsetenv(name);
    else ::setenv(name, value, 1);
#endif
}

std::filesystem::path TestDirectory()
{
    return std::filesystem::current_path() / ".staragent-tests";
}

void TestProtocolTime()
{
    const auto parsed = staragent::ProtocolTime::Parse("2026-10-17 08:00:00");
    Check(parsed.IsSet(), "parse protocol time");
    Check(parsed.Text() == "2026-10-17 08:00:00", "format protocol time");
    Check(parsed.UnixMs() > 0, "protocol time unix ms");

    const auto zero = staragent::ProtocolTime::Parse("0001-01-01 00:00:00");
    Check(!zero.IsSet(), "zero protocol time is unset");
    Check(zero.Text() == "0001-01-01 00:00:00", "zero protocol time text");

    const auto iso = staragent::ProtocolTime::Parse("2026-10-17T08:00:00Z");
    Check(iso.IsSet(), "parse iso time");
    Check(iso.UnixMs() == parsed.UnixMs(), "iso and protocol time agree");

    Check(!staragent::ProtocolTime::Parse("not a time").IsSet(), "invalid time rejected");
}

void TestUnwrapResponse()
{
    // A string Code is the node code from LoginResponse, not an API error.
    const auto login = staragent::Json::parse(R"({"Code":"12345678","Token":"t","Expire":7200})");
    const auto unwrapped = staragent::UnwrapResponse(login);
    const auto response = staragent::ParseLoginResponse(unwrapped);
    Check(response.Code == "12345678", "string Code is preserved");
    Check(response.Token == "t", "token parsed");

    const auto wrapped = staragent::Json::parse(R"({"code":0,"data":{"Name":"node"}})");
    const auto data = staragent::UnwrapResponse(wrapped);
    Check(data.is_object() && data["Name"] == "node", "code zero unwraps data");

    bool threw = false;
    try
    {
        staragent::UnwrapResponse(staragent::Json::parse(R"({"code":401,"msg":"denied"})"));
    }
    catch (const staragent::ApiError& error)
    {
        threw = error.Code == 401;
    }
    Check(threw, "numeric code raises ApiError");

    const auto raw = staragent::Json::parse(R"({"Time":1,"Period":60})");
    Check(staragent::UnwrapResponse(raw)["Period"] == 60, "bare object passed through");
}

void TestPingResponse()
{
    const auto value = staragent::Json::parse(R"({
        "Time": 0, "ServerTime": 1760000000000, "Period": 1, "Token": "next",
        "Commands": [{"Id": 7, "Command": "node/synctime", "Expire": "2026-10-17 08:00:00"}]
    })");
    const auto response = staragent::ParsePingResponse(staragent::UnwrapResponse(value));
    Check(response.Period == 1, "ping period");
    Check(response.Token == "next", "ping token");
    Check(response.Commands.size() == 1, "ping command count");
    Check(response.Commands[0].Id == 7, "ping command id");
    Check(response.Commands[0].Expire.IsSet(), "ping command expire");
}

void TestTruncate()
{
    std::string large(100 * 1024, 'a');
    const auto truncated = staragent::TruncateOutput(large);
    Check(truncated.size() <= 64 * 1024, "truncated output fits the limit");
    Check(truncated.find("output truncated") != std::string::npos, "truncation marker present");
    Check(staragent::TruncateOutput("short") == "short", "short output unchanged");
}

void TestUrls()
{
    const auto parsed = staragent::ParseServerUrl("http://127.0.0.1:54321/prefix/");
    Check(parsed.Scheme == "http", "url scheme");
    Check(parsed.Host == "127.0.0.1:54321", "url authority");
    Check(parsed.PathPrefix == "/prefix", "url prefix trimmed");
    Check(staragent::BuildNotifyUrl("https://star.example.com:6600") ==
              "wss://star.example.com:6600/node/notify",
          "notify url uses wss");
    Check(staragent::BuildNotifyUrl("http://127.0.0.1:1234/base") ==
              "ws://127.0.0.1:1234/base/node/notify",
          "notify url keeps prefix");
}

void TestConfig()
{
    SetEnv("STARAGENT_SERVER", "http://127.0.0.1:9999");
    SetEnv("STARAGENT_DATA_DIR", (TestDirectory() / "data").string().c_str());
    SetEnv("STARAGENT_METRICS_INTERVAL", "7");
    SetEnv("STARAGENT_ALLOW_EXEC", "true");

    const auto missing = staragent::LoadConfig((TestDirectory() / "missing.json").string());
    Check(missing.Server == "http://127.0.0.1:9999", "env server override");
    Check(missing.DataDir == (TestDirectory() / "data").string(), "env data dir alias");
    Check(missing.MetricsInterval == 7, "env metrics interval");
    Check(missing.AllowExec, "env allow exec");

    SetEnv("STARAGENT_SERVER", nullptr);
    SetEnv("STARAGENT_DATA_DIR", nullptr);
    SetEnv("STARAGENT_METRICS_INTERVAL", nullptr);
    SetEnv("STARAGENT_ALLOW_EXEC", nullptr);

    std::filesystem::create_directories(TestDirectory());
    const auto path = (TestDirectory() / "staragent.json").string();
    auto config = staragent::DefaultConfig();
    config.Server = "http://127.0.0.1:6600";
    config.DataDir = (TestDirectory() / "data").string();
    config.Code = "CONF0001";
    config.Secret = "secret-value";
    staragent::SaveConfigAtomic(path, config);

    const auto loaded = staragent::LoadConfig(path);
    Check(loaded.Server == config.Server, "config round trip server");
    Check(loaded.Code == "CONF0001", "config round trip code");
    Check(loaded.Secret == "secret-value", "config round trip secret");

    staragent::ConfigStore store(path, loaded);
    store.Update([](staragent::Config& value) { value.Channel = "Beta"; });
    Check(staragent::LoadConfig(path).Channel == "Beta", "config store update persisted");
}

void TestEventQueue()
{
    std::filesystem::create_directories(TestDirectory());
    const auto path = (TestDirectory() / "events.json").string();
    std::filesystem::remove(path);
    {
        staragent::EventQueue queue(path, 10);
        staragent::EventModel first;
        first.Time = 1000;
        first.Type = "info";
        first.Name = "Startup";
        queue.Add(first);
        staragent::EventModel second;
        second.Time = 2000;
        second.Type = "alert";
        second.Name = "Test";
        queue.Add(second);
        Check(queue.Size() == 2, "event queue size");

        auto batch = queue.Take(1);
        Check(batch.size() == 1 && batch[0].Name == "Startup", "event queue take");
        queue.PutFront(batch);
        Check(queue.Size() == 2, "event queue put front");
    }

    staragent::EventQueue restored(path, 10);
    Check(restored.Size() == 2, "event queue restored from disk");
    restored.Take(10);
    Check(restored.Size() == 0, "event queue drained");
    std::filesystem::remove(path);
}

void TestExecutor()
{
    staragent::ExecutorOptions options;
    options.AllowExec = false;
    options.TimeoutSeconds = 1;
    options.ServerNow = []() { return staragent::Clock::now(); };
    options.SyncTime = [](staragent::TimePoint) { return true; };
    staragent::Executor executor(options);

    std::vector<staragent::CommandReplyModel> replies;
    auto collect = [&](const staragent::CommandReplyModel& reply) { replies.push_back(reply); };

    staragent::CommandModel command;
    command.Id = 100;
    command.Command = "node/synctime";
    executor.Handle(command, collect);
    Check(replies.size() == 1, "synctime reply count");
    Check(replies[0].Status == 2, "synctime status completed");
    Check(!replies[0].Data.empty(), "synctime data not empty");

    executor.Handle(command, collect);
    Check(replies.size() == 1, "duplicate command ignored");

    staragent::CommandModel expired;
    expired.Id = 101;
    expired.Command = "node/synctime";
    expired.Expire = staragent::ProtocolTime::FromTimePoint(staragent::Clock::now() -
                                                             std::chrono::minutes(1));
    executor.Handle(expired, collect);
    Check(replies.size() == 2 && replies[1].Status == 3, "expired command cancelled");

    staragent::CommandModel unknown;
    unknown.Id = 102;
    unknown.Command = "deploy/install";
    executor.Handle(unknown, collect);
    Check(replies.size() == 3 && replies[2].Status == 4, "unsupported command error");
    Check(replies[2].Data.find("不支持命令") != std::string::npos, "unsupported command message");

#ifdef _WIN32
    staragent::CommandModel shell;
    shell.Id = 103;
    shell.Command = "cmd";
    shell.Argument = "echo hello";
#else
    staragent::CommandModel shell;
    shell.Id = 103;
    shell.Command = "bash";
    shell.Argument = "echo hello";
#endif
    executor.Handle(shell, collect);
    Check(replies.size() == 4 && replies[3].Status == 3, "shell disabled by default");
    Check(replies[3].Data == "命令执行未启用", "shell disabled message");

    executor.Stop();
}

void TestValidation()
{
    auto config = staragent::DefaultConfig();
    config.Server.clear();
    bool threw = false;
    try
    {
        staragent::Validate(config);
    }
    catch (const std::exception&)
    {
        threw = true;
    }
    Check(threw, "missing server rejected");

    config.Server = "ftp://example.com";
    threw = false;
    try
    {
        staragent::Validate(config);
    }
    catch (const std::exception&)
    {
        threw = true;
    }
    Check(threw, "unsupported scheme rejected");
}

} // namespace

int main()
{
    std::error_code error;
    std::filesystem::remove_all(TestDirectory(), error);
    std::filesystem::create_directories(TestDirectory(), error);

    TestProtocolTime();
    TestUnwrapResponse();
    TestPingResponse();
    TestTruncate();
    TestUrls();
    TestConfig();
    TestEventQueue();
    TestExecutor();
    TestValidation();

    std::filesystem::remove_all(TestDirectory(), error);

    std::cout << g_checks - g_failures << "/" << g_checks << " checks passed\n";
    return g_failures == 0 ? 0 : 1;
}
