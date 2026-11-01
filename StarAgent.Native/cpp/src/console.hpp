// SPDX-License-Identifier: MIT
#pragma once

#include <iosfwd>

namespace staragent {

/// <summary>让标准输出正确显示中文</summary>
/// <remarks>
/// Windows 控制台会按当前代码页（简体中文为 936/GBK）解码收到的字节，程序内部的字符串是 UTF-8，
/// 直接写出去就成了乱码。此函数把流的缓冲区换成“检测到控制台就转宽字符输出、重定向到文件或管道
/// 时仍写 UTF-8 字节”的实现；POSIX 终端本来就是 UTF-8，不做处理。
/// </remarks>
/// <param name="stream">要替换缓冲区的输出流，通常是 std::cout 或 std::cerr</param>
/// <param name="error_stream">true 表示该流对应标准错误句柄，false 表示标准输出句柄</param>
void AttachUtf8Console(std::ostream& stream, bool error_stream);

} // namespace staragent
