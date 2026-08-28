# E32R40T firmware

Target hardware: Hosyond/LCDWiki E32R40T with ESP32-32E, 320×480 ST7796S SPI LCD, and XPT2046 resistive touch.

The firmware is intentionally thin. It renders the compact AgentDisplay protocol, accepts authenticated HTTP snapshots, optionally pulls from the host over normal Wi-Fi, and accepts chunked BLE snapshot pushes. It does not store provider credentials or full transcripts.

## Build and flash

```bash
pio run -d firmware/e32r40t -e e32r40t
pio run -d firmware/e32r40t -e e32r40t -t upload
pio device monitor -d firmware/e32r40t
```

The default upload speed is defined in `platformio.ini`. Use a data-capable USB-C cable and the serial port selected by PlatformIO.

## First boot

1. The screen and serial console show an SSID such as `AgentDisplay-A12F` and a randomly generated WPA2 password.
2. Join that network from a laptop or phone.
3. Open `http://192.168.4.1`.
4. Enter normal Wi-Fi credentials when available.
5. Enter an AgentDisplay host URL reachable from the ESP32, such as `http://192.168.1.20:5277`.
6. Paste the pairing key from the PWA **Devices** page or `~/.agentdisplay/pairing-key` on the host.
7. Save. The board restarts and begins pulling snapshots.

The SoftAP remains enabled in AP+STA mode, so setup can be revisited without erasing normal Wi-Fi configuration.
Tap the persistent **WIFI** button at the top-right of the display to reopen the setup network details after dismissing them.
Tap **REFRESH NOW** on the **STATS** tab to request the latest snapshot immediately instead of waiting for the next automatic pull.

## HTTP endpoints

| Endpoint | Method | Access |
|---|---|---|
| `/` | GET | Direct SoftAP subnet only |
| `/api/config` | POST | Direct SoftAP subnet only |
| `/api/status` | GET | Direct SoftAP subnet or `X-AgentDisplay-Key` |
| `/api/snapshot` | POST | Direct SoftAP subnet or `X-AgentDisplay-Key` |

The host normally pulls or pushes only compact, redacted state. BLE uses the same JSON model terminated by a newline and split into 160-byte writes.

## Bluetooth limitation

BLE is display-push-only in `0.1.0-alpha.1`. It does not provide an authenticated return channel to the host. On-screen allow and deny buttons therefore require a configured Wi-Fi host URL even when the current snapshot arrived over BLE.

## Touch calibration

The five values in `src/main.cpp` are safe board-family defaults:

```cpp
{ 280, 3620, 260, 3560, 0 }
```

They are not a substitute for calibrating the individual panel. Update them when touches are mirrored, offset, or fail near an edge.

## Pins

| Function | GPIO |
|---|---:|
| TFT MISO | 12 |
| TFT MOSI | 13 |
| TFT SCLK | 14 |
| TFT CS | 15 |
| TFT DC | 2 |
| TFT backlight | 27 |
| Touch CS | 33 |
| Touch IRQ | 36 |

LCD reset is shared with board enable. Display and touch share the SPI bus. These values are pinned in `platformio.ini` and `include/BoardConfig.h` and checked by `scripts/validate.py`.

## Tests

```bash
bash scripts/test-firmware-model.sh
pio test -d firmware/e32r40t -e native
```

The first command compiles the dependency-free protocol model directly with `g++`. The PlatformIO native environment exercises the same library through Unity.
