import { findExecutable, launchExecutable } from './launcher.js';
import { readStage, stageExecutable, replyExecutable, newReplyRequest } from './handoff.js';

export const name = 'reflex-window';
export const inject = ['webServer'];

function reply(res, status, body) {
  res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store', 'x-content-type-options': 'nosniff' });
  res.end(JSON.stringify(body));
}

/** Only same-origin loopback browser requests may probe or wake a local executable. */
export function acceptsRequest(req) {
  const address = (req.socket?.remoteAddress || '').toLowerCase();
  if (!['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(address)) return false;
  const site = req.headers['sec-fetch-site'];
  if (site && site !== 'same-origin') return false;
  if (req.headers['x-reflex-request'] !== '1' || !req.headers.origin) return false;
  try {
    const origin = new URL(req.headers.origin);
    return origin.protocol === 'http:' && ['localhost', '127.0.0.1', '[::1]'].includes(origin.hostname) && origin.host === req.headers.host;
  } catch { return false; }
}

/** Local launch plus bounded draft handoff and passive reply read. No conversation history or session ids are accepted. */
export function createHandler({ locate = findExecutable, launch = launchExecutable, stage = stageExecutable, readReply = replyExecutable, newReply = newReplyRequest, now = Date.now } = {}) {
  let opening = false, lastOpen = -Infinity;
  return async (req, res) => {
    if (!acceptsRequest(req)) return reply(res, 403, { error: 'local-origin-required' });
    const route = req.url?.split('?')[0];
    if (!['/reflex-window/status', '/reflex-window/open', '/reflex-window/stage', '/reflex-window/reply'].includes(route)) return reply(res, 404, { error: 'not-found' });
    if (req.method !== 'POST') return reply(res, 405, { error: 'post-required' });
    // Legacy status/open and reply stay body-free; only stage has the narrow validated text schema.
    if (route !== '/reflex-window/stage' && (req.headers['transfer-encoding'] || Number(req.headers['content-length'] || 0) !== 0)) return reply(res, 400, { error: 'body-not-accepted' });
    try {
      const request = route === '/reflex-window/stage' ? await readStage(req) : null;
      const app = await locate();
      if (route === '/reflex-window/status') return reply(res, 200, { available: app.available, reason: app.reason });
      if (!app.available) return reply(res, 409, { error: app.reason });
      if (opening || now() - lastOpen < 1000) return reply(res, 429, { error: 'opening' });
      opening = true;
      try {
        if (route === '/reflex-window/reply') { const result = await readReply(app.executable, newReply()); lastOpen = now(); return reply(res, 200, result); }
        if (request) { const result = await stage(app.executable, request); lastOpen = now(); return reply(res, 200, result); }
        await launch(app.executable); lastOpen = now();
      }
      finally { opening = false; }
      return reply(res, 200, { opened: true });
    } catch (error) { return reply(res, error.message === 'invalid-request' ? 400 : 503, { error: error.message === 'invalid-request' ? 'invalid-request' : 'launch-failed' }); }
  };
}

export function apply(ctx) {
  const handler = createHandler();
  ctx.effect(() => ctx.webServer.register({ kind: 'prefix', path: '/reflex-window', handler }), 'reflex-window: local launch');
}
