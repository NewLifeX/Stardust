// SPDX-License-Identifier: MIT
#include "local_api.hpp"

#include "logging.hpp"
#include "util.hpp"

#include <cstring>
#include <vector>

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "ws2_32.lib")
#else
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/select.h>
#include <sys/socket.h>
#include <unistd.h>
#endif

namespace staragent {
namespace {

#ifdef _WIN32
using SocketHandle = SOCKET;
constexpr SocketHandle kInvalidSocket = INVALID_SOCKET;
#else
using SocketHandle = int;
constexpr SocketHandle kInvalidSocket = -1;
#endif
// SRMP 消息种类（Flag 高 2 位）
constexpr int kKindRequest = 0;
constexpr int kKindResponse = 2;
constexpr int kKindError = 3;

constexpr int kFlagJson = 1;

void WriteEncodedInt(std::string& buffer, std::uint32_t value)
{
    // 与 .NET SpanWriter.WriteEncodedInt 一致：7 位一组，低组在前，最高位表示续接
    while (value >= 0x80)
    {
        buffer.push_back(static_cast<char>((value & 0x7F) | 0x80));
        value >>= 7;
    }
    buffer.push_back(static_cast<char>(value & 0x7F));
}

bool ReadEncodedInt(const std::string& body, std::size_t& offset, std::uint32_t& value)
{
    value = 0;
    int shift = 0;
    while (offset < body.size() && shift <= 28)
    {
        const auto current = static_cast<unsigned char>(body[offset++]);
        value |= static_cast<std::uint32_t>(current & 0x7F) << shift;
        if ((current & 0x80) == 0) return true;
        shift += 7;
    }
    return false;
}

void WriteInt32(std::string& buffer, std::uint32_t value)
{
    buffer.push_back(static_cast<char>(value & 0xFF));
    buffer.push_back(static_cast<char>((value >> 8) & 0xFF));
    buffer.push_back(static_cast<char>((value >> 16) & 0xFF));
    buffer.push_back(static_cast<char>((value >> 24) & 0xFF));
}

std::uint32_t ReadInt32(const std::string& buffer, std::size_t offset)
{
    if (offset + 4 > buffer.size()) return 0;
    return static_cast<std::uint32_t>(static_cast<unsigned char>(buffer[offset])) |
           static_cast<std::uint32_t>(static_cast<unsigned char>(buffer[offset + 1])) << 8 |
           static_cast<std::uint32_t>(static_cast<unsigned char>(buffer[offset + 2])) << 16 |
           static_cast<std::uint32_t>(static_cast<unsigned char>(buffer[offset + 3])) << 24;
}

struct SrmpFrame
{
    int kind = kKindRequest;
    int flag = kFlagJson;
    unsigned char sequence = 0;
    std::string body;
};

bool ParseFrame(const char* data, std::size_t size, SrmpFrame& frame)
{
    if (size < 4) return false;
    const auto b0 = static_cast<unsigned char>(data[0]);
    frame.kind = b0 >> 6;
    frame.flag = b0 & 0x3F;
    frame.sequence = static_cast<unsigned char>(data[1]);

    auto length = static_cast<std::size_t>(static_cast<unsigned char>(data[2])) |
                  static_cast<std::size_t>(static_cast<unsigned char>(data[3])) << 8;
    std::size_t header = 4;
    if (length == 0xFFFF)
    {
        if (size < 8) return false;
        length = ReadInt32(std::string(data, 8), 4);
        header = 8;
    }
    if (size < header + length) return false;

    frame.body.assign(data + header, length);
    return true;
}

std::string BuildFrame(int kind, int flag, unsigned char sequence, const std::string& body)
{
    std::string frame;
    if (body.size() < 0xFFFF)
    {
        frame.reserve(4 + body.size());
        frame.push_back(static_cast<char>((flag & 0x3F) | (kind << 6)));
        frame.push_back(static_cast<char>(sequence));
        frame.push_back(static_cast<char>(body.size() & 0xFF));
        frame.push_back(static_cast<char>((body.size() >> 8) & 0xFF));
    }
    else
    {
        frame.reserve(8 + body.size());
        frame.push_back(static_cast<char>((flag & 0x3F) | (kind << 6)));
        frame.push_back(static_cast<char>(sequence));
        frame.push_back(static_cast<char>(0xFF));
        frame.push_back(static_cast<char>(0xFF));
        WriteInt32(frame, static_cast<std::uint32_t>(body.size()));
    }
    frame += body;
    return frame;
}

// Payload：[7位长度][action]{[4字节 code]}[4字节长度][data]
std::string BuildPayload(const std::string& action, int code, const std::string& data)
{
    std::string body;
    WriteEncodedInt(body, static_cast<std::uint32_t>(action.size()));
    body += action;
    if (code != 0)
    {
        WriteInt32(body, static_cast<std::uint32_t>(code));
    }
    WriteInt32(body, static_cast<std::uint32_t>(data.size()));
    body += data;
    return body;
}

bool ParseRequest(const std::string& body, std::string& action, std::string& data)
{
    std::size_t offset = 0;
    std::uint32_t length = 0;
    if (!ReadEncodedInt(body, offset, length)) return false;
    if (offset + length > body.size()) return false;
    action.assign(body, offset, length);
    offset += length;

    data.clear();
    if (offset + 4 > body.size()) return true;
    const auto dataLength = ReadInt32(body, offset);
    offset += 4;
    if (offset + dataLength > body.size()) return true;
    data.assign(body, offset, dataLength);
    return true;
}

} // namespace

LocalApiServer::LocalApiServer(Logger& logger) : logger_(logger) {}

LocalApiServer::~LocalApiServer() { Stop(); }

bool LocalApiServer::Available() { return true; }

bool LocalApiServer::Start(int port, InfoProvider info)
{
    if (port <= 0)
    {
        logger_.Debug("local api disabled (LocalPort=0)");
        return false;
    }

    info_ = std::move(info);
    port_ = port;
    stopping_ = false;

#ifdef _WIN32
    WSADATA wsa{};
    if (::WSAStartup(MAKEWORD(2, 2), &wsa) != 0)
    {
        logger_.Warn("local api WSAStartup failed");
        return false;
    }
    auto socket = ::socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (socket == INVALID_SOCKET)
    {
        logger_.Warn("local api socket failed");
        ::WSACleanup();
        return false;
    }
    socket_ = static_cast<unsigned long long>(socket);
#else
    auto socket = ::socket(AF_INET, SOCK_DGRAM, 0);
    if (socket < 0)
    {
        logger_.Warn("local api socket failed");
        return false;
    }
    socket_ = socket;
    int reuse = 1;
    ::setsockopt(socket, SOL_SOCKET, SO_REUSEADDR, &reuse, sizeof(reuse));
    // 注意：不要设置 SO_REUSEPORT，否则会与已运行的同机 .NET StarAgent 共享端口，
    // 数据报被内核随机分发，探测结果不可预期
#endif

    sockaddr_in address{};
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = htonl(INADDR_ANY);
    address.sin_port = htons(static_cast<unsigned short>(port));
    const auto native = static_cast<SocketHandle>(socket_);
    if (::bind(native, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0)
    {
        logger_.Warn("local api bind " + std::to_string(port) + " failed");
        Stop();
        return false;
    }

    thread_ = std::thread([this]() { Loop(); });
    logger_.Info("local api listening on udp://0.0.0.0:" + std::to_string(port));
    return true;
}

void LocalApiServer::Stop()
{
    stopping_ = true;
    if (thread_.joinable()) thread_.join();

#ifdef _WIN32
    if (socket_ != static_cast<unsigned long long>(-1))
    {
        ::closesocket(static_cast<SOCKET>(socket_));
        socket_ = static_cast<unsigned long long>(-1);
        ::WSACleanup();
    }
#else
    if (socket_ >= 0)
    {
        ::close(socket_);
        socket_ = -1;
    }
#endif
    port_ = 0;
}

void LocalApiServer::Loop()
{
    std::vector<char> buffer(64 * 1024);
    while (!stopping_.load())
    {
        fd_set readSet;
        FD_ZERO(&readSet);
        const auto native = static_cast<SocketHandle>(socket_);
        FD_SET(native, &readSet);

        timeval timeout{};
        timeout.tv_sec = 0;
        timeout.tv_usec = 200 * 1000;
#ifdef _WIN32
        const int ready = ::select(0, &readSet, nullptr, nullptr, &timeout);
#else
        const int ready = ::select(native + 1, &readSet, nullptr, nullptr, &timeout);
#endif
        if (ready <= 0) continue;

        sockaddr_in peer{};
#ifdef _WIN32
        int peerLength = sizeof(peer);
#else
        socklen_t peerLength = sizeof(peer);
#endif
        const auto size = ::recvfrom(native, buffer.data(), static_cast<int>(buffer.size()), 0,
                                     reinterpret_cast<sockaddr*>(&peer), &peerLength);
        if (size <= 0) continue;

        SrmpFrame frame;
        if (!ParseFrame(buffer.data(), static_cast<std::size_t>(size), frame)) continue;

        std::string action;
        std::string data;
        if (!ParseRequest(frame.body, action, data)) continue;

        std::string reply;
        int code = 0;
        try
        {
            reply = Handle(action, data);
        }
        catch (const std::exception& ex)
        {
            code = 500;
            reply = ex.what();
        }
        if (reply.empty() && code == 0)
        {
            code = 404;
            reply = "unsupported action: " + action;
        }

        const auto payload = BuildPayload(action, code, reply);
        const auto response = BuildFrame(code == 0 ? kKindResponse : kKindError, frame.flag, frame.sequence, payload);
        ::sendto(native, response.data(), static_cast<int>(response.size()), 0,
                 reinterpret_cast<const sockaddr*>(&peer), peerLength);
    }
}

std::string LocalApiServer::Handle(const std::string& action, const std::string& json)
{
    (void)json;

    if (EqualsIgnoreCase(action, "Info"))
    {
        return info_ != nullptr ? info_() : std::string();
    }
    if (EqualsIgnoreCase(action, "Ping"))
    {
        return "{\"ServerTime\":" + std::to_string(UnixMsNow()) + "}";
    }
    logger_.Debug("local api unsupported action=" + action);
    return {};
}

} // namespace staragent
