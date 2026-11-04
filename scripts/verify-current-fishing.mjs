import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
function run(args, cwd = root) {
  const result = spawnSync(process.execPath, args, {
    cwd, stdio: 'inherit', env: { ...process.env, DB_PATH: ':memory:', NODE_ENV: 'test' },
  });
  if (result.status !== 0) process.exit(result.status ?? 1);
}
run(['scripts/verify-fishing-rules.mjs']);
run(['node_modules/typescript/bin/tsc', '-p', 'shared/tsconfig.json']);
run(['../node_modules/vitest/vitest.mjs', 'run'], path.join(root, 'server'));
run(['--test', 'scripts/analytics/simulation-policy.test.mjs']);
