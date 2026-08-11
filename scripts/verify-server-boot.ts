/**
 * BUG-11 验收：服务端能在 15s 内启动并响应 /health
 * 运行: npm run verify:server-boot
 */
import { spawn, spawnSync, type ChildProcess } from 'child_process';
import path from 'path';
import { fileURLToPath } from 'url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const SERVER_DIR = path.join(ROOT, 'server');
const HEALTH_URL = 'http://127.0.0.1:3001/health';
const TIMEOUT_MS = 15_000;
const POLL_MS = 400;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function pollHealth(): Promise<boolean> {
  try {
    const res = await fetch(HEALTH_URL);
    if (!res.ok) return false;
    const data = (await res.json()) as { ok?: boolean };
    return data.ok === true;
  } catch {
    return false;
  }
}

function killProcessTree(child: ChildProcess): void {
  if (!child.pid) return;
  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
    return;
  }
  child.kill('SIGTERM');
}

async function main(): Promise<void> {
  console.log('verify-server-boot');

  const child = spawn('npx', ['tsx', 'src/index.ts'], {
    cwd: SERVER_DIR,
    shell: true,
    stdio: ['ignore', 'pipe', 'pipe'],
    env: {
      ...process.env,
      NODE_ENV: process.env.NODE_ENV ?? 'development',
      PORT: process.env.PORT ?? '3001',
    },
  });

  child.stdout?.on('data', (chunk: Buffer) => {
    const text = chunk.toString();
    if (text.includes('Fish Social server running')) {
      process.stdout.write(text);
    }
  });
  child.stderr?.on('data', (chunk: Buffer) => {
    process.stderr.write(chunk);
  });

  const deadline = Date.now() + TIMEOUT_MS;
  let ok = false;
  while (Date.now() < deadline) {
    if (await pollHealth()) {
      ok = true;
      break;
    }
    if (child.exitCode !== null) {
      break;
    }
    await sleep(POLL_MS);
  }

  killProcessTree(child);

  if (!ok) {
    throw new Error('server did not respond to /health within 15s');
  }

  console.log('  OK: server boot and /health within 15s');
  console.log('\nAll server boot checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
