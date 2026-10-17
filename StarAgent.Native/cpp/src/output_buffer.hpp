// SPDX-License-Identifier: MIT
#pragma once

#include <cstddef>
#include <mutex>
#include <string>

namespace staragent {

// Keeps the first half and the last half of a byte stream, bounded by max.
class HeadTailBuffer
{
public:
    explicit HeadTailBuffer(std::size_t max = 64 * 1024)
        : head_limit_(max / 2), tail_limit_(max - max / 2)
    {
    }

    void Append(const char* data, std::size_t size)
    {
        std::lock_guard<std::mutex> lock(mutex_);
        const char* cursor = data;
        if (head_.size() < head_limit_)
        {
            const std::size_t room = head_limit_ - head_.size();
            const std::size_t take = size < room ? size : room;
            head_.append(cursor, take);
            cursor += take;
            size -= take;
        }
        if (size > 0)
        {
            tail_.append(cursor, size);
            if (tail_.size() > tail_limit_)
            {
                tail_.erase(0, tail_.size() - tail_limit_);
                truncated_ = true;
            }
        }
    }

    std::string ToString() const
    {
        std::lock_guard<std::mutex> lock(mutex_);
        if (!truncated_) return head_ + tail_;
        return head_ + "\n...<output truncated>...\n" + tail_;
    }

    bool Truncated() const
    {
        std::lock_guard<std::mutex> lock(mutex_);
        return truncated_;
    }

private:
    mutable std::mutex mutex_;
    std::size_t head_limit_;
    std::size_t tail_limit_;
    std::string head_;
    std::string tail_;
    bool truncated_ = false;
};

} // namespace staragent
