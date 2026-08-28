# Troubleshooting

## Host or dashboard does not load

Run the host from the repository root with:

```bash
dotnet run --project src/AgentDisplay.Host -- --demo
```

Wait for `Now listening on: http://0.0.0.0:5277`, then open `http://127.0.0.1:5277` on the host machine. If startup reports that port 5277 is already in use, stop the older AgentDisplay process before retrying. `/healthz` returning JSON confirms the host API is running; the root page should return the AgentScreen dashboard rather than a 404 response.

## Bluetooth connection or sync fails

Web Bluetooth requires current Chrome or Edge on a Bluetooth-capable computer and a secure browser context. `http://localhost` is allowed; an unencrypted page opened by LAN IP normally is not.

1. Keep the display powered, nearby, and on its normal dashboard or setup screen.
2. Select **Retry connection** and choose `AgentDisplay` in the browser picker.
3. If the connection drops during sync, wait a moment. The browser bridge automatically reconnects once and reacquires the GATT service.
4. If the sync service remains unavailable, reset the display, wait for startup to finish, refresh the Devices page, and connect again.
5. Disconnect other browsers, computers, or phones that may already hold the BLE connection.
6. Confirm Bluetooth permission is allowed for the site in browser settings.

Raw messages such as `GATT Server is disconnected`, `Cannot retrieve services`, and trailing `undefined` indicate stale browser GATT state. Current builds convert these into the recovery flow above.

## Wi-Fi connection fails

Tap **WIFI** in the upper-right of the physical display to reopen provisioning details. Join the displayed `AgentDisplay-XXXX` network and open `http://192.168.4.1`. Verify the host URL is reachable from the display and the pairing key matches the **Devices** page.

## Data looks stale

Open **Devices**, enable **Update stats automatically**, and choose 30 seconds, 1 minute, 5 minutes, or 15 minutes. The setting is stored in that browser. The first snapshot is sent during connection; the selected interval controls later updates.

## Display flashes, fades, or touch is inverted

Build the `e32r40t` PlatformIO environment so its pinned ST7796S/XPT2046 configuration is used. The expected portrait touch calibration is `{275, 3620, 264, 3532, 4}`. If a specific panel still differs, capture raw corner readings before changing calibration values.

## Reporting a bug safely

Never attach credentials, pairing keys, prompts, transcripts, provider session files, usernames, or absolute private paths. Use a minimal redacted reproduction and include the OS, browser, board revision, transport, and AgentScreen version.
