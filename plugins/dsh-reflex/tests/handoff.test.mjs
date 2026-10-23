import { test } from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { Readable, PassThrough } from 'node:stream';
import { readStage, stageExecutable, validStage } from '../handoff.js';
import { createHandler } from '../index.js';

const request = () => ({ id: '31030090-7730-49e0-916e-b9d109ad5709', mode: 'polish', text: '仅本机的测试文字' });
function http(value) {
  const req = Readable.from([Buffer.from(typeof value === 'string' ? value : JSON.stringify(value))]);
  req.url = '/reflex-window/stage'; req.method = 'POST'; req.socket = { remoteAddress: '127.0.0.1' };
  req.headers = { origin: 'http://127.0.0.1:3081', host: '127.0.0.1:3081', 'x-reflex-request': '1', 'content-type': 'application/json' }; return req;
}
function childFactory(response, onStart = () => {}) {
  return (...args) => {
    const child = new EventEmitter(); child.stdin = new PassThrough(); child.stdout = new PassThrough(); child.kill = () => { child.killed = true; };
    let payload = ''; child.stdin.on('data', data => payload += data);
    child.stdin.on('finish', () => { onStart(args, payload, child); if (response !== null) { child.stdout.emit('data', Buffer.from(typeof response === 'string' ? response : JSON.stringify(response))); child.emit('close', 0); } }); return child;
  };
}
test('stage validates fixed schema, mode, UTF-16 length, UUID and finite UTF-8 body', async () => {
  assert.deepEqual(await readStage(http(request())), request());
  for (const value of [{ ...request(), mode: 'shell' }, { ...request(), text: ' ' }, { ...request(), text: '中'.repeat(20001) }, { ...request(), id: 'not-id' }, { ...request(), script: 'bad' }]) {
    assert.equal(validStage(value), false); await assert.rejects(readStage(http(value)));
  }
  await assert.rejects(readStage(http('x'.repeat(90001))));
  const wrongType = http(request()); wrongType.headers['content-type'] = 'text/plain'; await assert.rejects(readStage(wrongType));
  const slow = new PassThrough(); slow.headers = { 'content-type': 'application/json' }; await assert.rejects(readStage(slow, 10)); slow.destroy();
});
test('stage helper has fixed argv and stdin, never passes text through shell or args', async () => {
  const reply = { id: request().id, status: 'filled' };
  const result = await stageExecutable('C:\\Fixture\\DeepSeekFloat.exe', request(), { start: childFactory(reply, (args, payload) => {
    assert.deepEqual(args, ['C:\\Fixture\\DeepSeekFloat.exe', ['--reflex-bridge'], { windowsHide: true, shell: false, stdio: ['pipe', 'pipe', 'ignore'] }]); assert.deepEqual(JSON.parse(payload), request());
  }) }); assert.deepEqual(result, reply);
});
test('bad stdout, mismatched request, overlong output and timed out helper report unknown and stop only own child', async () => {
  for (const response of ['broken', { id: 'other', status: 'filled' }, { id: request().id, status: 'not-real' }, 'x'.repeat(2049), null]) {
    let own; const result = await stageExecutable('fixture', request(), { timeout: 15, start: childFactory(response, (_args, _body, child) => { own = child; }) });
    assert.equal(result.status, 'outcome-unknown'); assert.equal(own.killed, true);
  }
});
test('HTTP stage forwards only validated body and shares launch single-flight protection', async () => {
  let unblock; const holding = new Promise(resolve => { unblock = resolve; }); let calls = 0;
  const handler = createHandler({ locate: async () => ({ available: true, executable: 'fixture' }), stage: async (_exe, value) => { calls++; await holding; return { id: value.id, status: 'filled' }; } });
  const invoke = req => new Promise(resolve => { void handler(req, { writeHead: status => { req.result = status; }, end: body => resolve({ status: req.result, body: JSON.parse(body) }) }); });
  const first = invoke(http(request())); await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal((await invoke(http(request()))).status, 429); unblock(); assert.equal((await first).body.status, 'filled'); assert.equal(calls, 1);
  const foreign = http(request()); foreign.headers.origin = 'https://foreign.test'; assert.equal((await invoke(foreign)).status, 403);
  assert.equal((await invoke(http({ ...request(), mode: 'unknown' }))).status, 400);
});
