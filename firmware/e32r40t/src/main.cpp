#include <Arduino.h>
#include <HTTPClient.h>
#include <NimBLEDevice.h>
#include <Preferences.h>
#include <TFT_eSPI.h>
#include <WebServer.h>
#include <WiFi.h>
#include <ESPmDNS.h>
#include <lvgl.h>
#include <mutex>
#include <utility>
#include "AgentDisplayModel.h"
#include "BoardConfig.h"
#include "DeviceState.h"
#include "Ui.h"

namespace {
TFT_eSPI tft;
WebServer web(80);
Preferences preferences;
DeviceState state;
Ui ui(state);
agentdisplay::ChunkAssembler chunks(board::MaxSnapshotBytes);
std::mutex snapshotMutex;
std::string pendingSnapshot;
NimBLECharacteristic* txCharacteristic = nullptr;
String wifiSsid, wifiPassword, hostUrl, pairingKey, apName, apPassword;
unsigned long lastPull = 0;
lv_disp_draw_buf_t drawBuffer;
lv_color_t pixels[board::ScreenWidth * 24];

String chipSuffix() {
    const uint64_t id = ESP.getEfuseMac();
    char value[9]; snprintf(value, sizeof(value), "%04X", static_cast<unsigned>(id & 0xFFFF)); return String(value);
}
String cleanBaseUrl(String value) { value.trim(); while (value.endsWith("/")) value.remove(value.length() - 1); return value; }
String htmlEscape(String value) {
    value.replace("&", "&amp;"); value.replace("<", "&lt;"); value.replace(">", "&gt;"); value.replace("\"", "&quot;"); return value;
}
void cors() { web.sendHeader("Access-Control-Allow-Origin", "*"); web.sendHeader("Access-Control-Allow-Headers", "Content-Type, X-AgentDisplay-Key"); web.sendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS"); }
bool isDirectClient() {
    const IPAddress remote = web.client().remoteIP();
    const IPAddress local = WiFi.softAPIP();
    return remote[0] == local[0] && remote[1] == local[1] && remote[2] == local[2];
}
bool hasPairingKey() {
    return pairingKey.length() > 0 && web.hasHeader("X-AgentDisplay-Key") && web.header("X-AgentDisplay-Key") == pairingKey;
}
bool requireDeviceAccess(bool directOnly = false) {
    if (isDirectClient() || (!directOnly && hasPairingKey())) return true;
    cors(); web.send(401, "application/json", "{\"error\":\"pairing key or direct setup connection required\"}"); return false;
}

void displayFlush(lv_disp_drv_t* display, const lv_area_t* area, lv_color_t* colors) {
    const uint32_t width = area->x2 - area->x1 + 1;
    const uint32_t height = area->y2 - area->y1 + 1;
    tft.startWrite(); tft.setAddrWindow(area->x1, area->y1, width, height); tft.pushColors(reinterpret_cast<uint16_t*>(colors), width * height, true); tft.endWrite();
    lv_disp_flush_ready(display);
}
void touchRead(lv_indev_drv_t*, lv_indev_data_t* data) {
    uint16_t x = 0, y = 0;
    if (tft.getTouch(&x, &y, 500)) { data->state = LV_INDEV_STATE_PR; data->point.x = x; data->point.y = y; }
    else data->state = LV_INDEV_STATE_REL;
}

bool applySnapshot(const String& json) {
    if (!state.update(json)) { Serial.printf("Snapshot rejected: %s\n", state.lastError.c_str()); return false; }
    ui.refresh();
    if (txCharacteristic) { txCharacteristic->setValue("ok\n"); txCharacteristic->notify(); }
    return true;
}
void queueSnapshot(std::string value) { std::lock_guard<std::mutex> lock(snapshotMutex); pendingSnapshot = std::move(value); }
void applyQueuedSnapshot() {
    std::string value;
    { std::lock_guard<std::mutex> lock(snapshotMutex); if (pendingSnapshot.empty()) return; value.swap(pendingSnapshot); }
    applySnapshot(String(value.c_str()));
}

class RxCallbacks final : public NimBLECharacteristicCallbacks {
    void onWrite(NimBLECharacteristic* characteristic) override {
        const std::string value = characteristic->getValue();
        if (!chunks.append(value.data(), value.size())) { chunks.clear(); return; }
        if (chunks.ready()) queueSnapshot(chunks.take());
    }
};

void beginBle() {
    NimBLEDevice::init(apName.c_str());
    auto* server = NimBLEDevice::createServer();
    auto* service = server->createService(board::BleService);
    auto* rx = service->createCharacteristic(board::BleRx, NIMBLE_PROPERTY::WRITE | NIMBLE_PROPERTY::WRITE_NR);
    txCharacteristic = service->createCharacteristic(board::BleTx, NIMBLE_PROPERTY::READ | NIMBLE_PROPERTY::NOTIFY);
    rx->setCallbacks(new RxCallbacks()); txCharacteristic->setValue("ready\n"); service->start();
    auto* advertising = NimBLEDevice::getAdvertising(); advertising->addServiceUUID(board::BleService); advertising->setScanResponse(true); advertising->start();
}

String setupPage() {
    const String ip = WiFi.softAPIP().toString();
    String html = R"HTML(<!doctype html><html><head><meta name="viewport" content="width=device-width"><title>AgentDisplay setup</title><style>body{font-family:system-ui;background:#080b12;color:#eef2ff;margin:0;padding:24px}main{max-width:560px;margin:auto}h1{letter-spacing:-.04em}p{color:#9aa7bd;line-height:1.5}label{display:block;margin:16px 0 6px;font-size:12px;color:#9aa7bd}input{box-sizing:border-box;width:100%;padding:12px;border-radius:10px;border:1px solid #29344b;background:#101725;color:#fff}button{margin-top:20px;padding:12px 18px;border:0;border-radius:10px;background:#5ee8c7;color:#06110f;font-weight:800}.card{padding:22px;border:1px solid #263149;border-radius:18px;background:#101725}code{color:#f8c764}</style></head><body><main><div class="card"><small>LOCAL DEVICE</small><h1>AgentDisplay setup</h1><p>Configure normal Wi-Fi and the AgentDisplay host. The direct network stays available at <code>)HTML";
    html += ip;
    html += R"HTML(</code>.</p><form method="post" action="/api/config"><label>Wi-Fi SSID</label><input name="ssid" value=")HTML";
    html += htmlEscape(wifiSsid);
    html += R"HTML("><label>Wi-Fi password</label><input name="password" type="password" placeholder="Leave unchanged when blank"><label>Host URL</label><input name="host" value=")HTML";
    html += htmlEscape(hostUrl);
    html += R"HTML(" placeholder="http://192.168.1.20:5277"><label>Pairing key</label><input name="key" value=")HTML";
    html += htmlEscape(pairingKey);
    html += R"HTML("><button type="submit">Save and restart</button></form></div></main></body></html>)HTML";
    return html;
}
void beginWeb() {
    const char* headerKeys[] = { "X-AgentDisplay-Key" };
    web.collectHeaders(headerKeys, 1);
    web.onNotFound([] { if (web.method() == HTTP_OPTIONS) { cors(); web.send(204); } else { cors(); web.send(404, "application/json", "{\"error\":\"not found\"}"); } });
    web.on("/", HTTP_GET, [] { if (!requireDeviceAccess(true)) return; cors(); web.send(200, "text/html", setupPage()); });
    web.on("/api/status", HTTP_GET, [] {
        if (!requireDeviceAccess()) return;
        cors();
        String json = "{\"name\":\"" + apName + "\",\"wifi\":" + String(WiFi.status() == WL_CONNECTED ? "true" : "false") + ",\"ip\":\"" + WiFi.localIP().toString() + "\",\"updatedMs\":" + state.updatedAt + "}";
        web.send(200, "application/json", json);
    });
    web.on("/api/snapshot", HTTP_POST, [] { if (!requireDeviceAccess()) return; cors(); const bool ok = applySnapshot(web.arg("plain")); web.send(ok ? 202 : 400, "application/json", ok ? "{\"accepted\":true}" : "{\"accepted\":false}"); });
    web.on("/api/config", HTTP_POST, [] {
        if (!requireDeviceAccess(true)) return;
        preferences.begin("agentdisplay", false);
        preferences.putString("ssid", web.arg("ssid"));
        if (web.arg("password").length()) preferences.putString("wifiPass", web.arg("password"));
        preferences.putString("host", cleanBaseUrl(web.arg("host")));
        preferences.putString("key", web.arg("key"));
        preferences.end(); cors(); web.send(200, "text/html", "<h1>Saved</h1><p>AgentDisplay is restarting.</p>"); delay(500); ESP.restart();
    });
    web.begin();
}

