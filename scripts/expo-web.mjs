#!/usr/bin/env node
import { spawn } from 'child_process';
import path from 'path';
import { fileURLToPath } from 'url';
import { loadEnv } from './load-env.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
loadEnv(ROOT);

const port = String(process.env.EXPO_WEB_PORT || 8082);
const mobileDir = path.join(ROOT, 'mobile');

const childEnv = { ...process.env, BROWSER: 'none' };
const url = `http://localhost:${port}`;
console.log(`\n  🌐 Fish Social Web 客户端已启动\n     ${url}\n`);

const child = spawn('npx', ['expo', 'start', '--web', '--port', port], {
  cwd: mobileDir,
  stdio: 'inherit',
  shell: true,
  env: childEnv,
});

child.on('exit', (code, signal) => {
  if (signal) process.kill(process.pid, signal);
  process.exit(code ?? 0);
});
