// SPDX-License-Identifier: MIT
// Linux/macOS 共用的内部实现声明，仅供 platform_*.cpp 使用。
#pragma once

#include <cstddef>
#include <string>

namespace staragent::platform::internal {

// 读取系统伪文件（/proc、/sys、/etc 下的文本），超过 max 字节截断。
std::string ReadSysFile(const std::string& path, std::size_t max);
// 读取系统伪文件并 Trim 两端空白。
std::string ReadSysTrim(const std::string& path);
// 按前缀表排除常见虚拟网卡（lo/docker/veth/tun/...）。
bool IsVirtualInterface(const std::string& name);
// MAC 地址格式化为大写、'-' 分隔。
std::string FormatMac(const unsigned char* data, std::size_t length);
// sockaddr（IPv4/IPv6）转文本；其他族返回空串。
std::string SockaddrToIp(const void* address);
// 清洗硬件标识里的占位值（"To be filled by O.E.M."、全 0 UUID 等）。
std::string CleanId(std::string value);
// 解析 /etc/resolv.conf 的 nameserver 列表，逗号分隔。
std::string DnsServers();

} // namespace staragent::platform::internal
