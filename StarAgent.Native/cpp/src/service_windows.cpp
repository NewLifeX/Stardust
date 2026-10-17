// SPDX-License-Identifier: MIT
#include "service.hpp"

#include "platform.hpp"
#include "util.hpp"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <thread>

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

namespace staragent::service {
namespace {

constexpr wchar_t kServiceName[] = L"StarAgentNative";
constexpr wchar_t kServiceDisplayName[] = L"StarAgent Native Agent";

SERVICE_STATUS_HANDLE g_status_handle = nullptr;
SERVICE_STATUS g_status{};
std::function<int()> g_body;
std::function<void()> g_request_stop;
HANDLE g_stop_event = nullptr;
std::atomic<int> g_exit_code{0};

std::wstring WideFromUtf8(const std::string& value)
{
    if (value.empty()) return {};
    const int size = ::MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, nullptr, 0);
    if (size <= 0) return {};
    std::wstring result(static_cast<std::size_t>(size - 1), L'\0');
    ::MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, result.data(), size);
    return result;
}

std::string Utf8FromWide(const std::wstring& value)
{
    if (value.empty()) return {};
    const int size = ::WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (size <= 0) return {};
    std::string result(static_cast<std::size_t>(size - 1), '\0');
    ::WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, result.data(), size, nullptr, nullptr);
    return result;
}

std::string LastErrorText()
{
    return "Win32 error " + std::to_string(::GetLastError());
}

void ReportStatus(DWORD state, DWORD exit_code = NO_ERROR, DWORD wait_hint = 0)
{
    g_status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
    g_status.dwCurrentState = state;
    g_status.dwWin32ExitCode = exit_code;
    g_status.dwWaitHint = wait_hint;
    g_status.dwControlsAccepted = state == SERVICE_RUNNING ? SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN : 0;
    g_status.dwCheckPoint = 0;
    if (g_status_handle != nullptr) ::SetServiceStatus(g_status_handle, &g_status);
}

DWORD WINAPI ControlHandler(DWORD control, DWORD, void*, void*)
{
    if (control == SERVICE_CONTROL_STOP || control == SERVICE_CONTROL_SHUTDOWN)
    {
        ReportStatus(SERVICE_STOP_PENDING, NO_ERROR, 5000);
        if (g_request_stop) g_request_stop();
        if (g_stop_event != nullptr) ::SetEvent(g_stop_event);
        return NO_ERROR;
    }
    if (control == SERVICE_CONTROL_INTERROGATE)
    {
        ::SetServiceStatus(g_status_handle, &g_status);
        return NO_ERROR;
    }
    return ERROR_CALL_NOT_IMPLEMENTED;
}

void WINAPI ServiceMain(DWORD, LPWSTR*)
{
    g_status_handle = ::RegisterServiceCtrlHandlerExW(kServiceName, ControlHandler, nullptr);
    if (g_status_handle == nullptr) return;
    ReportStatus(SERVICE_START_PENDING, NO_ERROR, 5000);

    g_stop_event = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
    std::thread worker([]() {
        const int code = g_body ? g_body() : 1;
        g_exit_code = code;
        if (g_stop_event != nullptr) ::SetEvent(g_stop_event);
    });

    ReportStatus(SERVICE_RUNNING);
    if (g_stop_event != nullptr) ::WaitForSingleObject(g_stop_event, INFINITE);
    ReportStatus(SERVICE_STOP_PENDING, NO_ERROR, 5000);
    if (worker.joinable()) worker.join();
    const int exit_code = g_exit_code.load();
    g_status.dwWin32ExitCode = exit_code == 0 ? NO_ERROR : ERROR_SERVICE_SPECIFIC_ERROR;
    g_status.dwServiceSpecificExitCode = static_cast<DWORD>(exit_code);
    ReportStatus(SERVICE_STOPPED, g_status.dwWin32ExitCode);
}

SC_HANDLE OpenManager()
{
    return ::OpenSCManagerW(nullptr, nullptr, SC_MANAGER_ALL_ACCESS);
}

