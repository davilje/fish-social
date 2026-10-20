import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const source = path.join(root, 'shared', 'defaultAvatars.ts');
const dist = path.join(root, 'shared', 'dist', 'defaultAvatars.js');

if (!fs.existsSync(source)) {
  console.error(`[shared] missing ${source}`);
  process.exit(1);
}

const sourceMtime = fs.statSync(source).mtimeMs;
const distMtime = fs.existsSync(dist) ? fs.statSync(dist).mtimeMs : 0;
if (distMtime >= sourceMtime) {
  process.exit(0);
}

console.log('[shared] defaultAvatars.ts is newer than dist; building @fish-social/shared');
const result = spawnSync('npm', ['run', 'build', '--workspace=shared'], {
  cwd: root,
  stdio: 'inherit',
  shell: true,
});
if (result.status !== 0) {
  process.exit(result.status ?? 1);
}

if (!fs.existsSync(dist) || fs.statSync(dist).mtimeMs < sourceMtime) {
  console.error('[shared] dist/defaultAvatars.js is still older than defaultAvatars.ts');
  process.exit(1);
}
