// SPDX-License-Identifier: MIT
#include "service.hpp"

#include "platform.hpp"
#include "util.hpp"

#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <sstream>
#include <sys/stat.h>
#include <unistd.h>

#if defined(__APPLE__)
#define STARAGENT_MACOS 1
#endif

namespace staragent::service {
namespace {

constexpr const char* kServiceName = "staragent";
constexpr const char* kLaunchdLabel = "com.newlifex.staragent";

struct CommandResult
{
    int Status = -1;
    std::string Output;
};

CommandResult RunCapture(const std::string& command)
{
    CommandResult result;
    FILE* pipe = ::popen(command.c_str(), "r");
    if (pipe == nullptr) return result;
    char buffer[512];
    while (std::fgets(buffer, sizeof(buffer), pipe) != nullptr) result.Output += buffer;
    const int status = ::pclose(pipe);
    result.Status = status;
    return result;
}

bool RunSilent(const std::string& command)
{
    return std::system(command.c_str()) == 0;
}

bool WriteFile(const std::string& path, const std::string& content, mode_t mode)
{
    std::ofstream stream(path, std::ios::binary | std::ios::trunc);
    if (!stream) return false;
    stream << content;
    stream.close();
    if (!stream) return false;
    ::chmod(path.c_str(), mode);
    return true;
}

bool IsOpenWrt()
{
    return ::access("/etc/rc.common", F_OK) == 0 && ::access("/sbin/procd", F_OK) == 0;
}

std::string Quote(const std::string& value)
{
    std::string result = "\"";
    for (const char c : value)
    {
        if (c == '"' || c == '\\') result += '\\';
        result += c;
    }
    result += '"';
    return result;
}

std::string XmlEscape(const std::string& value)
{
    std::string result;
    for (const char c : value)
    {
        switch (c)
        {
        case '&':
            result += "&amp;";
            break;
        case '<':
            result += "&lt;";
            break;
        case '>':
            result += "&gt;";
            break;
        default:
            result += c;
            break;
        }
    }
    return result;
}

std::string SystemdUnit(const InstallOptions& options)
{
    const auto exe = platform::ExecutablePath();
    std::ostringstream unit;
    unit << "[Unit]\n"
         << "Description=StarAgent Native Agent (C++)\n"
         << "After=network-online.target\n"
         << "Wants=network-online.target\n\n"
         << "[Service]\n"
         << "Type=simple\n"
         << "ExecStart=" << Quote(exe) << " run --config " << Quote(options.ConfigPath) << "\n"
         << "Restart=always\n"
         << "RestartSec=5\n";
    if (!options.ServiceUser.empty()) unit << "User=" << options.ServiceUser << "\n";
    unit << "\n[Install]\nWantedBy=multi-user.target\n";
    return unit.str();
}

std::string OpenWrtScript(const InstallOptions& options)
{
    const auto exe = platform::ExecutablePath();
    std::ostringstream script;
    script << "#!/bin/sh /etc/rc.common\n"
           << "# StarAgent Native Agent (C++)\n"
           << "USE_PROCD=1\nSTART=95\nSTOP=10\n\n"
           << "start_service() {\n"
           << "  procd_open_instance\n"
           << "  procd_set_param command " << Quote(exe) << " run --config " << Quote(options.ConfigPath) << "\n"
           << "  procd_set_param respawn 3600 5 5\n"
           << "  procd_set_param stdout 1\n"
           << "  procd_set_param stderr 1\n"
           << "  procd_close_instance\n"
           << "}\n";
    return script.str();
}

std::string LaunchdPlist(const InstallOptions& options)
{
    const auto exe = platform::ExecutablePath();
    const auto log_dir = options.DataDir.empty() ? platform::DefaultDataDir() : options.DataDir;
    std::ostringstream plist;
    plist << "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
          << "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" "
             "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
          << "<plist version=\"1.0\">\n<dict>\n"
          << "  <key>Label</key><string>" << kLaunchdLabel << "</string>\n"
          << "  <key>ProgramArguments</key><array>\n"
          << "    <string>" << XmlEscape(exe) << "</string>\n"
          << "    <string>run</string>\n"
          << "    <string>--config</string>\n"
          << "    <string>" << XmlEscape(options.ConfigPath) << "</string>\n"
          << "  </array>\n"
          << "  <key>RunAtLoad</key><true/>\n"
          << "  <key>KeepAlive</key><true/>\n"
          << "  <key>StandardOutPath</key><string>" << XmlEscape(log_dir + "/logs/launchd.out.log")
          << "</string>\n"
          << "  <key>StandardErrorPath</key><string>" << XmlEscape(log_dir + "/logs/launchd.err.log")
          << "</string>\n"
          << "</dict>\n</plist>\n";
    return plist.str();
}

std::string LaunchdPath()
{
    if (::geteuid() == 0) return std::string("/Library/LaunchDaemons/") + kLaunchdLabel + ".plist";
    return platform::HomeDir() + "/Library/LaunchAgents/" + kLaunchdLabel + ".plist";
}

} // namespace

Result Install(const InstallOptions& options)
{
#if defined(STARAGENT_MACOS)
    const auto path = LaunchdPath();
    const auto directory = path.substr(0, path.find_last_of('/'));
    ::mkdir(directory.c_str(), 0755);
    if (!WriteFile(path, LaunchdPlist(options), 0644))
    {
        return {false, "write launchd plist failed: " + path};
    }
    RunSilent("launchctl unload " + Quote(path) + " >/dev/null 2>&1");
    const bool loaded = RunSilent("launchctl load -w " + Quote(path));
    return {true, "launchd plist installed at " + path + (loaded ? "" : " (load failed; try manually)")};
#else
    if (::geteuid() != 0) return {false, "installing the system service requires root"};

    if (IsOpenWrt())
    {
        const std::string path = "/etc/init.d/staragent";
        if (!WriteFile(path, OpenWrtScript(options), 0755))
        {
            return {false, "write init script failed: " + path};
        }
        RunSilent("/etc/init.d/staragent enable >/dev/null 2>&1");
        return {true, "OpenWrt procd init script installed at " + path};
    }

    const std::string path = "/etc/systemd/system/staragent.service";
    if (!WriteFile(path, SystemdUnit(options), 0644))
    {
        return {false, "write systemd unit failed: " + path};
    }
    RunSilent("systemctl daemon-reload >/dev/null 2>&1");
    const bool enabled = RunSilent("systemctl enable staragent >/dev/null 2>&1");
    return {true, "systemd unit installed at " + path + (enabled ? "" : " (enable failed)")};
#endif
}

Result Uninstall()
{
#if defined(STARAGENT_MACOS)
    const auto path = LaunchdPath();
    RunSilent("launchctl unload " + Quote(path) + " >/dev/null 2>&1");
    ::unlink(path.c_str());
    return {true, "launchd plist removed"};
#else
    if (::geteuid() != 0) return {false, "uninstalling the system service requires root"};
    if (IsOpenWrt())
    {
        RunSilent("/etc/init.d/staragent stop >/dev/null 2>&1");
        RunSilent("/etc/init.d/staragent disable >/dev/null 2>&1");
        ::unlink("/etc/init.d/staragent");
        return {true, "OpenWrt init script removed"};
    }
    RunSilent("systemctl disable --now staragent >/dev/null 2>&1");
    ::unlink("/etc/systemd/system/staragent.service");
    RunSilent("systemctl daemon-reload >/dev/null 2>&1");
    return {true, "systemd unit removed"};
#endif
}

Result Start()
{
#if defined(STARAGENT_MACOS)
    const auto path = LaunchdPath();
    if (::access(path.c_str(), F_OK) != 0) return {false, "service is not installed"};
    if (!RunSilent("launchctl load -w " + Quote(path)))
    {
        return {false, "launchctl load failed"};
    }
    return {true, "StarAgent service started"};
#else
    if (IsOpenWrt())
    {
        if (!RunSilent("/etc/init.d/staragent start")) return {false, "init script start failed"};
        return {true, "StarAgent service started"};
    }
    if (!RunSilent("systemctl start staragent"))
    {
        return {false, "systemctl start failed (is the unit installed?)"};
    }
    return {true, "StarAgent service started"};
#endif
}

Result Stop()
{
#if defined(STARAGENT_MACOS)
    const auto path = LaunchdPath();
    if (!RunSilent("launchctl unload " + Quote(path)))
    {
        return {false, "launchctl unload failed"};
    }
    return {true, "StarAgent service stopped"};
#else
    if (IsOpenWrt())
    {
        if (!RunSilent("/etc/init.d/staragent stop")) return {false, "init script stop failed"};
        return {true, "StarAgent service stopped"};
    }
    if (!RunSilent("systemctl stop staragent"))
    {
        return {false, "systemctl stop failed (is the unit installed?)"};
    }
    return {true, "StarAgent service stopped"};
#endif
}

std::string Status()
{
#if defined(STARAGENT_MACOS)
    const auto result = RunCapture("launchctl list 2>/dev/null | grep " + std::string(kLaunchdLabel));
    return result.Status == 0 ? "running" : "not loaded";
#else
    if (IsOpenWrt())
    {
        const auto result = RunCapture("/etc/init.d/staragent status 2>&1");
        return Trim(result.Output).empty() ? (result.Status == 0 ? "running" : "stopped") : Trim(result.Output);
    }
    const auto result = RunCapture("systemctl is-active staragent 2>/dev/null");
    const auto text = Trim(result.Output);
    return text.empty() ? "unknown (systemd not available?)" : text;
#endif
}

int RunAsService(const std::function<int()>&, const std::function<void()>&) { return -1; }

} // namespace staragent::service
