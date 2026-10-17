// SPDX-License-Identifier: MIT
#pragma once

#include <functional>
#include <string>

namespace staragent::service {

struct InstallOptions
{
    std::string ConfigPath;
    std::string InstallDir;
    std::string DataDir;
    std::string ServiceUser;
    std::string Server;
};

struct Result
{
    bool Success = false;
    std::string Message;
};

Result Install(const InstallOptions& options);
Result Uninstall();
Result Start();
Result Stop();
std::string Status();

// Runs body under the platform service manager. Returns -1 when the process
// was not launched by the service manager (the caller should run in console
// mode), otherwise the body's exit code.
int RunAsService(const std::function<int()>& body, const std::function<void()>& request_stop);

} // namespace staragent::service
