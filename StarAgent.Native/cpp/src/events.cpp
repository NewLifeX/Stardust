// SPDX-License-Identifier: MIT
#include "events.hpp"

#include "config.hpp"

#include <filesystem>
#include <fstream>

namespace staragent {

EventQueue::EventQueue(std::string path, std::size_t max_events)
    : path_(std::move(path)), max_events_(max_events == 0 ? 1000 : max_events)
{
    if (path_.empty()) return;

    std::error_code error;
    if (!std::filesystem::exists(path_, error)) return;

    try
    {
        std::ifstream stream(path_, std::ios::binary);
        if (!stream) return;
        Json value;
        stream >> value;
        if (!value.is_array()) return;
        for (const auto& item : value)
        {
            if (!item.is_object()) continue;
            EventModel event;
            event.Time = item.value("Time", static_cast<std::int64_t>(0));
            event.Type = item.value("Type", std::string());
            event.Name = item.value("Name", std::string());
            event.Remark = item.value("Remark", std::string());
            events_.push_back(std::move(event));
        }
        TrimLocked();
    }
    catch (...)
    {
        events_.clear();
    }
}

void EventQueue::Add(const EventModel& event)
{
    std::lock_guard<std::mutex> lock(mutex_);
    events_.push_back(event);
    TrimLocked();
    SaveLocked();
}

std::vector<EventModel> EventQueue::Take(std::size_t count)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (events_.empty() || count == 0) return {};
    if (count > events_.size()) count = events_.size();

    std::vector<EventModel> batch(events_.begin(), events_.begin() + static_cast<std::ptrdiff_t>(count));
    events_.erase(events_.begin(), events_.begin() + static_cast<std::ptrdiff_t>(count));
    SaveLocked();
    return batch;
}

void EventQueue::PutFront(const std::vector<EventModel>& events)
{
    if (events.empty()) return;
    std::lock_guard<std::mutex> lock(mutex_);
    std::vector<EventModel> merged = events;
    merged.insert(merged.end(), events_.begin(), events_.end());
    events_ = std::move(merged);
    TrimLocked();
    SaveLocked();
}

std::size_t EventQueue::Size() const
{
    std::lock_guard<std::mutex> lock(mutex_);
    return events_.size();
}

void EventQueue::TrimLocked()
{
    if (max_events_ > 0 && events_.size() > max_events_)
    {
        events_.erase(events_.begin(),
                      events_.begin() + static_cast<std::ptrdiff_t>(events_.size() - max_events_));
    }
}

void EventQueue::SaveLocked()
{
    if (path_.empty()) return;
    try
    {
        Json value = Json::array();
        for (const auto& event : events_) value.push_back(ToJson(event));
        WriteFileAtomic(path_, value.dump());
    }
    catch (...)
    {
        // Losing persistence is preferable to stopping the agent.
    }
}

} // namespace staragent
