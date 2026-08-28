#include "Ui.h"

namespace {
lv_color_t color(uint32_t value) { return lv_color_hex(value); }
void panel(lv_obj_t* object, int radius = 12) {
    lv_obj_set_style_bg_color(object, color(0x111827), 0);
    lv_obj_set_style_bg_opa(object, LV_OPA_COVER, 0);
    lv_obj_set_style_border_color(object, color(0x243149), 0);
    lv_obj_set_style_border_width(object, 1, 0);
    lv_obj_set_style_radius(object, radius, 0);
    lv_obj_set_style_pad_all(object, 10, 0);
}
lv_obj_t* label(lv_obj_t* parent, const char* text, const lv_font_t* font, uint32_t hex) {
    auto* result = lv_label_create(parent);
    lv_label_set_text(result, text);
    lv_obj_set_style_text_font(result, font, 0);
    lv_obj_set_style_text_color(result, color(hex), 0);
    return result;
}
void formatCompact(char* output, size_t size, double value, const char* prefix = "") {
    const double absolute = value < 0 ? -value : value;
    if (absolute >= 1'000'000'000) snprintf(output, size, "%s%.1fB", prefix, value / 1'000'000'000.0);
    else if (absolute >= 1'000'000) snprintf(output, size, "%s%.1fM", prefix, value / 1'000'000.0);
    else if (absolute >= 1'000) snprintf(output, size, "%s%.1fK", prefix, value / 1'000.0);
    else if (absolute >= 100) snprintf(output, size, "%s%.0f", prefix, value);
    else snprintf(output, size, "%s%.2f", prefix, value);
}
void metric(lv_obj_t* parent, const char* caption, lv_obj_t** value, int x, int y, int width) {
    auto* box = lv_obj_create(parent); panel(box, 10); lv_obj_set_size(box, width, 63); lv_obj_set_pos(box, x, y); lv_obj_clear_flag(box, LV_OBJ_FLAG_SCROLLABLE);
    auto* cap = label(box, caption, &lv_font_montserrat_10, 0x7e8ca5); lv_obj_align(cap, LV_ALIGN_TOP_LEFT, 0, 0);
    *value = label(box, "--", &lv_font_montserrat_18, 0xf2f5fb); lv_obj_set_width(*value, width - 20); lv_label_set_long_mode(*value, LV_LABEL_LONG_DOT); lv_obj_align(*value, LV_ALIGN_BOTTOM_LEFT, 0, 0);
}
}

void Ui::begin(DecisionHandler decisionHandler, RefreshHandler refreshHandler) {
    decision_ = decisionHandler;
    refreshHandler_ = refreshHandler;
    lv_obj_set_style_bg_color(lv_scr_act(), color(0x070a11), 0);
    auto* tabs = lv_tabview_create(lv_scr_act(), LV_DIR_BOTTOM, 44);
    lv_obj_set_style_bg_color(tabs, color(0x070a11), 0);
    lv_obj_set_style_border_width(tabs, 0, 0);
    auto* tabButtons = lv_tabview_get_tab_btns(tabs);
    lv_obj_set_style_bg_color(tabButtons, color(0x0d1421), 0);
    lv_obj_set_style_text_color(tabButtons, color(0x8491a8), 0);
    lv_obj_set_style_text_color(tabButtons, color(0x5ee8c7), LV_PART_ITEMS | LV_STATE_CHECKED);
    auto* dashboard = lv_tabview_add_tab(tabs, "OVERVIEW");
    auto* sessions = lv_tabview_add_tab(tabs, "SESSIONS");
    auto* stats = lv_tabview_add_tab(tabs, "STATS");
    buildDashboard(dashboard); buildSessions(sessions); buildStats(stats); buildDetail();
    auto* wifi = lv_btn_create(lv_layer_top()); lv_obj_set_size(wifi, 42, 34); lv_obj_set_pos(wifi, 268, 4); lv_obj_set_style_bg_color(wifi, color(0x192235), 0); lv_obj_set_style_border_color(wifi, color(0x5ee8c7), 0); lv_obj_set_style_border_width(wifi, 1, 0); lv_obj_add_event_cb(wifi, showSetupEvent, LV_EVENT_CLICKED, this); auto* wifiText = label(wifi, "WIFI", &lv_font_montserrat_10, 0x5ee8c7); lv_obj_center(wifiText);
    buildSetup(); buildGate();
    refresh();
}