void loadConfig() {
    preferences.begin("agentdisplay", false);
    wifiSsid = preferences.getString("ssid", ""); wifiPassword = preferences.getString("wifiPass", ""); hostUrl = cleanBaseUrl(preferences.getString("host", "")); pairingKey = preferences.getString("key", ""); apPassword = preferences.getString("apPass", "");
    if (apPassword.length() < 12) {
        char generated[18]; snprintf(generated, sizeof(generated), "AD%08lX%04X", static_cast<unsigned long>(esp_random()), static_cast<unsigned>(esp_random() & 0xFFFF)); apPassword = generated; preferences.putString("apPass", apPassword);
    }
    preferences.end();
    const String suffix = chipSuffix(); apName = "AgentDisplay-" + suffix;
}
void beginNetwork() {
    WiFi.mode(WIFI_AP_STA); WiFi.setSleep(true); WiFi.softAP(apName.c_str(), apPassword.c_str());
    if (wifiSsid.length()) { WiFi.begin(wifiSsid.c_str(), wifiPassword.c_str()); const auto deadline = millis() + 8'000; while (WiFi.status() != WL_CONNECTED && millis() < deadline) { delay(120); lv_timer_handler(); } }
    if (WiFi.status() == WL_CONNECTED) { MDNS.begin("agentdisplay"); ui.connection(WiFi.localIP().toString(), true); }
    else ui.connection("direct " + WiFi.softAPIP().toString(), false);
    ui.provisioning(apName, apPassword, WiFi.softAPIP().toString(), hostUrl.isEmpty());
    Serial.printf("Direct setup: %s / %s / http://%s\n", apName.c_str(), apPassword.c_str(), WiFi.softAPIP().toString().c_str());
}

void pullSnapshot() {
    if (WiFi.status() != WL_CONNECTED || hostUrl.isEmpty() || millis() - lastPull < board::SnapshotIntervalMs) return;
    lastPull = millis();
    HTTPClient http; http.setConnectTimeout(1200); http.setTimeout(1800); http.begin(hostUrl + "/api/v1/device/snapshot");
    if (pairingKey.length()) http.addHeader("X-AgentDisplay-Key", pairingKey);
    const int status = http.GET();
    if (status == HTTP_CODE_OK) { applySnapshot(http.getString()); ui.connection(WiFi.localIP().toString(), true); }
    else if (status > 0) ui.connection("host " + String(status), false);
    http.end();
}
void decideGate(bool allow) {
    if (!state.gate.pending || hostUrl.isEmpty()) return;
    HTTPClient http; http.setConnectTimeout(1500); http.setTimeout(2500); http.begin(hostUrl + "/api/v1/gates/" + state.gate.id + "/decision");
    http.addHeader("Content-Type", "application/json"); if (pairingKey.length()) http.addHeader("X-AgentDisplay-Key", pairingKey);
    const String body = String("{\"decision\":\"") + (allow ? "Allow" : "Deny") + "\",\"actor\":\"e32r40t\"}";
    const int status = http.POST(body); http.end();
    if (status >= 200 && status < 300) { state.gate.pending = false; ui.refresh(); }
    else ui.connection("gate send failed", false);
}

void beginDisplay() {
    pinMode(board::Backlight, OUTPUT); digitalWrite(board::Backlight, HIGH);
    pinMode(board::LedRed, OUTPUT); pinMode(board::LedGreen, OUTPUT); pinMode(board::LedBlue, OUTPUT); digitalWrite(board::LedRed, HIGH); digitalWrite(board::LedGreen, HIGH); digitalWrite(board::LedBlue, HIGH);
    tft.begin(); tft.setRotation(0); tft.fillScreen(TFT_BLACK);
    uint16_t calibration[5] = { 280, 3620, 260, 3560, 4 }; tft.setTouch(calibration);
    lv_init(); lv_disp_draw_buf_init(&drawBuffer, pixels, nullptr, board::ScreenWidth * 24);
    static lv_disp_drv_t display; lv_disp_drv_init(&display); display.hor_res = board::ScreenWidth; display.ver_res = board::ScreenHeight; display.flush_cb = displayFlush; display.draw_buf = &drawBuffer; lv_disp_drv_register(&display);
    static lv_indev_drv_t input; lv_indev_drv_init(&input); input.type = LV_INDEV_TYPE_POINTER; input.read_cb = touchRead; lv_indev_drv_register(&input);
    ui.begin(decideGate);
}
}

void setup() {
    Serial.begin(115200); delay(150);
    loadConfig(); beginDisplay(); beginBle(); beginNetwork(); beginWeb();
}

void loop() {
    web.handleClient(); applyQueuedSnapshot(); pullSnapshot(); lv_timer_handler(); delay(board::UiTickMs);
}
