// SPDX-License-Identifier: MIT
#include "console.hpp"

#include <ostream>
#include <streambuf>
#include <string>

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#endif

namespace staragent {
namespace {

#ifdef _WIN32

// 输出被重定向到文件或管道时保持 UTF-8 字节，不做编码转换
std::streamsize WriteRaw(std::streambuf* fallback, const char* text, std::streamsize length)
{
    if (fallback == nullptr) return length;

    const auto written = fallback->sputn(text, length);
    return written < 0 ? 0 : written;
}

class Utf8ConsoleBuffer : public std::streambuf
{
public:
    Utf8ConsoleBuffer(std::streambuf* fallback, bool error_stream)
        : fallback_(fallback), handle_(::GetStdHandle(error_stream ? STD_ERROR_HANDLE : STD_OUTPUT_HANDLE))
    {
    }

protected:
    int_type overflow(int_type ch) override
    {
        if (traits_type::eq_int_type(ch, traits_type::eof())) return traits_type::not_eof(ch);

        const auto value = traits_type::to_char_type(ch);
        return xsputn(&value, 1) == 1 ? ch : traits_type::eof();
    }

    std::streamsize xsputn(const char* text, std::streamsize length) override
    {
        if (length <= 0) return 0;

        if (!IsConsole())
        {
            pending_.clear();
            return WriteRaw(fallback_, text, length);
        }

        pending_.append(text, static_cast<size_t>(length));

        // 一次写入可能截断多字节字符（例如逐字节 put），只转换完整的前缀，残缺部分留给下一次
        const auto complete = CompleteLength(pending_);
        if (complete > 0) Emit(pending_.substr(0, complete));

        return length;
    }

    int sync() override
    {
        // 退出前把残留的残缺字节也吐出去，宁可显示替换字符也不丢内容
        if (!pending_.empty())
        {
            Emit(pending_);
            pending_.clear();
        }
        if (fallback_ != nullptr) fallback_->pubsync();
        return 0;
    }

private:
    bool IsConsole() const
    {
        DWORD mode = 0;
        return handle_ != nullptr && handle_ != INVALID_HANDLE_VALUE && ::GetConsoleMode(handle_, &mode) != 0;
    }

    void Emit(const std::string& text)
    {
        const auto size = ::MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0);
        if (size > 0)
        {
            wide_.resize(static_cast<size_t>(size));
            ::MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), wide_.data(), size);

            DWORD written = 0;
            if (::WriteConsoleW(handle_, wide_.data(), static_cast<DWORD>(wide_.size()), &written, nullptr))
            {
                pending_.erase(0, text.size());
                return;
            }
        }

        // 转换或写入失败：原样写字节，保证内容不丢
        WriteRaw(fallback_, text.data(), static_cast<std::streamsize>(text.size()));
        pending_.erase(0, text.size());
    }

    // 返回 text 中可以完整解码的字节数，末尾残缺的多字节序列留给下一次写入
    static size_t CompleteLength(const std::string& text)
    {
        const auto size = text.size();
        // 单个 UTF-8 字符最多 4 字节，回看 3 个字节就足以判断末尾是否残缺
        for (size_t back = 1; back <= 3 && back <= size; back++)
        {
            const auto value = static_cast<unsigned char>(text[size - back]);
            if ((value & 0xC0) == 0x80) continue; // 后续字节，继续往前找首字节
            if (value < 0x80) return size;        // ASCII，末尾完整

            const size_t need = value >= 0xF0 ? 4 : value >= 0xE0 ? 3 : 2;
            return back >= need ? size : size - back;
        }
        return size;
    }

    std::streambuf* fallback_;
    HANDLE handle_;
    std::string pending_;
    std::wstring wide_;
};

#endif

} // namespace

void AttachUtf8Console(std::ostream& stream, bool error_stream)
{
#ifdef _WIN32
    // 故意不释放：流的生命周期与进程一致，提前释放会让退出阶段的输出访问已释放缓冲区
    stream.rdbuf(new Utf8ConsoleBuffer(stream.rdbuf(), error_stream));
#else
    (void)stream;
    (void)error_stream;
#endif
}

} // namespace staragent
