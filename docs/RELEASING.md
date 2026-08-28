# Releasing AgentScreen

## Before the first public push

1. Run `python scripts/validate.py` and confirm every check passes.
2. Run `git status --short` and review every tracked file.
3. Confirm `git grep -I -n -E '(api[_-]?key|token|password|secret)'` contains only examples, field names, and security documentation.
4. Confirm local logs, build outputs, `.pio`, user settings, and provider data remain ignored.
5. Create the public `JerrettDavis/AgentScreen` repository with the MIT license topic and the description from this document.
6. Push `main`, enable GitHub Advanced Security features available to public repositories, and allow auto-merge.
7. Run **Sync repository labels** once, then configure `main` rules to require CI, CodeQL, and dependency review.

Suggested description:

> Local-first operations dashboard and ESP32 companion screen for Claude Code, OpenAI Codex, and GitHub Copilot CLI.

Suggested topics: `agent-dashboard`, `blazor`, `claude-code`, `codex`, `esp32`, `github-copilot`, `lvgl`, `platformio`, `web-bluetooth`.

## Tagged releases

Update `CHANGELOG.md`, run the complete validation suite, and tag a semantic version such as `v0.1.0-alpha.1`. The Release workflow publishes self-contained Windows and Linux hosts, E32R40T firmware, generated release notes, and `SHA256SUMS`.

Release artifacts are not code-signed. State that clearly in release notes and require checksum verification for downloaded binaries.
