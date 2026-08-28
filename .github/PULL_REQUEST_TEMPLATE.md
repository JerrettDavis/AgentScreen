## Summary

<!-- What changed and why? -->

## Areas

- [ ] Host/core/contracts
- [ ] Web dashboard or Bluetooth bridge
- [ ] ESP32 firmware or hardware configuration
- [ ] Hooks/provider integrations
- [ ] Documentation or CI

## Validation

- [ ] `npm test`
- [ ] `dotnet test AgentDisplay.slnx --configuration Release`
- [ ] `pio test -d firmware/e32r40t -e native`
- [ ] `pio run -d firmware/e32r40t -e e32r40t`
- [ ] `python scripts/validate.py`
- [ ] Hardware/browser verification described below, or not applicable

## Security and privacy

- [ ] No credentials, pairing keys, transcripts, prompts, or private paths are included.
- [ ] Trust-boundary changes are documented in `SECURITY.md`.

## Reviewer notes

<!-- Hardware used, screenshots, compatibility impact, or follow-up work. -->
