#pragma once
#include <Arduino.h>
#include <ArduinoJson.h>
#include "BoardConfig.h"

struct ProviderView { String name; int usage = 0; String window; bool connected = false; };
struct SessionView { String id; String provider; String project; String model; String state; int tokensK = 0; int costUnits = 0; };
struct StatsView { int active = 0; int sessions = 0; int projects = 0; int gates = 0; float spend = 0; float hourly = 0; int cacheRate = 0; int cacheBreaks = 0; long exhaustion = 0; };
struct GateView { bool pending = false; String id; String project; String tool; String summary; String reason; long expires = 0; };

class DeviceState {
public:
    bool update(const String& json) {
        JsonDocument doc;
        const auto error = deserializeJson(doc, json);
        if (error) { lastError = error.c_str(); return false; }
        if (String((const char*)(doc["v"] | "")) != "1") { lastError = "unsupported protocol"; return false; }
        providerCount = 0;
        for (JsonObject item : doc["p"].as<JsonArray>()) {
            if (providerCount >= 3) break;
            auto& target = providers[providerCount++];
            target.name = String((const char*)(item["n"] | "Agent"));
            target.usage = item["u"] | 0;
            target.window = String((const char*)(item["w"] | "observed"));
            target.connected = item["c"] | false;
        }
        sessionCount = 0;
        for (JsonObject item : doc["s"].as<JsonArray>()) {
            if (sessionCount >= 8) break;
            auto& target = sessions[sessionCount++];
            target.id = String((const char*)(item["i"] | ""));
            target.provider = String((const char*)(item["p"] | "Agent"));
            target.project = String((const char*)(item["a"] | "project"));
            target.model = String((const char*)(item["m"] | "model"));
            target.costUnits = item["c"] | 0;
            target.tokensK = item["t"] | 0;
            target.state = String((const char*)(item["s"] | "Idle"));
        }
        JsonObject metrics = doc["m"].as<JsonObject>();
        stats.active = metrics["a"] | 0; stats.sessions = metrics["s"] | 0; stats.projects = metrics["p"] | 0;
        stats.gates = metrics["g"] | 0; stats.spend = metrics["d"] | 0.0f; stats.hourly = metrics["h"] | 0.0f;
        stats.cacheRate = metrics["r"] | 0; stats.cacheBreaks = metrics["b"] | 0; stats.exhaustion = metrics["x"] | 0L;
        gate = {};
        if (!doc["g"].isNull()) {
            JsonObject value = doc["g"].as<JsonObject>();
            gate.pending = true; gate.id = String((const char*)(value["i"] | "")); gate.project = String((const char*)(value["p"] | "project"));
            gate.tool = String((const char*)(value["t"] | "tool")); gate.summary = String((const char*)(value["q"] | "")); gate.reason = String((const char*)(value["r"] | "")); gate.expires = value["x"] | 0L;
        }
        updatedAt = millis(); lastError = ""; return true;
    }

    ProviderView providers[3]; int providerCount = 0;
    SessionView sessions[8]; int sessionCount = 0;
    StatsView stats; GateView gate;
    unsigned long updatedAt = 0; String lastError;
};
