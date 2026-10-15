import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { build } from 'esbuild';
import { fileURLToPath } from 'node:url';
import { createHandler } from '../index.js';

const fixture = await build({ entryPoints: [fileURLToPath(new URL('fixture.jsx', import.meta.url))], bundle: true, write: false, format: 'iife', define: { 'process.env.NODE_ENV': '"development"' } });
const client = await readFile(new URL('../client.js', import.meta.url));
const handler = createHandler({ locate: async () => ({ available: true, executable: 'fixture-only' }), launch: async () => {} });
const html = `<!doctype html><html lang="zh"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Reflex 插件隔离预览</title><style>body{font:14px system-ui;margin:0;background:#f7f8fa;color:#252830}main{max-width:780px;margin:28px auto;padding:0 20px}.notice{font-size:13px;color:#666}#composer{margin-top:48vh;background:white;border:1px solid #e1e3e8;border-radius:16px;padding:16px}.draft{width:100%;height:90px;box-sizing:border-box;border:0;resize:vertical;font:inherit;outline:none}.toolbar{display:flex;align-items:center;gap:16px;padding-top:10px}.attachments{color:#777;font-size:12px}.send{margin-left:auto;border:0;border-radius:20px;background:#4563f5;color:white;padding:8px 16px}</style><main><h2>Reflex 官网小窗插件</h2><p class="notice">隔离预览：模拟 DSH 草稿与剪贴板，不读取个人会话、不调用模型、不启动真实小窗。完整安装说明见插件 README。</p><div id="root"></div></main><script src="/fixture.js"></script><script src="/client.js"></script></html>`;
const server = createServer((req, res) => {
  if (req.url?.startsWith('/reflex-window/')) { void handler(req, res); return; }
  if (req.url === '/fixture.js') { res.setHeader('content-type', 'text/javascript'); res.end(fixture.outputFiles[0].contents); return; }
  if (req.url === '/client.js') { res.setHeader('content-type', 'text/javascript'); res.end(client); return; }
  if (req.url?.split('?')[0] === '/') { res.setHeader('content-type', 'text/html; charset=utf-8'); res.end(html); return; }
  res.writeHead(404); res.end();
});
server.listen(0, '127.0.0.1', () => { console.log('Fixture only: http://127.0.0.1:' + server.address().port); });
process.once('SIGINT', () => server.close());
process.once('SIGTERM', () => server.close());
