# Provider support notes

Provider support is split into three independent capabilities: local observation, lifecycle hooks, and authoritative account usage. A provider can support one without exposing the others.

## Claude Code

- Sessions: `~/.claude/projects/**/*.jsonl`
- Hooks: user-level `~/.claude/settings.json`
- Installed events: `SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PermissionRequest`, `PostToolUse`, `Stop`
- Gate output: Claude hook-specific `PreToolUse` or `PermissionRequest` decision JSON
- Account usage: optional host-only collector for reported 5-hour and 7-day windows

## OpenAI Codex

- Sessions: `~/.codex/sessions/**/*.jsonl`
- History: `~/.codex/history.jsonl`
- Hooks: user-level `~/.codex/hooks.json`
- Installed events: `SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PermissionRequest`, `PostToolUse`, `Stop`
- Gate output: Codex hook-specific final allow or deny JSON
- Trust: new or modified hooks require review with `/hooks`
- Account usage: observed local activity only in this alpha

Codex currently parses but does not support `permissionDecision: "ask"` for `PreToolUse`. AgentDisplay waits inside its own hook process and sends Codex only the final decision.

## GitHub Copilot CLI

- Sessions: `~/.copilot/session-state/*/events.jsonl`
- Hooks: user-level `~/.copilot/hooks/agentdisplay.json`
- Installed events: `sessionStart`, `userPromptSubmitted`, `preToolUse`, `permissionRequest`, `postToolUse`, `sessionEnd`
- Gate output: camelCase `preToolUse` or `permissionRequest` decision JSON
- Account usage: observed local activity only in this alpha

The provider fallback price is useful for relative burn-rate telemetry, but it is not a representation of GitHub plan billing.

## Versioning rule

Every adapter should remain isolated behind the normalized contracts. Provider-specific fields belong in parser and hook translation tests, not in the PWA or firmware. Update fixtures whenever an upstream local schema or hook contract changes.
