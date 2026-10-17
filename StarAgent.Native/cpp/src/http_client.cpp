// SPDX-License-Identifier: MIT
#include "http_client.hpp"

#include "util.hpp"

#include <algorithm>
#include <cctype>
#include <cstdio>
#include <sstream>

#include "httplib.h"

#ifndef STARAGENT_VERSION
#define STARAGENT_VERSION "1.0.2026.1017"
#endif

namespace staragent {
namespace {

std::string TruncateForError(const std::string& value, std::size_t limit = 512)
{
    if (value.size() <= limit) return value;
    return value.substr(0, limit) + "...";
}

std::string PathWithPrefix(const ParsedUrl& url, const std::string& path)
{
    return url.PathPrefix + path;
}

} // namespace

std::string ParsedUrl::Authority() const { return Host; }

std::string ParsedUrl::Base() const { return Scheme + "://" + Host + PathPrefix; }

ParsedUrl ParseServerUrl(const std::string& server)
{
    const auto trimmed = Trim(server);
    const auto scheme_end = trimmed.find("://");
    if (scheme_end == std::string::npos) throw std::runtime_error("invalid server url: " + server);

    ParsedUrl result;
    result.Scheme = ToLower(trimmed.substr(0, scheme_end));
    if (result.Scheme != "http" && result.Scheme != "https")
    {
        throw std::runtime_error("unsupported server scheme: " + result.Scheme);
    }

    const auto rest = trimmed.substr(scheme_end + 3);
    const auto slash = rest.find('/');
    const auto authority = slash == std::string::npos ? rest : rest.substr(0, slash);
    if (authority.empty()) throw std::runtime_error("invalid server url: " + server);

    result.Host = authority;
    result.PathPrefix = slash == std::string::npos ? "" : rest.substr(slash);
    while (!result.PathPrefix.empty() && result.PathPrefix.back() == '/') result.PathPrefix.pop_back();

    // Extract the port for completeness. IPv6 literals are bracketed.
    std::string port_text;
    if (!authority.empty() && authority.front() == '[')
    {
        const auto close = authority.find(']');
        if (close != std::string::npos && close + 1 < authority.size() && authority[close + 1] == ':')
        {
            port_text = authority.substr(close + 2);
        }
    }
    else
    {
        const auto colon = authority.rfind(':');
        if (colon != std::string::npos) port_text = authority.substr(colon + 1);
    }
    if (!port_text.empty()) result.Port = std::atoi(port_text.c_str());
    if (result.Port == 0) result.Port = result.Scheme == "https" ? 443 : 80;

    return result;
}

std::string BuildNotifyUrl(const std::string& server)
{
    auto url = ParseServerUrl(server);
    const std::string scheme = url.Scheme == "https" ? "wss" : "ws";
    return scheme + "://" + url.Host + url.PathPrefix + "/node/notify";
}

std::string RedactSecrets(const std::string& text)
{
    std::string result = text;
    auto lower = ToLower(result);

    const char* keys[] = {"\"secret\"", "\"token\""};
    for (const char* key : keys)
    {
        std::size_t search = 0;
        while (true)
        {
            const auto position = lower.find(key, search);
            if (position == std::string::npos) break;
            const auto colon = lower.find(':', position + std::char_traits<char>::length(key));
            if (colon == std::string::npos) break;
            auto value_start = colon + 1;
            while (value_start < result.size() && std::isspace(static_cast<unsigned char>(result[value_start])))
            {
                value_start++;
            }
            if (value_start >= result.size() || result[value_start] != '"')
            {
                search = colon + 1;
                continue;
            }
            auto value_end = value_start + 1;
            while (value_end < result.size())
            {
                if (result[value_end] == '\\')
                {
                    value_end += 2;
                    continue;
                }
                if (result[value_end] == '"') break;
                value_end++;
            }
            if (value_end >= result.size()) break;
            result.replace(value_start + 1, value_end - value_start - 1, "***");
            lower = ToLower(result);
            search = value_start + 4;
        }
    }

    return result;
}

HttpClient::HttpClient(std::string server, bool tls_verify, Logger& logger)
    : server_(std::move(server)), tls_verify_(tls_verify), logger_(logger)
{
    url_ = ParseServerUrl(server_);
}

void HttpClient::SetBaseUrl(const std::string& server)
{
    auto parsed = ParseServerUrl(server);
    std::lock_guard<std::mutex> lock(mutex_);
    server_ = server;
    url_ = parsed;
}

std::string HttpClient::BaseUrl() const
{
    std::lock_guard<std::mutex> lock(mutex_);
    return server_;
}

void HttpClient::SetToken(const std::string& token, int expire_seconds)
{
    std::lock_guard<std::mutex> lock(mutex_);
    token_ = token;
    if (token.empty() || expire_seconds <= 0)
    {
        token_has_expiry_ = false;
        token_expires_ = {};
        return;
    }
    token_has_expiry_ = true;
    token_expires_ = std::chrono::steady_clock::now() + std::chrono::seconds(expire_seconds);
}

std::string HttpClient::Token() const
{
    std::lock_guard<std::mutex> lock(mutex_);
    return token_;
}

void HttpClient::ClearToken()
{
    std::lock_guard<std::mutex> lock(mutex_);
    token_.clear();
    token_has_expiry_ = false;
}

bool HttpClient::TokenExpired() const
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (token_.empty()) return true;
    if (!token_has_expiry_) return false;
    return std::chrono::steady_clock::now() >= token_expires_ - std::chrono::seconds(60);
}

void HttpClient::SetOnUnauthorized(std::function<void()> callback)
{
    std::lock_guard<std::mutex> lock(mutex_);
    on_unauthorized_ = std::move(callback);
}

