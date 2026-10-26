import { stat } from 'node:fs/promises';
import path from 'node:path';
import { spawn } from 'node:child_process';

/** Locate only the configured or standard installed Windows application. */
export async function findExecutable({ platform = process.platform, env = process.env, inspect = stat } = {}) {
  if (platform !== 'win32') return { available: false, reason: 'windows-only' };
  const executable = env.REFLEX_EXECUTABLE || (env.LOCALAPPDATA && path.win32.join(env.LOCALAPPDATA, 'Programs', 'DeepSeek-Reflex', 'DeepSeek-Reflex.exe'));
  const base = executable ? path.win32.basename(executable).toLowerCase() : '';
  // Current installs are DeepSeek-Reflex.exe. Keep the pre-rename name so an override still wakes an older folder.
  if (!executable || !path.win32.isAbsolute(executable) || (base !== 'deepseek-reflex.exe' && base !== 'deepseekfloat.exe')) {
    return { available: false, reason: 'not-installed' };
  }
  try {
    if (!(await inspect(executable)).isFile()) return { available: false, reason: 'not-installed' };
  } catch { return { available: false, reason: 'not-installed' }; }
  return { available: true, executable };
}

/** Wake the same Reflex instance without shell interpolation, text arguments, or API calls. */
export function launchExecutable(executable, start = spawn) {
  return new Promise((resolve, reject) => {
    const child = start(executable, [], { detached: true, windowsHide: true, shell: false, stdio: 'ignore' });
    child.once('error', reject);
    child.once('spawn', () => { child.unref(); resolve(); });
  });
}
