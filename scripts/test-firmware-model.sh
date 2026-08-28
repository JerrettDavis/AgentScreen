#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$(mktemp -t agentdisplay-model-test.XXXXXX)"
trap 'rm -f "$OUT"' EXIT

g++ -std=c++17 -Wall -Wextra -Wpedantic -Werror \
  -I "$ROOT/firmware/e32r40t/lib/AgentDisplayModel/src" \
  "$ROOT/tests/native/firmware_model_test.cpp" \
  "$ROOT/firmware/e32r40t/lib/AgentDisplayModel/src/AgentDisplayModel.cpp" \
  -o "$OUT"
"$OUT"
