#include <cassert>
#include <iostream>
#include <string>

#include "AgentDisplayModel.h"

using agentdisplay::ChunkAssembler;

int main() {
    ChunkAssembler assembler(64);
    assert(assembler.append("{\"v\":", 5));
    assert(!assembler.ready());
    assert(assembler.append("\"1\"}\r\n", 6));
    assert(assembler.ready());
    assert(assembler.take() == "{\"v\":\"1\"}");
    assert(assembler.size() == 0);

    ChunkAssembler bounded(5);
    assert(!bounded.append("123456", 6));
    assert(bounded.overflowed());
    assert(!bounded.append("ok\n", 3));
    bounded.clear();
    assert(bounded.append("ok\n", 3));
    assert(bounded.take() == "ok");

    assert(agentdisplay::shortLabel("AgentDisplay", 8) == "Agent...");
    assert(agentdisplay::shortLabel("Agent", 8) == "Agent");
    assert(agentdisplay::shortLabel("abcd", 2) == "ab");

    std::cout << "firmware model tests passed\n";
    return 0;
}
