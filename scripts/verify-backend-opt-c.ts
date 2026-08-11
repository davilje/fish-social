/**
 * BE-OPT-C (STAB-01～06) 验收
 * 运行: npm run verify:backend-opt-c
 */
process.env.NODE_ENV = process.env.NODE_ENV ?? 'development';
process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'verify-be-opt-c-secret';
process.env.PLAYER_ERASE_PEPPER = process.env.PLAYER_ERASE_PEPPER ?? 'verify-pepper';
process.env.ADMIN_SECRET = process.env.ADMIN_SECRET ?? 'verify-admin-secret';
process.env.ALLOWED_ORIGINS = process.env.ALLOWED_ORIGINS ?? '*';
delete process.env.AUTH_DISABLED;

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import http from 'http';
import type { AddressInfo } from 'net';
import type { Request } from 'express';
import { Server } from 'socket.io';
import type { ClientToServerEvents, ServerToClientEvents } from '@fish-social/shared';
import {
  allowAdminQueryKey,
  extractAdminKey,
} from '../server/src/adminRbac.js';
import {
  allowSocketEvent,
  getSocketEventRatePerSec,
  resetSocketEventRateLimitForTests,
} from '../server/src/socketEventRateLimit.js';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(rel: string): string {
  return fs.readFileSync(path.join(ROOT, rel), 'utf8');
}

function mkReq(pathName: string, opts: { header?: string; queryKey?: string }): Request {
  return {
    path: pathName,
    header: (name: string) => (name === 'X-Admin-Key' ? opts.header : undefined),
    query: { key: opts.queryKey },
    ip: '127.0.0.1',
    method: 'GET',
  } as unknown as Request;
}

function testSourceGuards(): void {
  console.log('\n=== TC: source guards ===');
  const index = read('server/src/index.ts');
  assert(index.includes('flushPlayerPondSessionsOnShutdown'), 'STAB-01 session flush on shutdown');
  assert(index.includes('await shutdownOtelTracing()'), 'STAB-02 awaits OTEL shutdown');
  assert(index.includes('await closePostgresPool()'), 'STAB-02 awaits PG pool close');
  assert(index.includes('setFatalShutdownHandler'), 'STAB-03 wires fatal shutdown');

  const health = read('server/src/createApp.ts');
  assert(health.includes('draining: true'), 'STAB-06 health draining payload');
  assert(health.includes('if (shuttingDown)'), 'STAB-06 health checks shuttingDown');

  const rbac = read('server/src/adminRbac.ts');
  assert(rbac.includes('allowAdminQueryKey'), 'STAB-05 allowAdminQueryKey');
  assert(rbac.includes('admin_sse_query_key_used'), 'STAB-05 SSE query audit');
  assert(rbac.includes('isAdminLiveSessionPath'), 'STAB-05 live-session exception');

  const rate = read('server/src/socketEventRateLimit.ts');
  assert(rate.includes('SOCKET_EVENT_RATE_PER_SEC'), 'STAB-04 rate env');
  const handlers = read('server/src/socketPondHandlers.ts');
  assert(handlers.includes("error: 'rate_limited'"), 'STAB-04 ack rate_limited');

  const envEx = read('.env.example');
  assert(envEx.includes('SOCKET_EVENT_RATE_PER_SEC'), '.env.example SOCKET_EVENT_RATE_PER_SEC');
  assert(envEx.includes('ADMIN_ALLOW_QUERY_KEY'), '.env.example ADMIN_ALLOW_QUERY_KEY');

  const ops = read('docs/ops/shutdown-health.md');
  assert(ops.includes('/health'), 'ops docs shutdown health');
}

function testAdminQueryKeyPolicy(): void {
  console.log('\n=== TC: STAB-05 query key policy ===');
  const prevEnv = process.env.NODE_ENV;
  const prevAllow = process.env.ADMIN_ALLOW_QUERY_KEY;
  try {
    process.env.NODE_ENV = 'production';
    delete process.env.ADMIN_ALLOW_QUERY_KEY;

    assert(
      !allowAdminQueryKey(mkReq('/api/admin/status', {})),
      'production REST denies query key by default',
    );
    assert(
      allowAdminQueryKey(mkReq('/api/admin/live-session', {})),
      'SSE live-session allows query key in production',
    );

    const restQ = extractAdminKey(mkReq('/api/admin/status', { queryKey: 'verify-admin-secret' }));
    assert(!restQ.key, 'production REST extract ignores query key');

    const sseQ = extractAdminKey(mkReq('/api/admin/live-session', { queryKey: 'verify-admin-secret' }));
    assert(sseQ.key === 'verify-admin-secret' && sseQ.fromQuery, 'SSE extract accepts query key');

    const hdr = extractAdminKey(mkReq('/api/admin/status', { header: 'verify-admin-secret' }));
    assert(hdr.key === 'verify-admin-secret' && !hdr.fromQuery, 'Header still works in production');
  } finally {
    process.env.NODE_ENV = prevEnv;
    if (prevAllow !== undefined) process.env.ADMIN_ALLOW_QUERY_KEY = prevAllow;
    else delete process.env.ADMIN_ALLOW_QUERY_KEY;
  }
}

async function testHealthDraining(): Promise<void> {
  console.log('\n=== TC: STAB-06 /health draining ===');
  const { createApp, setShuttingDown } = await import('../server/src/createApp.js');
  const httpServer = http.createServer();
  const io = new Server<ClientToServerEvents, ServerToClientEvents>(httpServer);
  const app = createApp(ROOT, io);
  httpServer.on('request', app);

  await new Promise<void>((resolve) => {
    httpServer.listen(0, '127.0.0.1', () => resolve());
  });
  const port = (httpServer.address() as AddressInfo).port;
  const base = `http://127.0.0.1:${port}`;

  try {
    setShuttingDown(false);
    const okRes = await fetch(`${base}/health`);
    assert(okRes.status === 200, 'health 200 when not draining');
    const okBody = (await okRes.json()) as { ok: boolean };
    assert(okBody.ok === true, 'health ok:true when not draining');

    setShuttingDown(true);
    const drainRes = await fetch(`${base}/health`);
    assert(drainRes.status === 503, 'health 503 when draining');
    const drainBody = (await drainRes.json()) as { ok: boolean; draining?: boolean };
    assert(drainBody.ok === false && drainBody.draining === true, 'health draining payload');

    const readyRes = await fetch(`${base}/ready`);
    assert(readyRes.status === 503, 'ready 503 when draining');
  } finally {
    setShuttingDown(false);
    io.close();
    await new Promise<void>((resolve) => httpServer.close(() => resolve()));
  }
}

function testSocketRateLimit(): void {
  console.log('\n=== TC: STAB-04 socket event rate limit ===');
  resetSocketEventRateLimitForTests();
  const limit = getSocketEventRatePerSec();
  const sid = `rate-test-${Date.now()}`;
  for (let i = 0; i < limit; i++) {
    assert(allowSocketEvent(sid, 'send_chat'), `allow event #${i + 1}`);
  }
  assert(!allowSocketEvent(sid, 'send_chat'), 'blocks when over rate');
  assert(allowSocketEvent(sid, 'register_player'), 'non-limited events still allowed');
}

async function main(): Promise<void> {
  console.log('verify-backend-opt-c');
  testSourceGuards();
  testAdminQueryKeyPolicy();
  testSocketRateLimit();
  await testHealthDraining();
  console.log('\nAll BE-OPT-C checks passed.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
