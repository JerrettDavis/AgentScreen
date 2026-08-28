#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const args = parseArgs(process.argv.slice(2));
const selected = String(args.provider || 'all').toLowerCase();
const dryRun = !args.apply;
const jsonOutput = Boolean(args.json);
const home = process.env.AGENTDISPLAY_HOME || os.homedir();
const relay = fileURLToPath(new URL('./relay.mjs', import.meta.url));
const providers = selected === 'all' ? ['claude', 'codex', 'copilot'] : [selected];

export function mergeClaudeLike(current, provider, command) {
  const next = structuredClone(current || {});
  next.hooks ||= {};
  const events = ['SessionStart', 'UserPromptSubmit', 'PreToolUse', 'PermissionRequest', 'PostToolUse', 'Stop'];
  for (const event of events) {
    next.hooks[event] ||= [];
    const timeout = event === 'PreToolUse' || event === 'PermissionRequest' ? 110 : 10;
    const exists = next.hooks[event].some(group => JSON.stringify(group).includes('AgentDisplay') || JSON.stringify(group).includes(`--provider ${provider}`));
    if (!exists) next.hooks[event].push({ hooks: [{ type: 'command', command, timeout }] });
  }
  return next;
}

export function mergeCopilot(current, command) {
  const next = structuredClone(current || {});
  next.version ||= 1;
  next.hooks ||= {};
  const events = ['sessionStart', 'userPromptSubmitted', 'preToolUse', 'permissionRequest', 'postToolUse', 'sessionEnd'];
  for (const event of events) {
    next.hooks[event] ||= [];
    const timeoutSec = event === 'preToolUse' || event === 'permissionRequest' ? 110 : 10;
    const exists = next.hooks[event].some(hook => JSON.stringify(hook).includes('AgentDisplay') || JSON.stringify(hook).includes('--provider copilot'));
    if (!exists) next.hooks[event].push({ type: 'command', command, timeoutSec });
  }
  return next;
}

export function installOne(provider, options = {}) {
  const target = provider === 'claude'
    ? path.join(home, '.claude', 'settings.json')
    : provider === 'codex'
      ? path.join(home, '.codex', 'hooks.json')
      : path.join(home, '.copilot', 'hooks', 'agentdisplay.json');
  const command = `${quote(process.execPath)} ${quote(relay)} --provider ${provider}`;
  const current = readJson(target);
  const next = provider === 'copilot' ? mergeCopilot(current, command) : mergeClaudeLike(current, provider, command);
  const before = stable(current);
  const after = stable(next);
  const changed = before !== after;
  let backupPath = null;
  if (!dryRun && changed) {
    fs.mkdirSync(path.dirname(target), { recursive: true });
    if (fs.existsSync(target)) {
      backupPath = `${target}.agentdisplay.${timestamp()}.bak`;
      fs.copyFileSync(target, backupPath);
    }
    const temp = `${target}.tmp-${process.pid}`;
    fs.writeFileSync(temp, `${JSON.stringify(next, null, 2)}\n`, { mode: 0o600 });
    fs.renameSync(temp, target);
  }
  return {
    provider: provider === 'claude' ? 'Claude' : provider === 'codex' ? 'Codex' : 'Copilot',
    path: target,
    changed,
    dryRun,
    message: messageFor(provider, changed, dryRun),
    backupPath
  };
}

function messageFor(provider, changed, dryRun) {
  if (!changed) return provider === 'codex'
    ? 'AgentDisplay hook entries are already present. Review them once with /hooks before first use.'
    : 'AgentDisplay hook entries are already present.';
  const base = dryRun
    ? 'Hook entries would be added; existing configuration is preserved.'
    : 'Hook entries installed; existing configuration was preserved.';
  return provider === 'codex' ? `${base} Review and trust the hook once with /hooks before first use.` : base;
}

function readJson(target) {
  if (!fs.existsSync(target)) return {};
  try { return JSON.parse(fs.readFileSync(target, 'utf8')); }
  catch (error) { throw new Error(`Cannot merge invalid JSON in ${target}: ${error.message}`); }
}
function stable(value) { return JSON.stringify(sort(value)); }
function sort(value) { if (Array.isArray(value)) return value.map(sort); if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map(key => [key, sort(value[key])])); return value; }
function quote(value) { return process.platform === 'win32' ? `"${String(value).replaceAll('"', '\\"')}"` : `'${String(value).replaceAll("'", "'\\''")}'`; }
function timestamp() { return new Date().toISOString().replace(/[-:TZ.]/g, '').slice(0, 14); }
function parseArgs(values) { const result = {}; for (let i = 0; i < values.length; i++) if (values[i].startsWith('--')) { const key = values[i].slice(2); result[key] = values[i + 1]?.startsWith('--') || i + 1 >= values.length ? true : values[++i]; } return result; }

function main() {
  try {
    const results = providers.map(provider => installOne(provider));
    if (jsonOutput) process.stdout.write(`${JSON.stringify(results)}\n`);
    else for (const result of results) console.log(`${result.dryRun ? '[dry-run]' : '[applied]'} ${result.provider}: ${result.message} (${result.path})`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) main();
