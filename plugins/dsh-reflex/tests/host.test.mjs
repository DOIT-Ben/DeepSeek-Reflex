import { test } from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { createHandler, acceptsRequest, apply } from '../index.js';
import { findExecutable, launchExecutable } from '../launcher.js';

function request(route = '/reflex-window/open') {
  return { method: 'POST', url: route, socket: { remoteAddress: '127.0.0.1' }, headers: { host: '127.0.0.1:3080', origin: 'http://127.0.0.1:3080', 'x-reflex-request': '1', 'sec-fetch-site': 'same-origin' } };
}
async function run(handler, req) {
  let status, body;
  await handler(req, { writeHead: value => { status = value; }, end: value => { body = JSON.parse(value); } });
  return { status, body };
}

test('native launch is a fixed executable with no shell, draft or command-line arguments', async () => {
  let call;
  await launchExecutable('C:\\Test\\DeepSeekFloat.exe', (...args) => {
    call = args; const child = new EventEmitter(); child.unref = () => {}; queueMicrotask(() => child.emit('spawn')); return child;
  });
  assert.deepEqual(call, ['C:\\Test\\DeepSeekFloat.exe', [], { detached: true, windowsHide: true, shell: false, stdio: 'ignore' }]);
});

test('launch error is propagated; unsupported OS and missing file have explicit status', async () => {
  await assert.rejects(launchExecutable('missing', () => { const child = new EventEmitter(); queueMicrotask(() => child.emit('error', new Error('missing'))); return child; }));
  assert.deepEqual(await findExecutable({ platform: 'linux' }), { available: false, reason: 'windows-only' });
  assert.equal((await findExecutable({ platform: 'win32', env: { REFLEX_EXECUTABLE: 'relative.exe' } })).available, false);
  assert.equal((await findExecutable({ platform: 'win32', env: { REFLEX_EXECUTABLE: 'C:\\Test\\DeepSeekFloat.exe' }, inspect: async () => ({ isFile: () => false }) })).available, false);
  const located = await findExecutable({ platform: 'win32', env: { LOCALAPPDATA: 'C:\\Users\\test\\AppData\\Local' }, inspect: async () => ({ isFile: () => true }) });
  assert.ok(located.executable.endsWith('Programs\\DeepSeekFloat\\DeepSeekFloat.exe'));
});

test('reject cross-origin, remote, missing origin, missing custom header and forged local origin', () => {
  assert.equal(acceptsRequest(request()), true);
  for (const alter of [r => { r.socket.remoteAddress = '192.168.1.2'; }, r => { delete r.headers.origin; }, r => { delete r.headers['x-reflex-request']; }, r => { r.headers.origin = 'http://evil.test'; }, r => { r.headers['sec-fetch-site'] = 'cross-site'; }, r => { r.headers.host = 'evil.test'; r.headers.origin = 'http://evil.test'; }]) {
    const req = request(); alter(req); assert.equal(acceptsRequest(req), false);
  }
});

test('probe reveals no path, open calls no model or sessions, and rapid launches are blocked', async () => {
  const opened = [];
  const handler = createHandler({ locate: async () => ({ available: true, executable: 'secret-path' }), launch: async exe => opened.push(exe), now: () => 1000 });
  assert.deepEqual(await run(handler, request('/reflex-window/status')), { status: 200, body: { available: true } });
  assert.deepEqual(await run(handler, request()), { status: 200, body: { opened: true } });
  assert.equal((await run(handler, request())).status, 429);
  assert.deepEqual(opened, ['secret-path']);
});

test('reject payloads and GET before launch; missing application and launch failures preserve diagnostics', async () => {
  const handler = createHandler({ locate: async () => ({ available: false, reason: 'not-installed' }) });
  const body = request(); body.headers['content-length'] = '12'; assert.equal((await run(handler, body)).status, 400);
  const get = request(); get.method = 'GET'; assert.equal((await run(handler, get)).status, 405);
  assert.equal((await run(handler, request())).status, 409);
  const fail = createHandler({ locate: async () => ({ available: true }), launch: async () => { throw new Error('private path'); } });
  assert.deepEqual(await run(fail, request()), { status: 503, body: { error: 'launch-failed' } });
});

test('Cordis effect removes its route when disposed', () => {
  let disposed = false, effect;
  apply({ effect: fn => { effect = fn(); }, webServer: { register: route => { assert.equal(route.path, '/reflex-window'); return () => { disposed = true; }; } } });
  effect(); assert.equal(disposed, true);
});
