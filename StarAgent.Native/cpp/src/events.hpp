// SPDX-License-Identifier: MIT
#pragma once

#include "protocol.hpp"

#include <mutex>
#include <string>
#include <vector>

namespace staragent {

// Bounded event queue persisted to JSON so events survive restarts.
class EventQueue
{
public:
    EventQueue(std::string path, std::size_t max_events = 1000);

    void Add(const EventModel& event);
    std::vector<EventModel> Take(std::size_t count);
    void PutFront(const std::vector<EventModel>& events);
    std::size_t Size() const;

private:
    void SaveLocked();
    void TrimLocked();

    mutable std::mutex mutex_;
    std::string path_;
    std::size_t max_events_;
    std::vector<EventModel> events_;
};

} // namespace staragent
