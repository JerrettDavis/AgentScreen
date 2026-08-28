# Delivery plan

## Product slice

The first slice is a local-first agent operations console. A host service watches supported agent directories, normalizes session and usage data, evaluates deterministic gate rules, serves a Blazor PWA, and sends a deliberately smaller redacted model to the E32R40T display.

The screen is useful as a glanceable dashboard and an approval surface. The PWA owns configuration, detailed turns, provider coverage labels, pairing, and policy management. Agent credentials and raw transcripts remain on the host.

## Milestones represented in this repository

1. **Observe:** discover Claude Code, Codex, and Copilot CLI sessions; show normalized dashboards and drill-downs.
2. **Estimate:** calculate token totals, API-equivalent cost, cache effectiveness, burn rate, and exhaustion forecasts with provenance labels.
3. **Gate:** install lifecycle hooks, match deterministic policies, wait for an approval, and translate the decision back to each provider's hook contract.
4. **Pair:** support normal LAN mode, an ESP32 SoftAP direct mode, and a browser-to-device Web Bluetooth path.
5. **Harden:** redact device payloads, authenticate non-loopback host requests, back up modified hook files, fail open by default, and make strict mode explicit.

## Next slices

- Provider-specific entitlement collectors where a documented local/API surface exists.
- Signed firmware updates and per-device certificates.
- Policy authoring UI with simulations against recorded redacted events.
- Durable SQLite history and multi-machine aggregation.
- Optional companion tray process and packaged Windows/macOS/Linux installers.
- Additional displays through a versioned compact device protocol.
