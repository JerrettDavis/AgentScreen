# Contributing

Thanks for considering a contribution to AgentScreen. The internal `AgentDisplay.*` names are compatibility identifiers and should not be mechanically renamed without a migration plan.

1. Create a focused branch and include tests for behavioral changes.
2. Run `python scripts/validate.py`, `node --test tests/js/*.test.mjs`, `dotnet test AgentDisplay.slnx`, and `pio test -d firmware/e32r40t -e native`.
3. Keep provider-specific parsing behind normalized contracts. Do not leak full prompts, tool inputs, credentials, or absolute paths into device payloads.
4. Update `CHANGELOG.md` for user-visible changes.

Install browser/screenshot dependencies with `python -m pip install -r requirements-dev.txt`. With the host running, validate the Devices page using `python tests/browser/devices_smoke.py`; regenerate checked screenshots with `python scripts/capture_screenshots.py`.

Use a conventional pull-request title such as `fix(web): recover disconnected Bluetooth sync` or `feat(firmware): add a display status view`. Describe physical hardware and browser testing in the pull-request template when applicable.

Firmware changes should be tested at 320×480 portrait resolution on the LCDWiki/Hosyond E32R40T pinout. Hook changes must preserve existing user configuration, create a timestamped backup, and support `--dry-run`.
