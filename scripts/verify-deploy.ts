/**
 * Phase 2 Sprint 1 验收：ARC-06 Docker + ARC-07 Mobile JWT
 * 运行: npm run verify:deploy
 */
import { spawnSync } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { signPlayerToken } from '../server/src/auth.js';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const REFRESH_LEAD_SEC = 5 * 60;

function decodeJwtExpSec(token: string): number | null {
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  try {
    const padded = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad = padded.length % 4;
    const base64 = pad ? padded + '='.repeat(4 - pad) : padded;
    const payload = JSON.parse(Buffer.from(base64, 'base64').toString('utf8')) as { exp?: number };
    return typeof payload.exp === 'number' ? payload.exp : null;
  } catch {
    return null;
  }
}

function shouldRefreshToken(expSec: number, nowSec: number): boolean {
  return expSec - nowSec <= REFRESH_LEAD_SEC;
}

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(relPath: string): string {
  return fs.readFileSync(path.join(ROOT, relPath), 'utf8');
}

function testDockerArtifacts(): void {
  console.log('\n=== TC: ARC-06 Docker artifacts ===');
  assert(fs.existsSync(path.join(ROOT, 'Dockerfile')), 'Dockerfile exists');
  assert(fs.existsSync(path.join(ROOT, 'docker-compose.yml')), 'docker-compose.yml exists');
  assert(fs.existsSync(path.join(ROOT, '.env.example')), '.env.example exists');

  const dockerfile = read('Dockerfile');
  assert(dockerfile.includes('node:20-alpine'), 'Dockerfile uses node:20-alpine');
  assert(dockerfile.includes('AS builder'), 'Dockerfile multi-stage build');
  assert(dockerfile.includes('/health'), 'Dockerfile healthcheck on /health');

  const compose = read('docker-compose.yml');
  assert(compose.includes('fish_social_data'), 'compose defines sqlite volume');
  assert(compose.includes('JWT_SECRET'), 'compose requires JWT_SECRET');
  assert(compose.includes('3001'), 'compose exposes port 3001');

  const envExample = read('.env.example');
  assert(envExample.includes('JWT_SECRET'), '.env.example documents JWT_SECRET');
  assert(envExample.includes('ADMIN_SECRET'), '.env.example documents ADMIN_SECRET');
  assert(envExample.includes('ALLOWED_ORIGINS'), '.env.example documents ALLOWED_ORIGINS');
  assert(envExample.includes('LOG_DIR'), '.env.example documents LOG_DIR');

  const pkg = JSON.parse(read('package.json')) as { scripts?: Record<string, string> };
  assert(typeof pkg.scripts?.['docker:build'] === 'string', 'npm script docker:build');
  assert(typeof pkg.scripts?.['docker:up'] === 'string', 'npm script docker:up');
  assert(typeof pkg.scripts?.['docker:down'] === 'string', 'npm script docker:down');
}

function testMobileJwtWiring(): void {
  console.log('\n=== TC: ARC-07 Mobile JWT wiring ===');
  const mobilePkg = JSON.parse(read('mobile/package.json')) as {
    dependencies?: Record<string, string>;
  };
  assert(
    Boolean(mobilePkg.dependencies?.['expo-secure-store']),
    'mobile depends on expo-secure-store',
  );

  const jwtToken = read('mobile/lib/jwtToken.ts');
  assert(jwtToken.includes('expo-secure-store'), 'jwtToken uses SecureStore');
  assert(jwtToken.includes('REFRESH_LEAD_SEC'), 'jwtToken defines refresh lead');

  const apiClient = read('mobile/lib/apiClient.ts');
  assert(apiClient.includes('/api/auth/dev-token'), 'apiClient fetches dev-token');
  assert(apiClient.includes('Authorization'), 'apiClient attaches Authorization header');
  assert(apiClient.includes('getValidAccessToken'), 'apiClient exposes token refresh');

  const login = read('mobile/app/login.tsx');
  assert(login.includes('fetchDevToken'), 'login fetches JWT after register');

  const socket = read('mobile/lib/usePondSocket.ts');
  assert(socket.includes('auth:'), 'socket.io carries auth.token');
  assert(socket.includes('getValidAccessToken'), 'pond socket resolves JWT before connect');

  const socialApi = read('mobile/lib/socialApi.ts');
  assert(socialApi.includes('apiFetch'), 'socialApi uses apiClient');

  const shopApi = read('mobile/lib/shopApi.ts');
  assert(shopApi.includes('apiFetch'), 'shopApi uses apiClient');
}

function testJwtRefreshLogic(): void {
  console.log('\n=== TC: JWT refresh threshold ===');
  process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'verify-deploy-test-secret';
  const token = signPlayerToken('verify-deploy-player');
  const exp = decodeJwtExpSec(token);
  assert(typeof exp === 'number' && exp > 0, 'server token has exp claim');

  const farFuture = exp - REFRESH_LEAD_SEC - 10;
  assert(!shouldRefreshToken(exp, farFuture), 'token not refreshed when far from expiry');

  const nearExpiry = exp - REFRESH_LEAD_SEC + 1;
  assert(shouldRefreshToken(exp, nearExpiry), 'token refreshed within 5-minute lead');
}

function testDockerCliOptional(): void {
  console.log('\n=== TC: Docker CLI (optional) ===');
  const which = spawnSync('docker', ['--version'], { encoding: 'utf8', shell: true });
  if (which.status !== 0) {
    console.log('  SKIP: docker CLI not available');
    return;
  }
  assert(Boolean(which.stdout?.trim()), `docker CLI available (${which.stdout.trim()})`);

  if (process.env.VERIFY_DEPLOY_DOCKER !== '1') {
    console.log('  SKIP: set VERIFY_DEPLOY_DOCKER=1 to run docker build');
    return;
  }

  const build = spawnSync('docker', ['build', '-t', 'fish-social:verify-deploy', '.'], {
    cwd: ROOT,
    encoding: 'utf8',
    shell: true,
    stdio: 'pipe',
  });
  if (build.status !== 0) {
    console.error(build.stderr || build.stdout);
    throw new Error('docker build failed');
  }
  console.log('  OK: docker build succeeded');
}

function main(): void {
  console.log('verify-deploy (Phase 2 Sprint 1)');
  testDockerArtifacts();
  testMobileJwtWiring();
  testJwtRefreshLogic();
  testDockerCliOptional();
  console.log('\nAll deploy checks passed.');
}

main();
