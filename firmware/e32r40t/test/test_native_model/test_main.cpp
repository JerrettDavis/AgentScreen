#include <unity.h>
#include "AgentDisplayModel.h"

using agentdisplay::ChunkAssembler;

void setUp() {}
void tearDown() {}

void test_assembles_newline_terminated_snapshot() {
    ChunkAssembler assembler(64);
    TEST_ASSERT_TRUE(assembler.append("{\"v\":", 5));
    TEST_ASSERT_FALSE(assembler.ready());
    TEST_ASSERT_TRUE(assembler.append("\"1\"}\n", 5));
    TEST_ASSERT_TRUE(assembler.ready());
    TEST_ASSERT_EQUAL_STRING("{\"v\":\"1\"}", assembler.take().c_str());
    TEST_ASSERT_EQUAL_UINT32(0, assembler.size());
}

void test_rejects_overflow_and_recovers_after_clear() {
    ChunkAssembler assembler(5);
    TEST_ASSERT_FALSE(assembler.append("123456", 6));
    TEST_ASSERT_TRUE(assembler.overflowed());
    assembler.clear();
    TEST_ASSERT_TRUE(assembler.append("ok\n", 3));
    TEST_ASSERT_EQUAL_STRING("ok", assembler.take().c_str());
}

void test_short_label_is_bounded() {
    TEST_ASSERT_EQUAL_STRING("Agent...", agentdisplay::shortLabel("AgentDisplay", 8).c_str());
    TEST_ASSERT_EQUAL_STRING("Agent", agentdisplay::shortLabel("Agent", 8).c_str());
}

int main(int, char**) {
    UNITY_BEGIN();
    RUN_TEST(test_assembles_newline_terminated_snapshot);
    RUN_TEST(test_rejects_overflow_and_recovers_after_clear);
    RUN_TEST(test_short_label_is_bounded);
    return UNITY_END();
}
