# Third-party notices

AgentScreen depends on open-source components. Their authors retain their respective copyrights. Release packages should preserve the license files emitted by the .NET publisher and include this notice with firmware artifacts.

Direct firmware dependencies:

| Component | Version constraint | License | Project |
|---|---:|---|---|
| ArduinoJson | `^7.3.1` | MIT | <https://github.com/bblanchon/ArduinoJson> |
| LVGL | `^8.4.0` | MIT | <https://github.com/lvgl/lvgl> |
| NimBLE-Arduino | `^1.4.2` | Apache-2.0, with bundled upstream notices | <https://github.com/h2zero/NimBLE-Arduino> |
| TFT_eSPI | `^2.5.43` | MIT/BSD-derived; see upstream license | <https://github.com/Bodmer/TFT_eSPI> |

The host and PWA use Microsoft .NET and ASP.NET Core packages under the MIT license. Test-only dependencies include xUnit, Microsoft.NET.Test.Sdk, and Coverlet. PlatformIO and the Espressif Arduino toolchain are build dependencies and are not vendored in this repository.

Dependency versions are resolved by NuGet and PlatformIO during build. Consult each resolved package’s included license file for the complete applicable terms. This notice does not modify those terms.
