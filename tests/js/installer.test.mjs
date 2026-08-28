import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const installer = path.join(root, 'integrations', 'hooks', 'install.mjs');

test('installer preserves existing hooks and writes a backup', () => {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'agentdisplay-hooks-'));
  const target = path.join(home, '.claude', 'settings.json');
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, JSON.stringify({ hooks: { SessionStart: [{ matcher: 'legacy', hooks: [{ type: 'command', command: 'echo legacy' }] }] }, theme: 'dark' }));
  const result = spawnSync(process.execPath, [installer, '--provider', 'claude', '--apply', '--json'], { encoding: 'utf8', env: { ...process.env, AGENTDISPLAY_HOME: home } });
  assert.equal(result.status, 0, result.stderr);
  const output = JSON.parse(result.stdout)[0];
  assert.equal(output.changed, true);
  assert.ok(output.backupPath && fs.existsSync(output.backupPath));
  const merged = JSON.parse(fs.readFileSync(target, 'utf8'));
  assert.equal(merged.theme, 'dark');
  assert.equal(merged.hooks.SessionStart[0].matcher, 'legacy');
  const installed = merged.hooks.PreToolUse.find(group => JSON.stringify(group).includes('--provider claude'));
  assert.ok(installed);
  assert.equal(Object.hasOwn(installed, 'matcher'), false);
});

test('dry-run reports changes without touching disk', () => {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'agentdisplay-hooks-'));
  const result = spawnSync(process.execPath, [installer, '--provider', 'codex', '--dry-run', '--json'], { encoding: 'utf8', env: { ...process.env, AGENTDISPLAY_HOME: home } });
  assert.equal(result.status, 0, result.stderr);
  const output = JSON.parse(result.stdout)[0];
  assert.equal(output.dryRun, true);
  assert.equal(output.changed, true);
  assert.equal(fs.existsSync(path.join(home, '.codex', 'hooks.json')), false);
  assert.match(output.message, /\/hooks/);
});

test('copilot installer creates a versioned user hook file', () => {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'agentdisplay-hooks-'));
  const result = spawnSync(process.execPath, [installer, '--provider', 'copilot', '--apply', '--json'], { encoding: 'utf8', env: { ...process.env, AGENTDISPLAY_HOME: home } });
  assert.equal(result.status, 0, result.stderr);
  const target = path.join(home, '.copilot', 'hooks', 'agentdisplay.json');
  const merged = JSON.parse(fs.readFileSync(target, 'utf8'));
  assert.equal(merged.version, 1);
  assert.ok(merged.hooks.preToolUse.some(hook => hook.timeoutSec === 110 && hook.command.includes('--provider copilot')));
});