std::string BuildBinaryPath(const std::string& config_path)
{
    const auto exe = platform::ExecutablePath();
    return "\"" + exe + "\" run --config \"" + config_path + "\" --service";
}

Result WaitForState(SC_HANDLE service, DWORD desired, int timeout_ms)
{
    const auto deadline = SteadyMsNow() + timeout_ms;
    while (SteadyMsNow() < deadline)
    {
        SERVICE_STATUS_PROCESS status{};
        DWORD needed = 0;
        if (!::QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<LPBYTE>(&status),
                                    sizeof(status), &needed))
        {
            return {false, "QueryServiceStatusEx failed: " + LastErrorText()};
        }
        if (status.dwCurrentState == desired) return {true, {}};
        std::this_thread::sleep_for(std::chrono::milliseconds(200));
    }
    return {false, "service did not reach the requested state"};
}

} // namespace

Result Install(const InstallOptions& options)
{
    const SC_HANDLE manager = OpenManager();
    if (manager == nullptr) return {false, "OpenSCManager failed (administrator required): " + LastErrorText()};

    const auto binary_path = WideFromUtf8(BuildBinaryPath(options.ConfigPath));
    SC_HANDLE service = ::OpenServiceW(manager, kServiceName, SERVICE_ALL_ACCESS);
    if (service != nullptr)
    {
        const BOOL changed = ::ChangeServiceConfigW(service, SERVICE_WIN32_OWN_PROCESS, SERVICE_AUTO_START,
                                                    SERVICE_ERROR_NORMAL, binary_path.c_str(), nullptr, nullptr,
                                                    nullptr, nullptr, nullptr, kServiceDisplayName);
        ::CloseServiceHandle(service);
        ::CloseServiceHandle(manager);
        if (!changed) return {false, "ChangeServiceConfig failed: " + LastErrorText()};
        return {true, "StarAgent service updated"};
    }

    service = ::CreateServiceW(manager, kServiceName, kServiceDisplayName, SERVICE_ALL_ACCESS,
                               SERVICE_WIN32_OWN_PROCESS, SERVICE_AUTO_START, SERVICE_ERROR_NORMAL,
                               binary_path.c_str(), nullptr, nullptr, nullptr, nullptr, nullptr);
    if (service == nullptr)
    {
        const auto error = ::GetLastError();
        ::CloseServiceHandle(manager);
        return {false, "CreateService failed (administrator required): Win32 error " + std::to_string(error)};
    }

    SERVICE_DESCRIPTIONW description{};
    description.lpDescription = const_cast<LPWSTR>(L"StarAgent native C++ agent");
    ::ChangeServiceConfig2W(service, SERVICE_CONFIG_DESCRIPTION, &description);
    ::CloseServiceHandle(service);
    ::CloseServiceHandle(manager);
    return {true, "StarAgent service installed"};
}

Result Uninstall()
{
    const SC_HANDLE manager = OpenManager();
    if (manager == nullptr) return {false, "OpenSCManager failed (administrator required): " + LastErrorText()};

    SC_HANDLE service = ::OpenServiceW(manager, kServiceName, SERVICE_STOP | DELETE | SERVICE_QUERY_STATUS);
    if (service == nullptr)
    {
        const auto error = ::GetLastError();
        ::CloseServiceHandle(manager);
        if (error == ERROR_SERVICE_DOES_NOT_EXIST) return {true, "service is not installed"};
        return {false, "OpenService failed: Win32 error " + std::to_string(error)};
    }

    SERVICE_STATUS status{};
    ::ControlService(service, SERVICE_CONTROL_STOP, &status);
    WaitForState(service, SERVICE_STOPPED, 30000);
    const BOOL deleted = ::DeleteService(service);
    ::CloseServiceHandle(service);
    ::CloseServiceHandle(manager);
    if (!deleted) return {false, "DeleteService failed: " + LastErrorText()};
    return {true, "StarAgent service removed"};
}