void Ui::buildDashboard(lv_obj_t* parent) {
    lv_obj_set_style_bg_color(parent, color(0x070a11), 0); lv_obj_set_style_pad_all(parent, 10, 0);
    auto* title = label(parent, "AgentDisplay", &lv_font_montserrat_20, 0xf2f5fb); lv_obj_set_pos(title, 3, 0);
    auto* subtitle = label(parent, "LOCAL AGENT OPERATIONS", &lv_font_montserrat_10, 0x5ee8c7); lv_obj_set_pos(subtitle, 4, 25);
    status_ = label(parent, "starting", &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_width(status_, 82); lv_label_set_long_mode(status_, LV_LABEL_LONG_DOT); lv_obj_align(status_, LV_ALIGN_TOP_RIGHT, -47, 6);
    metric(parent, "ACTIVE", &activeValue_, 0, 48, 68);
    metric(parent, "SPEND", &spendValue_, 75, 48, 95);
    metric(parent, "COST / HR", &hourlyValue_, 177, 48, 103);
    metric(parent, "CACHE", &cacheValue_, 0, 118, 86);

    auto* providers = lv_obj_create(parent); panel(providers, 12); lv_obj_set_size(providers, 187, 132); lv_obj_set_pos(providers, 93, 118); lv_obj_clear_flag(providers, LV_OBJ_FLAG_SCROLLABLE);
    auto* heading = label(providers, "USAGE WINDOWS", &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(heading, 0, 0);
    for (int i = 0; i < 3; ++i) {
        providerNames_[i] = label(providers, "--", &lv_font_montserrat_10, 0xd9dfeb); lv_obj_set_width(providerNames_[i], 118); lv_label_set_long_mode(providerNames_[i], LV_LABEL_LONG_DOT); lv_obj_set_pos(providerNames_[i], 0, 23 + i * 31);
        providerValues_[i] = label(providers, "0%", &lv_font_montserrat_10, 0x5ee8c7); lv_obj_align(providerValues_[i], LV_ALIGN_TOP_RIGHT, 0, 23 + i * 31);
        providerBars_[i] = lv_bar_create(providers); lv_obj_set_size(providerBars_[i], 161, 5); lv_obj_set_pos(providerBars_[i], 0, 40 + i * 31); lv_bar_set_range(providerBars_[i], 0, 100);
        lv_obj_set_style_bg_color(providerBars_[i], color(0x263044), LV_PART_MAIN); lv_obj_set_style_bg_color(providerBars_[i], i == 1 ? color(0x8da2ff) : i == 2 ? color(0x5fc6ff) : color(0x5ee8c7), LV_PART_INDICATOR);
    }
    auto* recent = label(parent, "RECENT SESSIONS", &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(recent, 2, 266);
    sessionList_ = lv_obj_create(parent); lv_obj_set_size(sessionList_, 280, 112); lv_obj_set_pos(sessionList_, 0, 284); lv_obj_set_style_bg_opa(sessionList_, LV_OPA_TRANSP, 0); lv_obj_set_style_border_width(sessionList_, 0, 0); lv_obj_set_style_pad_all(sessionList_, 0, 0); lv_obj_set_flex_flow(sessionList_, LV_FLEX_FLOW_COLUMN); lv_obj_set_style_pad_row(sessionList_, 5, 0);
}

void Ui::buildSessions(lv_obj_t* parent) {
    lv_obj_set_style_bg_color(parent, color(0x070a11), 0); lv_obj_set_style_pad_all(parent, 10, 0);
    auto* title = label(parent, "Sessions", &lv_font_montserrat_20, 0xf2f5fb); lv_obj_set_pos(title, 3, 0);
    auto* note = label(parent, "Tap a row for redacted details", &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(note, 3, 27);
    fullSessionList_ = lv_obj_create(parent); lv_obj_set_size(fullSessionList_, 280, 337); lv_obj_set_pos(fullSessionList_, 0, 48); lv_obj_set_style_bg_opa(fullSessionList_, LV_OPA_TRANSP, 0); lv_obj_set_style_border_width(fullSessionList_, 0, 0); lv_obj_set_style_pad_all(fullSessionList_, 0, 0); lv_obj_set_flex_flow(fullSessionList_, LV_FLEX_FLOW_COLUMN); lv_obj_set_style_pad_row(fullSessionList_, 6, 0);
}

void Ui::buildStats(lv_obj_t* parent) {
    lv_obj_set_style_bg_color(parent, color(0x070a11), 0); lv_obj_set_style_pad_all(parent, 10, 0);
    auto* title = label(parent, "Statistics", &lv_font_montserrat_20, 0xf2f5fb); lv_obj_set_pos(title, 3, 0);
    auto* refreshButton = lv_btn_create(parent); lv_obj_set_size(refreshButton, 92, 34); lv_obj_set_pos(refreshButton, 170, 0); lv_obj_set_style_bg_color(refreshButton, color(0x192235), 0); lv_obj_set_style_border_color(refreshButton, color(0x5ee8c7), 0); lv_obj_set_style_border_width(refreshButton, 1, 0); lv_obj_add_event_cb(refreshButton, refreshEvent, LV_EVENT_CLICKED, this); auto* refreshText = label(refreshButton, "REFRESH NOW", &lv_font_montserrat_10, 0x5ee8c7); lv_obj_center(refreshText);
    statsText_ = label(parent, "Waiting for a snapshot", &lv_font_montserrat_14, 0xd9dfeb); lv_obj_set_size(statsText_, 278, 330); lv_obj_set_pos(statsText_, 3, 47); lv_label_set_long_mode(statsText_, LV_LABEL_LONG_WRAP);
}

void Ui::buildDetail() {
    detailLayer_ = lv_obj_create(lv_layer_top()); lv_obj_set_size(detailLayer_, 320, 436); lv_obj_align(detailLayer_, LV_ALIGN_TOP_MID, 0, 0); panel(detailLayer_, 0); lv_obj_set_style_bg_color(detailLayer_, color(0x090d16), 0); lv_obj_add_flag(detailLayer_, LV_OBJ_FLAG_HIDDEN); lv_obj_clear_flag(detailLayer_, LV_OBJ_FLAG_SCROLLABLE);
    auto* back = lv_btn_create(detailLayer_); lv_obj_set_size(back, 66, 33); lv_obj_set_pos(back, 0, 0); lv_obj_set_style_bg_color(back, color(0x192235), 0); lv_obj_add_event_cb(back, backEvent, LV_EVENT_CLICKED, this); auto* backText = label(back, "< BACK", &lv_font_montserrat_10, 0x9aa7bd); lv_obj_center(backText);
    detailTitle_ = label(detailLayer_, "Session", &lv_font_montserrat_20, 0xf2f5fb); lv_obj_set_pos(detailTitle_, 0, 51);
    detailBody_ = label(detailLayer_, "", &lv_font_montserrat_12, 0xaeb8ca); lv_obj_set_size(detailBody_, 290, 310); lv_obj_set_pos(detailBody_, 0, 86); lv_label_set_long_mode(detailBody_, LV_LABEL_LONG_WRAP);
}

void Ui::buildGate() {
    gateLayer_ = lv_obj_create(lv_layer_top()); lv_obj_set_size(gateLayer_, 304, 330); lv_obj_align(gateLayer_, LV_ALIGN_CENTER, 0, -10); panel(gateLayer_, 18); lv_obj_set_style_bg_color(gateLayer_, color(0x211b14), 0); lv_obj_set_style_border_color(gateLayer_, color(0xf8c764), 0); lv_obj_set_style_border_width(gateLayer_, 2, 0); lv_obj_add_flag(gateLayer_, LV_OBJ_FLAG_HIDDEN); lv_obj_clear_flag(gateLayer_, LV_OBJ_FLAG_SCROLLABLE);
    auto* badge = label(gateLayer_, "APPROVAL REQUIRED", &lv_font_montserrat_10, 0xf8c764); lv_obj_align(badge, LV_ALIGN_TOP_LEFT, 2, 2);
    gateTitle_ = label(gateLayer_, "Tool request", &lv_font_montserrat_20, 0xf2f5fb); lv_obj_set_size(gateTitle_, 272, 55); lv_obj_set_pos(gateTitle_, 2, 28); lv_label_set_long_mode(gateTitle_, LV_LABEL_LONG_WRAP);
    gateCommand_ = label(gateLayer_, "", &lv_font_montserrat_12, 0xf8d98c); lv_obj_set_size(gateCommand_, 272, 72); lv_obj_set_pos(gateCommand_, 2, 91); lv_label_set_long_mode(gateCommand_, LV_LABEL_LONG_WRAP);
    gateReason_ = label(gateLayer_, "", &lv_font_montserrat_10, 0x9ca8bb); lv_obj_set_size(gateReason_, 272, 52); lv_obj_set_pos(gateReason_, 2, 171); lv_label_set_long_mode(gateReason_, LV_LABEL_LONG_WRAP);
    const char* captions[] = { "DENY", "ALLOW ONCE" };
    for (int i = 0; i < 2; ++i) {
        auto* button = lv_btn_create(gateLayer_); lv_obj_set_size(button, i == 0 ? 105 : 151, 50); lv_obj_set_pos(button, i == 0 ? 2 : 119, 247); lv_obj_set_style_bg_color(button, i == 0 ? color(0x3a1d25) : color(0x5ee8c7), 0); lv_obj_add_event_cb(button, gateEvent, LV_EVENT_CLICKED, this); lv_obj_set_user_data(button, reinterpret_cast<void*>(static_cast<intptr_t>(i)));
        auto* text = label(button, captions[i], &lv_font_montserrat_12, i == 0 ? 0xff8793 : 0x06110f); lv_obj_center(text);
    }
}

void Ui::buildSetup() {
    setupLayer_ = lv_obj_create(lv_layer_top()); lv_obj_set_size(setupLayer_, 304, 350); lv_obj_align(setupLayer_, LV_ALIGN_CENTER, 0, -8); panel(setupLayer_, 18); lv_obj_set_style_bg_color(setupLayer_, color(0x101827), 0); lv_obj_set_style_border_color(setupLayer_, color(0x5ee8c7), 0); lv_obj_set_style_border_width(setupLayer_, 2, 0); lv_obj_add_flag(setupLayer_, LV_OBJ_FLAG_HIDDEN); lv_obj_clear_flag(setupLayer_, LV_OBJ_FLAG_SCROLLABLE);
    auto* badge = label(setupLayer_, "FIRST-RUN SETUP", &lv_font_montserrat_10, 0x5ee8c7); lv_obj_set_pos(badge, 2, 2);
    auto* title = label(setupLayer_, "Connect this display", &lv_font_montserrat_20, 0xf2f5fb); lv_obj_set_pos(title, 2, 30);
    auto* instructions = label(setupLayer_, "Join the direct Wi-Fi network, then open the address below.", &lv_font_montserrat_10, 0x9ca8bb); lv_obj_set_size(instructions, 270, 42); lv_obj_set_pos(instructions, 2, 61); lv_label_set_long_mode(instructions, LV_LABEL_LONG_WRAP);
    auto* networkCaption = label(setupLayer_, "NETWORK", &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(networkCaption, 2, 113);
    setupNetwork_ = label(setupLayer_, "AgentDisplay", &lv_font_montserrat_14, 0xf2f5fb); lv_obj_set_pos(setupNetwork_, 2, 132);
    auto* passwordCaption = label(setupLayer_, "PASSWORD", &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(passwordCaption, 2, 165);
    setupPassword_ = label(setupLayer_, "--", &lv_font_montserrat_16, 0xf8c764); lv_obj_set_pos(setupPassword_, 2, 184);
    auto* addressCaption = label(setupLayer_, "SETUP ADDRESS", &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(addressCaption, 2, 220);
    setupAddress_ = label(setupLayer_, "http://192.168.4.1", &lv_font_montserrat_14, 0x8da2ff); lv_obj_set_pos(setupAddress_, 2, 239);
    auto* button = lv_btn_create(setupLayer_); lv_obj_set_size(button, 270, 48); lv_obj_set_pos(button, 2, 278); lv_obj_set_style_bg_color(button, color(0x5ee8c7), 0); lv_obj_add_event_cb(button, setupEvent, LV_EVENT_CLICKED, this); auto* text = label(button, "CONTINUE TO DASHBOARD", &lv_font_montserrat_12, 0x06110f); lv_obj_center(text);
}

void Ui::refresh() {
    char text[256];
    formatCompact(text, sizeof(text), state_.stats.active); lv_label_set_text(activeValue_, text);
    formatCompact(text, sizeof(text), state_.stats.spend, "$"); lv_label_set_text(spendValue_, text);
    formatCompact(text, sizeof(text), state_.stats.hourly, "$"); lv_label_set_text(hourlyValue_, text);
    snprintf(text, sizeof(text), "%d%%", state_.stats.cacheRate); lv_label_set_text(cacheValue_, text);
    for (int i = 0; i < 3; ++i) {
        if (i < state_.providerCount) {
            String name = state_.providers[i].name + " " + state_.providers[i].window; lv_label_set_text(providerNames_[i], name.c_str()); snprintf(text, sizeof(text), "%d%%", state_.providers[i].usage); lv_label_set_text(providerValues_[i], text); lv_bar_set_value(providerBars_[i], state_.providers[i].usage, LV_ANIM_ON);
        } else { lv_label_set_text(providerNames_[i], "--"); lv_label_set_text(providerValues_[i], "0%"); lv_bar_set_value(providerBars_[i], 0, LV_ANIM_OFF); }
    }
    char spend[24], hourly[24]; formatCompact(spend, sizeof(spend), state_.stats.spend, "$"); formatCompact(hourly, sizeof(hourly), state_.stats.hourly, "$");
    snprintf(text, sizeof(text), "SESSIONS        %d\nACTIVE          %d\nPROJECTS        %d\nPENDING GATES   %d\n\nEST. SPEND      %s\nCOST / HOUR     %s\nCACHE HIT       %d%%\nCACHE BREAKS    %d\n\nUpdated %lus ago\nProtocol v1",
        state_.stats.sessions, state_.stats.active, state_.stats.projects, state_.stats.gates, spend, hourly, state_.stats.cacheRate, state_.stats.cacheBreaks, (millis() - state_.updatedAt) / 1000);
    lv_label_set_text(statsText_, text);
    refreshSessions();
    if (state_.gate.pending) {
        snprintf(text, sizeof(text), "%s wants %s", state_.gate.project.c_str(), state_.gate.tool.c_str()); lv_label_set_text(gateTitle_, text); lv_label_set_text(gateCommand_, state_.gate.summary.c_str()); lv_label_set_text(gateReason_, state_.gate.reason.c_str()); lv_obj_clear_flag(gateLayer_, LV_OBJ_FLAG_HIDDEN);
    } else lv_obj_add_flag(gateLayer_, LV_OBJ_FLAG_HIDDEN);
}

void Ui::refreshSessions() {
    lv_obj_clean(sessionList_);
    for (int i = 0; i < state_.sessionCount && i < 3; ++i) {
        auto* row = lv_obj_create(sessionList_); panel(row, 9); lv_obj_set_width(row, 280); lv_obj_set_height(row, 48); lv_obj_clear_flag(row, LV_OBJ_FLAG_SCROLLABLE);
        auto* name = label(row, state_.sessions[i].project.c_str(), &lv_font_montserrat_12, 0xf2f5fb); lv_obj_set_width(name, 160); lv_label_set_long_mode(name, LV_LABEL_LONG_DOT); lv_obj_set_pos(name, 0, 0);
        String caption = state_.sessions[i].provider + " / " + state_.sessions[i].state; auto* meta = label(row, caption.c_str(), &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(meta, 0, 19);
        char money[20], value[32]; formatCompact(money, sizeof(money), state_.sessions[i].costUnits / 10000.0, "$"); snprintf(value, sizeof(value), "%dk  %s", state_.sessions[i].tokensK, money); auto* amount = label(row, value, &lv_font_montserrat_10, 0x5ee8c7); lv_obj_set_width(amount, 100); lv_label_set_long_mode(amount, LV_LABEL_LONG_DOT); lv_obj_align(amount, LV_ALIGN_RIGHT_MID, 0, 0);
    }
    if (!fullSessionList_) return;
    lv_obj_clean(fullSessionList_);
    for (int i = 0; i < state_.sessionCount; ++i) {
        auto* row = lv_btn_create(fullSessionList_); lv_obj_set_width(row, 280); lv_obj_set_height(row, 61); lv_obj_set_style_bg_color(row, color(0x111827), 0); lv_obj_set_style_border_color(row, color(0x243149), 0); lv_obj_set_style_border_width(row, 1, 0); lv_obj_set_style_radius(row, 10, 0); lv_obj_add_event_cb(row, sessionEvent, LV_EVENT_CLICKED, this); lv_obj_set_user_data(row, reinterpret_cast<void*>(static_cast<intptr_t>(i)));
        auto* name = label(row, state_.sessions[i].project.c_str(), &lv_font_montserrat_12, 0xf2f5fb); lv_obj_set_pos(name, 1, 1);
        String metaText = state_.sessions[i].provider + " / " + state_.sessions[i].model; auto* meta = label(row, metaText.c_str(), &lv_font_montserrat_10, 0x7e8ca5); lv_obj_set_pos(meta, 1, 23);
        auto* state = label(row, state_.sessions[i].state.c_str(), &lv_font_montserrat_10, 0x5ee8c7); lv_obj_align(state, LV_ALIGN_RIGHT_MID, 0, 0);
    }
}

void Ui::connection(const String& text, bool online) { lv_label_set_text(status_, text.c_str()); lv_obj_set_style_text_color(status_, online ? color(0x5ee8c7) : color(0xf8c764), 0); }
void Ui::provisioning(const String& ssid, const String& password, const String& address, bool show) { lv_label_set_text(setupNetwork_, ssid.c_str()); lv_label_set_text(setupPassword_, password.c_str()); String url = "http://" + address; lv_label_set_text(setupAddress_, url.c_str()); if (show) lv_obj_clear_flag(setupLayer_, LV_OBJ_FLAG_HIDDEN); else lv_obj_add_flag(setupLayer_, LV_OBJ_FLAG_HIDDEN); }
void Ui::gateEvent(lv_event_t* event) { auto* self = static_cast<Ui*>(lv_event_get_user_data(event)); auto* target = lv_event_get_target(event); const bool allow = reinterpret_cast<intptr_t>(lv_obj_get_user_data(target)) == 1; if (self->decision_) self->decision_(allow); }
void Ui::sessionEvent(lv_event_t* event) { auto* self = static_cast<Ui*>(lv_event_get_user_data(event)); const int index = static_cast<int>(reinterpret_cast<intptr_t>(lv_obj_get_user_data(lv_event_get_target(event)))); if (index < 0 || index >= self->state_.sessionCount) return; const auto& session = self->state_.sessions[index]; lv_label_set_text(self->detailTitle_, session.project.c_str()); char body[400]; snprintf(body, sizeof(body), "%s / %s\n\nSTATUS\n%s\n\nMODEL\n%s\n\nOBSERVED TOKENS\n%dk\n\nAPI-EQUIVALENT COST\n$%.4f\n\nSession ID\n%s", session.provider.c_str(), session.state.c_str(), session.state.c_str(), session.model.c_str(), session.tokensK, session.costUnits / 10000.0f, session.id.c_str()); lv_label_set_text(self->detailBody_, body); lv_obj_clear_flag(self->detailLayer_, LV_OBJ_FLAG_HIDDEN); }
void Ui::backEvent(lv_event_t* event) { auto* self = static_cast<Ui*>(lv_event_get_user_data(event)); lv_obj_add_flag(self->detailLayer_, LV_OBJ_FLAG_HIDDEN); }
void Ui::setupEvent(lv_event_t* event) { auto* self = static_cast<Ui*>(lv_event_get_user_data(event)); lv_obj_add_flag(self->setupLayer_, LV_OBJ_FLAG_HIDDEN); }
void Ui::showSetupEvent(lv_event_t* event) { auto* self = static_cast<Ui*>(lv_event_get_user_data(event)); lv_obj_clear_flag(self->setupLayer_, LV_OBJ_FLAG_HIDDEN); }
void Ui::refreshEvent(lv_event_t* event) { auto* self = static_cast<Ui*>(lv_event_get_user_data(event)); if (self->refreshHandler_) self->refreshHandler_(); }
