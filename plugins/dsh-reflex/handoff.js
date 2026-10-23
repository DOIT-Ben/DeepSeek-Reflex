import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';

const modes = new Set(['normal', 'translate', 'explain', 'question', 'polish', 'summarize']);
const statuses = new Set(['filled', 'draft', 'ambiguous', 'no-editor', 'wrong-host', 'loading', 'busy', 'failed', 'invalid', 'outcome-unknown']);
const replyStatuses = new Set(['ok', 'wrong-host', 'unsupported', 'empty', 'overflow', 'loading', 'busy', 'failed', 'outcome-unknown']);
const uuid = value => typeof value === 'string' && /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
const keys = value => Object.keys(value).sort().join(',');

export function validStage(value) {
  return !!value && typeof value === 'object' && keys(value) === 'id,mode,text' &&
    uuid(value.id) && modes.has(value.mode) &&
    typeof value.text === 'string' && !!value.text.trim() && value.text.length <= 20000;
}

export function validReplyRequest(value) {
  return !!value && typeof value === 'object' && keys(value) === 'id,op' && uuid(value.id) && value.op === 'reply';
}

function validReplyResult(value, id) {
  if (!value || typeof value !== 'object' || value.id !== id) return false;
  if (keys(value) === 'id,status') return replyStatuses.has(value.status);
  return keys(value) === 'id,status,text' && value.status === 'ok' && typeof value.text === 'string' && value.text.length <= 20000;
}

export function readStage(req, timeout = 3000) {
  return new Promise((resolve, reject) => {
    if (!/^application\/json(?:\s*;\s*charset=utf-8)?$/i.test(req.headers['content-type'] || '')) { req.resume?.(); reject(new Error('invalid-request')); return; }
    let size = 0; const chunks = [];
    const cleanup = () => { clearTimeout(timer); req.removeListener('data', data); req.removeListener('end', end); req.removeListener('error', fail); req.removeListener('aborted', fail); };
    const fail = () => { cleanup(); req.resume?.(); reject(new Error('invalid-request')); };
    const data = chunk => { size += chunk.length; if (size > 90000) { fail(); return; } chunks.push(chunk); };
    const end = () => {
      cleanup();
      try { const value = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(Buffer.concat(chunks))); if (!validStage(value)) throw new Error(); resolve(value); }
      catch { reject(new Error('invalid-request')); }
    };
    const timer = setTimeout(fail, timeout);
    req.on('data', data); req.once('end', end); req.once('error', fail); req.once('aborted', fail);
  });
}

/** One helper per call: the fixed entry reads stdin; only the bounded reply reaches stdout. */
function runBridge(executable, request, { start = spawn, timeout = 12000, stdoutLimit, accept }) {
  if (!accept.request(request)) return Promise.resolve({ id: request?.id, status: 'invalid' });
  return new Promise(resolve => {
    let child, settled = false, size = 0; const chunks = [];
    const unknown = () => finish({ id: request.id, status: 'outcome-unknown' }, true);
    const finish = (result, stop = false) => { if (settled) return; settled = true; clearTimeout(timer); if (stop && child && !child.killed) child.kill(); resolve(result); };
    const timer = setTimeout(unknown, timeout);
    try {
      child = start(executable, ['--reflex-bridge'], { windowsHide: true, shell: false, stdio: ['pipe', 'pipe', 'ignore'] });
      child.once('error', unknown); child.stdin.once('error', unknown);
      child.stdout.on('data', chunk => { size += chunk.length; if (size > stdoutLimit) unknown(); else chunks.push(chunk); });
      child.once('close', () => {
        try { const reply = JSON.parse(Buffer.concat(chunks).toString('utf8')); if (!accept.reply(reply, request.id)) throw new Error(); finish(reply); }
        catch { unknown(); }
      });
      child.stdin.end(JSON.stringify(request));
    } catch { unknown(); }
  });
}

/** Only this child is terminated on timeout. Neither text nor mode enters argv. */
export function stageExecutable(executable, request, options = {}) {
  return runBridge(executable, request, {
    ...options, stdoutLimit: 2048,
    accept: { request: validStage, reply: (reply, id) => keys(reply) === 'id,status' && reply.id === id && statuses.has(reply.status) },
  });
}

/** Reply reads return up to 20,000 characters of website text, never commands or paths. */
export function replyExecutable(executable, request, options = {}) {
  return runBridge(executable, request, {
    ...options, stdoutLimit: 90112,
    accept: { request: validReplyRequest, reply: validReplyResult },
  });
}

export function newReplyRequest() {
  return { id: randomUUID(), op: 'reply' };
}
