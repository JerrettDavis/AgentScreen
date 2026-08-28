#include "AgentDisplayModel.h"

namespace agentdisplay {
bool ChunkAssembler::append(const char* bytes, std::size_t length) {
    if (overflowed_) return false;
    if (buffer_.size() + length > maximum_) { overflowed_ = true; buffer_.clear(); return false; }
    buffer_.append(bytes, length);
    return true;
}
bool ChunkAssembler::ready() const { return !buffer_.empty() && buffer_.back() == '\n'; }
std::string ChunkAssembler::take() {
    if (!ready()) return {};
    while (!buffer_.empty() && (buffer_.back() == '\n' || buffer_.back() == '\r')) buffer_.pop_back();
    std::string result;
    result.swap(buffer_);
    overflowed_ = false;
    return result;
}
void ChunkAssembler::clear() { buffer_.clear(); overflowed_ = false; }
std::string shortLabel(const std::string& value, std::size_t maximum) {
    if (value.size() <= maximum) return value;
    if (maximum <= 3) return value.substr(0, maximum);
    return value.substr(0, maximum - 3) + "...";
}
}
