/**
 * v0.5 R0-1 验收：JWT 鉴权
 * 运行: npm run verify:auth
 */
process.env.NODE_ENV = 'development';
process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'verify-auth-test-secret';
delete process.env.AUTH_DISABLED;

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

async function testIdentityMismatchLogging(): Promise<void> {
  console.log('\n=== TC: identity_mismatch path ===');
  const chunks: string[] = [];
  const origWrite = process.stdout.write.bind(process.stdout);
  process.stdout.write = ((chunk: string | Uint8Array, ...args: unknown[]) => {
    chunks.push(String(chunk));
    return origWrite(chunk as never, ...(args as never));
  }) as typeof process.stdout.write;
  try {
    await import('../server/src/debugSampler.js');
    const { resolveAuthedPlayerId } = await import('../server/src/auth.js');
    const { flushPendingLogEvents } = await import('../server/src/logger.js');
    const req = {
      authPlayerId: 'player-a',
      path: '/api/test',
      method: 'POST',
      body: { playerId: 'player-b' },
    } as import('express').Request;
    const resolved = resolveAuthedPlayerId(req, 'player-b');
    assert(resolved === 'player-a', 'auth playerId wins over body');
    await flushPendingLogEvents();
    assert(chunks.some((l) => l.includes('identity_mismatch')), 'identity_mismatch logged');
  } finally {
    process.stdout.write = origWrite;
  }
}

async function testTokenRoundTrip(): Promise<void> {
  console.log('\n=== TC: token sign/verify ===');
  const { signPlayerToken, verifyPlayerToken } = await import('../server/src/auth.js');
  const token = signPlayerToken('player-verify-auth');
  const payload = verifyPlayerToken(token);
  assert(payload?.playerId === 'player-verify-auth', 'valid token verifies');
  assert(verifyPlayerToken('bad.token.here') === null, 'invalid token rejected');
  assert(verifyPlayerToken(undefined) === null, 'missing token rejected');
}

async function testRequireAuthMiddleware(): Promise<void> {
  console.log('\n=== TC: requireAuth middleware ===');
  const { requireAuth, signPlayerToken } = await import('../server/src/auth.js');
  let status = 0;
  let body: unknown;
  const res = {
    status(code: number) {
      status = code;
      return this;
    },
    json(payload: unknown) {
      body = payload;
    },
  } as import('express').Response;
  const token = signPlayerToken('player-http-auth');
  const req = {
    header(name: string) {
      return name === 'Authorization' ? `Bearer ${token}` : undefined;
    },
    body: {},
    path: '/api/shop/baits/buy',
    method: 'POST',
  } as import('express').Request;
  let nextCalled = false;
  requireAuth(req, res, () => {
    nextCalled = true;
  });
  assert(nextCalled && req.authPlayerId === 'player-http-auth', 'valid bearer passes requireAuth');

  status = 0;
  nextCalled = false;
  const badReq = {
    header() {
      return undefined;
    },
    body: {},
    path: '/api/posts',
    method: 'POST',
  } as import('express').Request;
  requireAuth(badReq, res, () => {
    nextCalled = true;
  });
  assert(!nextCalled && status === 401, 'missing token rejected by requireAuth');
  void body;
}

async function testSocketAuthWiring(): Promise<void> {
  console.log('\n=== TC: socket auth wiring in index.ts ===');
  const { isAuthDisabled } = await import('../server/src/auth.js');
  const indexSrc = fs.readFileSync(path.join(rootDir, 'server/src/index.ts'), 'utf8');
  const lifecycleSrc = fs.readFileSync(path.join(rootDir, 'server/src/socketLifecycle.ts'), 'utf8');
  assert(indexSrc.includes('io.use((socket, next)'), 'socket auth middleware present');
  assert(indexSrc.includes("verifyPlayerToken(token)"), 'socket verifies JWT');
  assert(lifecycleSrc.includes("resolveSocketPlayerId"), 'join uses auth playerId');
  assert(!isAuthDisabled(), 'verify runs with auth enabled');
}

async function testBackendOptAScriptPresent(): Promise<void> {
  console.log('\n=== TC: BE-OPT-A verify script ===');
  assert(
    fs.existsSync(path.join(rootDir, 'scripts/verify-backend-opt-a.ts')),
    'verify-backend-opt-a.ts exists',
  );
  const pkg = JSON.parse(fs.readFileSync(path.join(rootDir, 'package.json'), 'utf8')) as {
    scripts?: Record<string, string>;
  };
  assert(typeof pkg.scripts?.['verify:backend-opt-a'] === 'string', 'npm script verify:backend-opt-a');
}

async function main(): Promise<void> {
  console.log('verify-auth');
  await testIdentityMismatchLogging();
  await testTokenRoundTrip();
  await testRequireAuthMiddleware();
  await testSocketAuthWiring();
  await testBackendOptAScriptPresent();
  console.log('\nAll auth checks passed.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
