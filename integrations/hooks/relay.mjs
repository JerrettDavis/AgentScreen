#!/usr/bin/env node
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const args = parseArgs(process.argv.slice(2));
const provider = normalizeProvider(args.provider || process.env.AGENTDISPLAY_PROVIDER || 'claude');
const host = (args.host || process.env.AGENTDISPLAY_HOST || 'http://127.0.0.1:5277').replace(/\/$/, '');
const strict = String(process.env.AGENTDISPLAY_STRICT || '').toLowerCase() === 'true';
const maxWaitMs = Number(process.env.AGENTDISPLAY_GATE_TIMEOUT_MS || 95_000);
const pairingKey = process.env.AGENTDISPLAY_KEY || '';

export function normalizeProvider(value) {
  const normalized = String(value).toLowerCase();
  if (normalized === 'openai' || normalized === 'codex') return 'codex';
  if (normalized === 'github' || normalized === 'microsoft' || normalized === 'copilot') return 'copilot';
  return 'claude';
}

export function redact(value, key = '') {
  const sensitive = /(authorization|api.?key|token|password|secret|cookie|credential|private.?key)/i;
  if (sensitive.test(key)) return '[redacted]';
  if (typeof value === 'string') {
    return value
      .replace(/(Bearer\s+)[A-Za-z0-9._~+/=-]{12,}/gi, '$1[redacted]')
      .replace(/\b(sk-ant-|sk-proj-|ghp_|github_pat_|eyJ)[A-Za-z0-9._-]{12,}\b/gi, '[redacted-token]')
      .replace(/\r?\n/g, ' ')
      .slice(0, 1_000);
  }
  if (Array.isArray(value)) return value.slice(0, 40).map(item => redact(item, key));
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([name, item]) => [name, redact(item, name)]));
  return value;
}

export function normalizedEvent(providerName, input) {
  const eventName = input.hook_event_name || input.hookEventName || input.event_name || input.eventName || input.event || 'Unknown';
  const toolInput = input.tool_input ?? input.toolInput ?? input.toolArgs ?? input.input ?? input.arguments ?? null;
  return {
    provider: providerName[0].toUpperCase() + providerName.slice(1),
    eventName,
    sessionId: input.session_id || input.sessionId || input.conversation_id || input.conversationId || null,
    cwd: input.cwd || input.working_directory || input.workingDirectory || null,
    toolName: input.tool_name || input.toolName || input.tool || input.name || null,
    toolInput: redact(toolInput),
    prompt: redact(input.prompt || input.user_prompt || input.userPrompt || null),
    model: input.model || input.model_name || input.modelName || null,
    receivedAt: new Date().toISOString(),
    metadata: { sourceEvent: eventName, relayVersion: '0.1.0-alpha.1' }
  };
}

export function providerOutput(providerName, eventName, decision, reason) {
  const finalDecision = decision === 'deny' ? 'deny' : 'allow';
  const permissionEvent = String(eventName).toLowerCase() === 'permissionrequest';
  const gateEvent = permissionEvent || String(eventName).toLowerCase() === 'pretooluse';
  if (!gateEvent) return {};

  if (providerName === 'copilot') {
    return permissionEvent
      ? { behavior: finalDecision, message: reason, interrupt: finalDecision === 'deny' }
      : { permissionDecision: finalDecision, permissionDecisionReason: reason };
  }

  if (permissionEvent) {
    return {
      hookSpecificOutput: {
        hookEventName: eventName,
        decision: { behavior: finalDecision, message: reason }
      }
    };
  }

  return {
    hookSpecificOutput: {
      hookEventName: eventName,
      permissionDecision: finalDecision,
      permissionDecisionReason: reason
    }
  };
}

async function main() {
  const inputText = await readStdin();
  let input;
  try { input = inputText.trim() ? JSON.parse(inputText) : {}; }
  catch { return emit(providerOutput(provider, 'PreToolUse', strict ? 'deny' : 'allow', 'AgentDisplay received invalid hook JSON')); }

  const event = normalizedEvent(provider, input);
  try {
    const initial = await postJson(`${host}/api/v1/hooks/event`, event, 4_000);
    let decision = String(initial.decision || 'allow').toLowerCase();
    let reason = initial.reason || 'AgentDisplay policy result';
    if (decision === 'ask' && initial.gateId) {
      const result = await waitForGate(initial.gateId, initial.pollAfterMs || 350, maxWaitMs);
      decision = result.decision;
      reason = result.reason;
    }
    emit(providerOutput(provider, event.eventName, decision, reason));
  } catch (error) {
    emit(providerOutput(provider, event.eventName, strict ? 'deny' : 'allow', `AgentDisplay unavailable: ${error.message}`));
  }
}

async function waitForGate(gateId, pollAfterMs, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, Math.max(100, pollAfterMs)));
    const result = await getJson(`${host}/api/v1/gates/${encodeURIComponent(gateId)}`, 3_000);
    if (!result.pending) return { decision: String(result.decision || 'deny').toLowerCase(), reason: result.gate?.reason || 'AgentDisplay approval decision' };
  }
  return { decision: strict ? 'deny' : 'allow', reason: strict ? 'AgentDisplay approval timed out (strict mode)' : 'AgentDisplay approval timed out (fail open)' };
}

async function postJson(url, body, timeoutMs) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const headers = { 'content-type': 'application/json' };
    if (pairingKey) headers['X-AgentDisplay-Key'] = pairingKey;
    const response = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body), signal: controller.signal });
    if (!response.ok) throw new Error(`host returned HTTP ${response.status}`);
    return await response.json();
  } finally { clearTimeout(timeout); }
}

async function getJson(url, timeoutMs) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const headers = pairingKey ? { 'X-AgentDisplay-Key': pairingKey } : {};
    const response = await fetch(url, { headers, signal: controller.signal });
    if (!response.ok) throw new Error(`host returned HTTP ${response.status}`);
    return await response.json();
  } finally { clearTimeout(timeout); }
}

function readStdin() {
  return new Promise((resolve, reject) => {
    let data = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', chunk => { data += chunk; });
    process.stdin.on('end', () => resolve(data));
    process.stdin.on('error', reject);
  });
}

function emit(value) { process.stdout.write(`${JSON.stringify(value)}\n`); }
function parseArgs(values) { const result = {}; for (let i = 0; i < values.length; i++) if (values[i].startsWith('--')) result[values[i].slice(2)] = values[i + 1]?.startsWith('--') ? true : values[++i] ?? true; return result; }

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) main();
