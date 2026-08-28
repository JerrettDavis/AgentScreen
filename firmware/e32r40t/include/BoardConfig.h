#pragma once

#include <Arduino.h>

namespace board {
constexpr uint16_t ScreenWidth = 320;
constexpr uint16_t ScreenHeight = 480;
constexpr uint8_t Backlight = 27;
constexpr uint8_t TouchCs = 33;
constexpr uint8_t TouchIrq = 36;
constexpr uint8_t Button = 0;
constexpr uint8_t BatteryAdc = 34;
constexpr uint8_t LedRed = 22;
constexpr uint8_t LedGreen = 16;
constexpr uint8_t LedBlue = 17;
constexpr uint32_t SnapshotIntervalMs = 3'000;
constexpr uint32_t UiTickMs = 5;
constexpr size_t MaxSnapshotBytes = 12 * 1024;
constexpr char BleService[] = "9f5e0001-4a67-4f3b-a7d0-a1d4a7d10001";
constexpr char BleRx[] = "9f5e0002-4a67-4f3b-a7d0-a1d4a7d10001";
constexpr char BleTx[] = "9f5e0003-4a67-4f3b-a7d0-a1d4a7d10001";
}
