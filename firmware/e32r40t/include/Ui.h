#pragma once
#include <Arduino.h>
#include <lvgl.h>
#include "DeviceState.h"

class Ui {
public:
    using DecisionHandler = void (*)(bool allow);
    explicit Ui(DeviceState& state) : state_(state) {}
    void begin(DecisionHandler handler);
    void refresh();
    void connection(const String& text, bool online);
    void provisioning(const String& ssid, const String& password, const String& address, bool show);
private:
    DeviceState& state_;
    DecisionHandler decision_ = nullptr;
    lv_obj_t* status_ = nullptr;
    lv_obj_t* activeValue_ = nullptr;
    lv_obj_t* spendValue_ = nullptr;
    lv_obj_t* hourlyValue_ = nullptr;
    lv_obj_t* cacheValue_ = nullptr;
    lv_obj_t* providerNames_[3]{};
    lv_obj_t* providerBars_[3]{};
    lv_obj_t* providerValues_[3]{};
    lv_obj_t* sessionList_ = nullptr;
    lv_obj_t* fullSessionList_ = nullptr;
    lv_obj_t* statsText_ = nullptr;
    lv_obj_t* gateLayer_ = nullptr;
    lv_obj_t* gateTitle_ = nullptr;
    lv_obj_t* gateCommand_ = nullptr;
    lv_obj_t* gateReason_ = nullptr;
    lv_obj_t* detailLayer_ = nullptr;
    lv_obj_t* detailTitle_ = nullptr;
    lv_obj_t* detailBody_ = nullptr;
    lv_obj_t* setupLayer_ = nullptr;
    lv_obj_t* setupNetwork_ = nullptr;
    lv_obj_t* setupPassword_ = nullptr;
    lv_obj_t* setupAddress_ = nullptr;

    void buildDashboard(lv_obj_t* parent);
    void buildSessions(lv_obj_t* parent);
    void buildStats(lv_obj_t* parent);
    void buildGate();
    void buildDetail();
    void buildSetup();
    void refreshSessions();
    static void gateEvent(lv_event_t* event);
    static void sessionEvent(lv_event_t* event);
    static void backEvent(lv_event_t* event);
    static void setupEvent(lv_event_t* event);
    static void showSetupEvent(lv_event_t* event);
};
