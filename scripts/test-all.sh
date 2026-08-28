#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
node --check integrations/hooks/install.mjs
node --check integrations/hooks/relay.mjs
node --check src/AgentDisplay.Web/wwwroot/js/agentdisplay.js
node --test tests/js/*.test.mjs
scripts/test-firmware-model.sh
if command -v dotnet >/dev/null 2>&1; then dotnet test AgentDisplay.slnx --configuration Release --nologo; else echo 'dotnet: skipped (not installed)'; fi
if command -v pio >/dev/null 2>&1; then pio test -d firmware/e32r40t -e native && pio run -d firmware/e32r40t -e e32r40t; else echo 'platformio: skipped (not installed)'; fi
