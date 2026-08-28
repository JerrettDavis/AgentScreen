# Product review

Agent dashboards and small physical controllers are beginning to converge, but most current projects specialize in one provider, one desktop UI, or one hardware concept. AgentScreen combines four concerns in one local-first boundary:

- provider-neutral session and usage normalization;
- a full Blazor PWA and a purpose-built 320×480 display UI;
- deterministic hook-based behavior gates with physical approvals;
- LAN, direct SoftAP, and browser-mediated Bluetooth transport.

The closest reusable patterns from Jerrett Davis's existing projects are the EspScreen launcher/networking model, ClaudeUsageDashboard's local file watching and live dashboard, ClaudeStatusLineWidgets' usage normalization and cache accounting, and ClaudeScheduleSessionResume's plugin/hook packaging discipline.

The design avoids claiming that estimated API spend equals subscription billing, that locally observed tokens equal entitlement consumption, or that every provider exposes identical authoritative windows. Coverage and provenance are visible product concepts rather than footnotes.
