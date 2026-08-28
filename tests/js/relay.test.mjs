import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { normalizedEvent, providerOutput, redact } from '../../integrations/hooks/relay.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const relay = path.join(root, 'integrations', 'hooks', 'relay.mjs');

test('provider output uses provider-specific gate contracts', () => {
  const claude = providerOutput('claude', 'PreToolUse', 'deny', 'blocked');
  assert.equal(claude.hookSpecificOutput.permissionDecision, 'deny');
  const copilot = providerOutput('copilot', 'preToolUse', 'allow', 'approved');
  assert.equal(copilot.permissionDecision, 'allow');
  const permission = providerOutput('copilot', 'permissionRequest', 'deny', 'blocked');
  assert.equal(permission.behavior, 'deny');
  assert.equal(permission.interrupt, true);
});

test('Copilot camelCase toolArgs are normalized for policy evaluation', () => {
  const event = normalizedEvent('copilot', { eventName: 'preToolUse', toolName: 'bash', toolArgs: { command: 'npm publish' } });
  assert.deepEqual(event.toolInput, { command: 'npm publish' });
});

test('redaction masks credential-like fields and values', () => {
  const result = redact({ authorization: 'Bearer abcdefghijklmnopqrstuvwxyz', command: 'echo sk-ant-abcdefghijklmnopqrstuvwxyz' });
  assert.equal(result.authorization, '[redacted]');
  assert.equal(result.command, 'echo [redacted-token]');
});

test('relay waits for an ask gate and emits final allow', async () => {
  let polls = 0;
  const server = http.createServer((request, response) => {
    response.setHeader('content-type', 'application/json');
    if (request.method === 'POST') response.end(JSON.stringify({ decision: 'Ask', reason: 'approval', gateId: 'gate-1', pollAfterMs: 10 }));
    else { polls++; response.end(JSON.stringify(polls < 2 ? { pending: true, decision: 'Ask', gate: { reason: 'waiting' } } : { pending: false, decision: 'Allow', gate: { reason: 'approved on display' } })); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address();
  try {
    const result = await runRelay('codex', `http://127.0.0.1:${port}`, { hook_event_name: 'PreToolUse', session_id: 's1', tool_name: 'shell', tool_input: { command: 'npm publish' } });
    assert.equal(result.hookSpecificOutput.permissionDecision, 'allow');
    assert.match(result.hookSpecificOutput.permissionDecisionReason, /approved/);
  } finally { server.close(); }
});


test('relay includes a pairing key for non-loopback host boundaries', async () => {
  let receivedKey = null;
  const server = http.createServer((request, response) => {
    receivedKey = request.headers['x-agentdisplay-key'];
    response.setHeader('content-type', 'application/json');
    response.end(JSON.stringify({ decision: 'Allow', reason: 'ok' }));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address();
  try {
    const output = await runRelay('claude', `http://127.0.0.1:${port}`, { hook_event_name: 'PreToolUse', tool_name: 'Bash', tool_input: { command: 'echo ok' } }, { AGENTDISPLAY_KEY: 'pair-test-key' });
    assert.equal(output.hookSpecificOutput.permissionDecision, 'allow');
    assert.equal(receivedKey, 'pair-test-key');
  } finally { server.close(); }
});

test('relay fails open when host is unavailable unless strict mode is enabled', async () => {
  const normal = await runRelay('copilot', 'http://127.0.0.1:1', { hookEventName: 'preToolUse', toolName: 'shell', toolInput: { command: 'echo ok' } });
  assert.equal(normal.permissionDecision, 'allow');
  const strict = await runRelay('copilot', 'http://127.0.0.1:1', { hookEventName: 'preToolUse', toolName: 'shell', toolInput: { command: 'echo ok' } }, { AGENTDISPLAY_STRICT: 'true' });
  assert.equal(strict.permissionDecision, 'deny');
});

function runRelay(provider, host, input, extraEnv = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [relay, '--provider', provider, '--host', host], { env: { ...process.env, ...extraEnv }, stdio: ['pipe', 'pipe', 'pipe'] });
    let stdout = '', stderr = '';
    child.stdout.on('data', chunk => stdout += chunk);
    child.stderr.on('data', chunk => stderr += chunk);
    child.on('error', reject);
    child.on('close', code => code === 0 ? resolve(JSON.parse(stdout)) : reject(new Error(stderr || `exit ${code}`)));
    child.stdin.end(JSON.stringify(input));
  });
}
