import { build } from 'esbuild';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

// Resolve existing built upstream artifacts in isolation; never repair the upstream checkout.
const dsh = process.argv[2];
if (!dsh || !path.isAbsolute(dsh)) throw new Error('Usage: node scripts/runtime-smoke.mjs <absolute DSH checkout> [--open-window]');
const temp = await mkdtemp(path.join(tmpdir(), 'reflex-dsh-runtime-'));
try {
  const hostPath = fileURLToPath(new URL('../index.js', import.meta.url));
  const code = `import assert from 'node:assert/strict';
import {Context} from '@deepseek-ai/cordis';
import {WebServer} from '@deepseek-ai/dsh-host-webserver';
import * as plugin from ${JSON.stringify(hostPath)};
const ctx=new Context();
try {
 await ctx.plugin(WebServer,{host:'127.0.0.1',port:0});
 const mounted=await ctx.plugin(plugin);
 const origin='http://127.0.0.1:'+ctx.webServer.port;
 const request=path=>fetch(origin+path,{method:'POST',headers:{origin,'x-reflex-request':'1','sec-fetch-site':'same-origin'}});
 const probe=await request('/reflex-window/status');assert.equal(probe.status,200);console.log('PASS real Cordis + DSH WebServer plugin activation, status',await probe.json());
 const rejected=await fetch(origin+'/reflex-window/open',{method:'POST',headers:{origin:'http://evil.test','x-reflex-request':'1'}});assert.equal(rejected.status,403);console.log('PASS real HTTP cross-origin rejection');
 ${process.argv.includes('--open-window') ? "const opened=await request('/reflex-window/open');assert.equal(opened.status,200);console.log('PASS native launch request',await opened.json());" : ''}
 await mounted.dispose();
 assert.equal((await request('/reflex-window/status')).status,404);console.log('PASS plugin disposal removes HTTP route');
}finally{await ctx.fiber.dispose();}`;
  const output = path.join(temp, 'smoke.mjs');
  await build({ stdin: { contents: code, resolveDir: dsh, sourcefile: 'smoke.mjs' }, outfile: output, platform: 'node', format: 'esm', bundle: true, alias: {
    '@deepseek-ai/cordis': path.join(dsh, 'vendor/cordis/lib/index.js'),
    '@deepseek-ai/cosmokit': path.join(dsh, 'vendor/cosmokit/lib/index.js'),
    '@deepseek-ai/schemastery': path.join(dsh, 'vendor/schemastery/lib/index.mjs'),
    '@deepseek-ai/dsh-host-webserver': path.join(dsh, 'packages/host/webserver/lib/index.js'),
  } });
  const result = spawnSync(process.execPath, [output], { timeout: 15000, encoding: 'utf8', windowsHide: true });
  process.stdout.write(result.stdout || ''); process.stderr.write(result.stderr || '');
  if (result.error || result.status !== 0) throw new Error('Runtime smoke failed: ' + (result.error?.code || result.status));
} finally { await rm(temp, { recursive: true, force: true }); }
