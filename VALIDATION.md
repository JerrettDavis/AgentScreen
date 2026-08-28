# Validation record

The repository keeps every major layer behind a testable seam.

| Layer | Command | Coverage |
|---|---|---|
| Contracts and core | `dotnet test AgentDisplay.slnx --configuration Release` | provider fixtures, pricing, metrics, policy order, redaction, device mapping |
| Hook installer and relay | `node --test tests/js/*.test.mjs` | provider output contracts, delayed approval, pairing header, fail-open/strict behavior, merge, backup, dry-run |
| Firmware protocol | `bash scripts/test-firmware-model.sh` | standalone C++ chunk framing and compact model parsing |
| PlatformIO firmware | `pio test -d firmware/e32r40t -e native` and `pio run -d firmware/e32r40t -e e32r40t` | PlatformIO native model test and ESP32 firmware build |
| Repository | `python3 scripts/validate.py` | required files, JSON/XML/Python/JavaScript syntax, native test, pin map, secret scan, icons, screenshots, repository digest |
| Browser visual | `node scripts/capture-screenshots.mjs` | deterministic desktop, mobile, session, approval, and 320×480 captures |

## Packaging-environment result

The final package was checked with:

- eight executable Node integration tests;
- JavaScript syntax checks for the relay, installer, PWA bridge, service workers, and screenshot entry point;
- the standalone native C++ firmware-model test;
- Python compilation checks;
- JSON and XML parsing;
- the E32R40T pin-map check;
- secret-shaped value scanning;
- PWA icon dimensions;
- five deterministic screenshot dimensions;
- a deterministic SHA-256 digest over repository file paths and contents.

The packaging environment did not contain the .NET SDK or PlatformIO. Those two checks are recorded as skips rather than passes. The included GitHub Actions workflow runs the .NET solution on Linux and Windows and builds the PlatformIO firmware on Linux.

No physical E32R40T flash, touch calibration, network pairing, or provider-account login was performed in the packaging environment. Those remain hardware and account acceptance tests.

Run validation to generate the ignored local `validation-report.json`; CI uploads the same report as a build artifact. It contains the timestamp, exact check output, status, and repository digest. A `pass_with_skips` result means every executable check available in the environment passed and unavailable toolchains were explicitly identified.
