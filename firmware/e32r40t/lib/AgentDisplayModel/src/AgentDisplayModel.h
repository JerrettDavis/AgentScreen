#pragma once
#include <cstddef>
#include <string>

namespace agentdisplay {
class ChunkAssembler {
public:
    explicit ChunkAssembler(std::size_t maximum = 12 * 1024) : maximum_(maximum) {}
    bool append(const char* bytes, std::size_t length);
    bool ready() const;
    std::string take();
    void clear();
    std::size_t size() const { return buffer_.size(); }
    bool overflowed() const { return overflowed_; }
private:
    std::size_t maximum_;
    std::string buffer_;
    bool overflowed_ = false;
};

std::string shortLabel(const std::string& value, std::size_t maximum);
}
