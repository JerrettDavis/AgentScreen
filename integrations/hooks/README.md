# AgentDisplay hooks

`relay.mjs` reads one provider hook payload from standard input, redacts it, posts the normalized event to the local AgentDisplay host, waits when policy returns `ask`, and emits the provider-specific final hook result.

`install.mjs` merges the relay into user-level Claude Code, Codex, and GitHub Copilot CLI hook files. It never replaces an existing hook array. Use `--dry-run` before `--apply`; every modified existing file receives a timestamped backup.

Environment variables:

- `AGENTDISPLAY_HOST` changes the loopback host URL.
- `AGENTDISPLAY_KEY` supplies the pairing key when the relay reaches the host through a non-loopback address, such as across a VM or WSL boundary.
- `AGENTDISPLAY_STRICT=true` denies instead of allowing when the host or approval surface is unavailable.
- `AGENTDISPLAY_GATE_TIMEOUT_MS` changes the relay wait ceiling.
- `AGENTDISPLAY_HOME` overrides the home directory for testing or portable installations.
