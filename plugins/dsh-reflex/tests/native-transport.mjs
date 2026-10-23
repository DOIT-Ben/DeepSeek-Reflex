// Called only by the isolated native fixture, never by the installed application.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { createConnection } from 'node:net';
import { once } from 'node:events';
import { createHandler } from '../index.js';

const [executable, pipe] = process.argv.slice(2);
assert.ok(executable && pipe?.startsWith('DeepSeekReflex.v1.'));
// A direct Node client is not the authenticated same-EXE helper and must receive no reply.
await new Promise((resolve, reject) => {
  let bytes = 0; const socket = createConnection('\\\\.\\pipe\\' + pipe);
  const timer = setTimeout(() => { socket.destroy(); reject(new Error('unauthenticated client was not closed')); }, 3000);
  socket.on('data', data => bytes += data.length); socket.on('error', () => {});
  socket.on('close', () => { clearTimeout(timer); try { assert.equal(bytes, 0); resolve(); } catch (error) { reject(error); } });
});
const handler = createHandler({ locate: async () => ({ available: true, executable }) });
const server = createServer((req, res) => void handler(req, res));
server.listen(0, '127.0.0.1'); await once(server, 'listening');
try {
  const origin = 'http://127.0.0.1:' + server.address().port;
  const body = { id: crypto.randomUUID(), mode: 'polish', text: 'transport fixture material' };
  const response = await fetch(origin + '/reflex-window/stage', { method: 'POST', headers: { origin, 'x-reflex-request': '1', 'content-type': 'application/json' }, body: JSON.stringify(body), signal: AbortSignal.timeout(15000) });
  assert.equal(response.status, 200); assert.deepEqual(await response.json(), { id: body.id, status: 'filled' });
  console.log('PASS real HTTP stdin pipe transport');
} finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