Result Start()
{
    const SC_HANDLE manager = OpenManager();
    if (manager == nullptr) return {false, "OpenSCManager failed (administrator required): " + LastErrorText()};
    SC_HANDLE service = ::OpenServiceW(manager, kServiceName, SERVICE_START | SERVICE_QUERY_STATUS);
    if (service == nullptr)
    {
        const auto error = ::GetLastError();
        ::CloseServiceHandle(manager);
        return {false, "OpenService failed: Win32 error " + std::to_string(error)};
    }
    const BOOL started = ::StartServiceW(service, 0, nullptr);
    const auto error = ::GetLastError();
    const auto result = WaitForState(service, SERVICE_RUNNING, 30000);
    ::CloseServiceHandle(service);
    ::CloseServiceHandle(manager);
    if (!started && error != ERROR_SERVICE_ALREADY_RUNNING)
    {
        return {false, "StartService failed: Win32 error " + std::to_string(error)};
    }
    if (!result.Success) return result;
    return {true, "StarAgent service started"};
}

Result Stop()
{
    const SC_HANDLE manager = OpenManager();
    if (manager == nullptr) return {false, "OpenSCManager failed (administrator required): " + LastErrorText()};
    SC_HANDLE service = ::OpenServiceW(manager, kServiceName, SERVICE_STOP | SERVICE_QUERY_STATUS);
    if (service == nullptr)
    {
        const auto error = ::GetLastError();
        ::CloseServiceHandle(manager);
        if (error == ERROR_SERVICE_DOES_NOT_EXIST) return {true, "service is not installed"};
        return {false, "OpenService failed: Win32 error " + std::to_string(error)};
    }
    SERVICE_STATUS status{};
    const BOOL stopped = ::ControlService(service, SERVICE_CONTROL_STOP, &status);
    const auto error = ::GetLastError();
    const auto result = WaitForState(service, SERVICE_STOPPED, 30000);
    ::CloseServiceHandle(service);
    ::CloseServiceHandle(manager);
    if (!stopped && error != ERROR_SERVICE_NOT_ACTIVE)
    {
        return {false, "ControlService failed: Win32 error " + std::to_string(error)};
    }
    if (!result.Success) return result;
    return {true, "StarAgent service stopped"};
}

std::string Status()
{
    const SC_HANDLE manager = ::OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT);
    if (manager == nullptr) return "OpenSCManager failed (administrator required)";
    SC_HANDLE service = ::OpenServiceW(manager, kServiceName, SERVICE_QUERY_STATUS);
    if (service == nullptr)
    {
        ::CloseServiceHandle(manager);
        return "not installed";
    }
    SERVICE_STATUS_PROCESS status{};
    DWORD needed = 0;
    std::string text = "unknown";
    if (::QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<LPBYTE>(&status), sizeof(status),
                               &needed))
    {
        switch (status.dwCurrentState)
        {
        case SERVICE_STOPPED:
            text = "stopped";
            break;
        case SERVICE_START_PENDING:
            text = "start pending";
            break;
        case SERVICE_STOP_PENDING:
            text = "stop pending";
            break;
        case SERVICE_RUNNING:
            text = "running (pid " + std::to_string(status.dwProcessId) + ")";
            break;
        case SERVICE_PAUSED:
            text = "paused";
            break;
        default:
            text = "state " + std::to_string(status.dwCurrentState);
            break;
        }
    }
    ::CloseServiceHandle(service);
    ::CloseServiceHandle(manager);
    return text;
}

int RunAsService(const std::function<int()>& body, const std::function<void()>& request_stop)
{
    g_body = body;
    g_request_stop = request_stop;
    SERVICE_TABLE_ENTRYW table[] = {{const_cast<LPWSTR>(kServiceName), ServiceMain}, {nullptr, nullptr}};
    if (!::StartServiceCtrlDispatcherW(table))
    {
        if (::GetLastError() == ERROR_FAILED_SERVICE_CONTROLLER_CONNECT) return -1;
        return 1;
    }
    return 0;
}

} // namespace staragent::service