HttpClient::RawResponse HttpClient::SendOnce(const std::string& method, const std::string& path,
                                             const std::string& body, bool auth)
{
    std::string token;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        token = token_;
    }

    httplib::Client client(url_.Base());
    if (!client.is_valid())
    {
        throw HttpError(0, "invalid server URL or unsupported scheme (https requires an OpenSSL build)");
    }

    client.set_connection_timeout(ConnectTimeoutSeconds, 0);
    client.set_read_timeout(RequestTimeoutSeconds, 0);
    client.set_write_timeout(RequestTimeoutSeconds, 0);
    client.set_follow_location(false);
    client.set_keep_alive(false);

#ifdef CPPHTTPLIB_OPENSSL_SUPPORT
    if (url_.Scheme == "https" && !tls_verify_) client.enable_server_certificate_verification(false);
#endif

    httplib::Headers headers;
    headers.emplace("Accept", "application/json");
    headers.emplace("User-Agent", std::string("StarAgent-Cpp/") + STARAGENT_VERSION);
    if (auth && !token.empty()) headers.emplace("Authorization", "Bearer " + token);

    httplib::Result result;
    if (method == "GET")
    {
        result = client.Get(path, headers);
    }
    else
    {
        result = client.Post(path, headers, body, "application/json");
    }

    if (!result)
    {
        throw HttpError(0, "request failed: " + httplib::to_string(result.error()));
    }

    return RawResponse{result->status, result->body};
}

HttpClient::RawResponse HttpClient::Send(const std::string& method, const std::string& path,
                                         const std::string& body, bool auth)
{
    auto response = SendOnce(method, path, body, auth);
    if (auth && response.Status == 401)
    {
        logger_.Warn("http 401 unauthorized, attempting re-login");
        ClearToken();

        std::function<void()> callback;
        {
            std::lock_guard<std::mutex> lock(mutex_);
            callback = on_unauthorized_;
        }
        if (callback)
        {
            try
            {
                callback();
            }
            catch (const std::exception& ex)
            {
                throw HttpError(401, std::string("re-login failed: ") + ex.what());
            }
        }

        if (Token().empty()) throw HttpError(401, "unauthorized and re-login did not produce a token");
        response = SendOnce(method, path, body, auth);
    }
    return response;
}

Json HttpClient::Decode(const RawResponse& response, const std::string& operation) const
{
    if (Trim(response.Body).empty()) throw ProtocolError(operation + ": empty response body");
    Json value = Json::parse(response.Body, nullptr, false);
    if (value.is_discarded()) throw ProtocolError(operation + ": invalid JSON response");
    return UnwrapResponse(value);
}

void HttpClient::EnsureSuccess(const RawResponse& response, const std::string& operation) const
{
    if (response.Status >= 200 && response.Status < 300) return;
    throw HttpError(response.Status, operation + " failed: http " + std::to_string(response.Status) + " " +
                                        RedactSecrets(TruncateForError(response.Body)));
}

LoginResponse HttpClient::Login(const LoginInfo& request)
{
    const auto path = PathWithPrefix(url_, "/node/login");
    const auto response = Send("POST", path, ToJson(request).dump(), false);
    EnsureSuccess(response, "login");
    auto result = ParseLoginResponse(Decode(response, "login"));
    if (!result.Token.empty()) SetToken(result.Token, result.Expire);
    return result;
}

LoginResponse HttpClient::Logout(const std::string& reason)
{
    auto path = PathWithPrefix(url_, "/node/logout");
    if (!reason.empty()) path += "?reason=" + UrlEncode(reason);
    const auto response = Send("POST", path, "", true);
    EnsureSuccess(response, "logout");
    try
    {
        return ParseLoginResponse(Decode(response, "logout"));
    }
    catch (...)
    {
        return {};
    }
}

PingResponse HttpClient::Ping(const PingInfo& request)
{
    const auto path = PathWithPrefix(url_, "/node/ping");
    const auto response = Send("POST", path, ToJson(request).dump(), true);
    EnsureSuccess(response, "ping");
    auto result = ParsePingResponse(Decode(response, "ping"));
    if (!result.Token.empty()) SetToken(result.Token, 0);
    return result;
}

int HttpClient::PostEvents(const std::vector<EventModel>& events)
{
    Json body = Json::array();
    for (const auto& event : events) body.push_back(ToJson(event));

    const auto path = PathWithPrefix(url_, "/node/postevents");
    const auto response = Send("POST", path, body.dump(), true);
    EnsureSuccess(response, "post events");
    return ParseEventCount(Decode(response, "post events"));
}

void HttpClient::CommandReply(const CommandReplyModel& reply)
{
    const auto path = PathWithPrefix(url_, "/node/commandreply");
    const auto response = Send("POST", path, ToJson(reply).dump(), true);
    EnsureSuccess(response, "command reply");
}

std::optional<UpgradeInfo> HttpClient::Upgrade(const std::string& channel)
{
    const auto effective = channel.empty() ? std::string("Release") : channel;
    const auto path = PathWithPrefix(url_, "/node/upgrade") + "?channel=" + UrlEncode(effective);
    const auto response = Send("POST", path, "", true);
    EnsureSuccess(response, "upgrade");

    const auto trimmed = Trim(response.Body);
    if (trimmed.empty() || trimmed == "null") return std::nullopt;

    const auto value = Decode(response, "upgrade");
    auto info = ParseUpgradeInfo(value);
    if (Trim(info.Version).empty()) return std::nullopt;
    return info;
}

} // namespace staragent
