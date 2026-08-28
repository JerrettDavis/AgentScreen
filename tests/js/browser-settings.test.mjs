import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

function loadBridge(stored = {}) {
  const values = new Map(Object.entries(stored));
  const context = {
    window: {},
    navigator: { languages: ['en-US'], language: 'en-US' },
    localStorage: {
      getItem: key => values.get(key) ?? null,
      setItem: (key, value) => values.set(key, value)
    },
    sessionStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    history: { replaceState() {} },
    location: { href: 'http://localhost/' },
    URL,
    TextEncoder,
    setTimeout,
    fetch: async () => ({ ok: true, json: async () => ({}) })
  };
  vm.runInNewContext(fs.readFileSync('src/AgentDisplay.Web/wwwroot/js/agentdisplay.js', 'utf8'), context);
  return { bridge: context.window.agentDisplay, values };
}

test('automatic sync defaults to 30 seconds and persists supported intervals', () => {
  const { bridge, values } = loadBridge();
  assert.equal(bridge.autoSyncIntervalSeconds(), 30);
  bridge.setAutoSyncIntervalSeconds(300);
  assert.equal(bridge.autoSyncIntervalSeconds(), 300);
  assert.equal(values.get('agentdisplay.auto-sync-interval-seconds'), '300');
});

test('automatic sync rejects unsupported intervals and repairs stale storage', () => {
  const { bridge } = loadBridge({ 'agentdisplay.auto-sync-interval-seconds': '7' });
  assert.equal(bridge.autoSyncIntervalSeconds(), 30);
  assert.throws(() => bridge.setAutoSyncIntervalSeconds(7), /Unsupported automatic sync interval/);
});
