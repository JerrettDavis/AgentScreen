# Security policy

AgentDisplay is local-first, but it deliberately listens on local network interfaces so a paired display and companion browser can reach it. The security boundary is enforced at the API, not by claiming that the host is loopback-only.

## Data classification

Raw transcripts, prompts, full tool inputs, credentials, account identifiers, and absolute project paths are host-only. The PWA can request session and turn detail after local or paired access. The E32R40T receives only a compact model containing aliases, counts, percentages, short redacted summaries, and pending gate metadata.

Provider credentials are never copied into the browser or device payload. The optional Claude usage collector reads the local token only inside the host process and is disabled by default.

## Host access

- The application binds to `0.0.0.0:5277` so LAN devices can connect.
- Loopback API requests are accepted without a key.
- Every non-loopback path under `/api` requires `X-AgentDisplay-Key` or the equivalent `key` query parameter.
- The static Blazor shell can load without the key, but data access redirects an unpaired browser to `/pair`.
- A query-string key is moved to `sessionStorage` and removed from the address bar before API use.
- Browser trust is scoped to the current tab. Closing the tab clears it.
- The generated key is compared in fixed time and stored at `~/.agentdisplay/pairing-key`.
- On Unix-like systems, `~/.agentdisplay` is restricted to mode `0700` and key/settings files to `0600` when the filesystem supports it.

The key is a local pairing secret, not a replacement for TLS on an untrusted network. Use trusted HTTPS before exposing the host beyond a private network.

## Hook safety

- Dry-run is available in the CLI and PWA.
- Existing hook arrays are preserved.
- Every modified existing file receives a timestamped backup.
- Hook configuration files are written atomically through a temporary file.
- Gate rules are deterministic regular expressions, not model judgments.
- Approval surfaces show a redacted command or tool summary and a project alias.
- The relay fails open after its timeout unless strict mode is explicitly enabled.
- Strict mode can deny work when the host is unavailable, so it should be enabled only after testing the complete approval path.
- Codex hook trust remains under Codex control and must be reviewed with `/hooks`.

## Device network boundary

The ESP32 runs in AP+STA mode.

- First boot creates a random WPA2 SoftAP password and persists it in device preferences.
- The configuration page and configuration POST are accepted only from the direct SoftAP subnet.
- Snapshot and status endpoints accept either a direct SoftAP client or the host pairing key.
- Host-initiated pushes are restricted to loopback, private, link-local, and `.local` targets to reduce server-side request forgery risk.
- Device responses never echo the pairing key.
- Device payloads are size-limited and parsed into a compact fixed-purpose state model.

## Bluetooth boundary

BLE is display-push-only in this alpha. The characteristic does not carry provider credentials or raw turns, but it does not add application-layer authentication or bonding. Treat it as a proximity transport for redacted snapshots. Physical approval decisions still require a reachable authenticated Wi-Fi host.

## Web and browser considerations

Web Bluetooth and normal PWA installation require a secure browser context. Localhost qualifies; remote plain HTTP generally does not. A dashboard opened over remote HTTP can still use paired API access, but Bluetooth and installability should be expected only on localhost or trusted HTTPS.

## Reporting

Report vulnerabilities privately through [GitHub Security Advisories](https://github.com/JerrettDavis/AgentScreen/security/advisories/new).

Do not open a public issue containing a transcript, credential, pairing key, private project path, or provider session file. Provide a minimal redacted reproduction and include the relevant validation-report entry. Security fixes are currently released from `main`; this pre-1.0 project does not maintain older release branches.
