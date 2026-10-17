// SPDX-License-Identifier: MIT
#pragma once

#include "logging.hpp"
#include "protocol.hpp"

#include <chrono>
#include <functional>
#include <mutex>
#include <optional>
#include <stdexcept>
#include <string>
#include <vector>

namespace staragent {

struct HttpError : std::runtime_error
{
    HttpError(int code, const std::string& message) : std::runtime_error(message), StatusCode(code) {}
    int StatusCode;
};

struct ParsedUrl
{
    std::string Scheme;
    std::string Host;
    int Port = 0;
    std::string PathPrefix;

    std::string Authority() const;
    std::string Base() const;
};

ParsedUrl ParseServerUrl(const std::string& server);
std::string BuildNotifyUrl(const std::string& server);

class HttpClient
{
public:
    HttpClient(std::string server, bool tls_verify, Logger& logger);
    ~HttpClient() = default;

    void SetBaseUrl(const std::string& server);
    std::string BaseUrl() const;

    void SetToken(const std::string& token, int expire_seconds);
    std::string Token() const;
    void ClearToken();
    bool TokenExpired() const;

    void SetOnUnauthorized(std::function<void()> callback);

    LoginResponse Login(const LoginInfo& request);
    LoginResponse Logout(const std::string& reason);
    PingResponse Ping(const PingInfo& request);
    int PostEvents(const std::vector<EventModel>& events);
    void CommandReply(const CommandReplyModel& reply);
    std::optional<UpgradeInfo> Upgrade(const std::string& channel);

    static constexpr int RequestTimeoutSeconds = 15;
    static constexpr int ConnectTimeoutSeconds = 5;

private:
    struct RawResponse
    {
        int Status = 0;
        std::string Body;
    };

    RawResponse SendOnce(const std::string& method, const std::string& path, const std::string& body, bool auth);
    RawResponse Send(const std::string& method, const std::string& path, const std::string& body, bool auth);
    Json Decode(const RawResponse& response, const std::string& operation) const;
    void EnsureSuccess(const RawResponse& response, const std::string& operation) const;

    mutable std::mutex mutex_;
    std::string server_;
    ParsedUrl url_;
    bool tls_verify_ = true;
    Logger& logger_;

    std::string token_;
    std::chrono::steady_clock::time_point token_expires_{};
    bool token_has_expiry_ = false;
    std::function<void()> on_unauthorized_;
};

std::string RedactSecrets(const std::string& text);

} // namespace staragent
