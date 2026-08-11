/**
 * BE-OPT-A (SEC-01～06) 验收
 * 运行: npm run verify:backend-opt-a
 */
process.env.NODE_ENV = 'development';
process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'verify-be-opt-a-secret';
process.env.PLAYER_ERASE_PEPPER = process.env.PLAYER_ERASE_PEPPER ?? 'verify-pepper';
delete process.env.AUTH_DISABLED;

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import http from 'http';
import type { AddressInfo } from 'net';
import { Server } from 'socket.io';
import type { ClientToServerEvents, ServerToClientEvents } from '@fish-social/shared';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(rel: string): string {
  return fs.readFileSync(path.join(ROOT, rel), 'utf8');
}

function testSourceGuards(): void {
  console.log('\n=== TC: source guards ===');
  const auth = read('server/src/auth.ts');
  assert(auth.includes('requireSelf'), 'requireSelf helper present');

  const social = read('server/src/socialRoutes.ts');
  assert(social.includes('player_id_taken'), 'register rejects taken playerId');
  assert(social.includes("requireSelf('playerId')"), 'social private GETs use requireSelf');

  const app = read('server/src/createApp.ts');
  assert(app.includes("requireSelf('playerId')"), 'inventory uses requireSelf');
  assert(app.includes('CLIENT_LOGS_MAX') || app.includes('logs_batch_limit'), 'client-logs batch limit');
  assert(app.includes("requireAuth"), 'client-logs requireAuth');

  const anon = read('server/src/playerAnonymize.ts');
  assert(anon.includes('assertErasePepperConfigured'), 'pepper assert exported');
  assert(anon.includes('throw new Error'), 'pepper hard-fails in production');

  const index = read('server/src/index.ts');
  assert(index.includes('assertErasePepperConfigured'), 'startup asserts pepper');

  const envEx = read('.env.example');
  assert(envEx.includes('PLAYER_ERASE_PEPPER'), '.env.example documents PLAYER_ERASE_PEPPER');

  const inv = read('mobile/lib/useInventory.ts');
  assert(inv.includes('apiFetch'), 'mobile inventory sends Authorization via apiFetch');
}

async function testPepperProductionFail(): Promise<void> {
  console.log('\n=== TC: SEC-06 pepper production hard-fail ===');
  const prev = process.env.NODE_ENV;
  const prevPepper = process.env.PLAYER_ERASE_PEPPER;
  try {
    process.env.NODE_ENV = 'production';
    delete process.env.PLAYER_ERASE_PEPPER;
    // Re-import fresh module state is hard; call assert directly after clearing
    const mod = await import('../server/src/playerAnonymize.js');
    let threw = false;
    try {
      mod.assertErasePepperConfigured();
    } catch {
      threw = true;
    }
    // Module may have been loaded earlier with pepper set — still check getErasePepper throws
    let getThrew = false;
    try {
      mod.getErasePepper();
    } catch {
      getThrew = true;
    }
    assert(threw || getThrew, 'production without pepper throws');
  } finally {
    process.env.NODE_ENV = prev;
    if (prevPepper) process.env.PLAYER_ERASE_PEPPER = prevPepper;
    else process.env.PLAYER_ERASE_PEPPER = 'verify-pepper';
  }
}

async function withTestServer(
  fn: (base: string, helpers: { sign: (id: string) => string }) => Promise<void>,
): Promise<void> {
  const { createApp } = await import('../server/src/createApp.js');
  const { signPlayerToken } = await import('../server/src/auth.js');
  const { ensurePlayer } = await import('../server/src/players.js');

  const httpServer = http.createServer();
  const io = new Server<ClientToServerEvents, ServerToClientEvents>(httpServer);
  const app = createApp(ROOT, io);
  httpServer.on('request', app);

  await new Promise<void>((resolve) => {
    httpServer.listen(0, '127.0.0.1', () => resolve());
  });
  const { port } = httpServer.address() as AddressInfo;
  const base = `http://127.0.0.1:${port}`;

  ensurePlayer('victim-opt-a', '受害者');
  ensurePlayer('self-opt-a', '本人');

  try {
    await fn(base, { sign: signPlayerToken });
  } finally {
    io.close();
    await new Promise<void>((resolve, reject) => {
      httpServer.close((err) => (err ? reject(err) : resolve()));
    });
  }
}

async function testHttpSec(): Promise<void> {
  console.log('\n=== TC: SEC-01/02/03/04/05 HTTP ===');
  await withTestServer(async (base, { sign }) => {
    // SEC-01: cannot register as existing player
    const regTaken = await fetch(`${base}/api/players/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId: 'victim-opt-a', nickname: '黑客' }),
    });
    assert(regTaken.status === 403, 'register existing playerId → 403');

    // SEC-01: new id ok + token
    const regOk = await fetch(`${base}/api/players/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId: `new-${Date.now()}`, nickname: '新人' }),
    });
    const regBody = (await regOk.json()) as { token?: string; profile?: { playerId: string } };
    assert(regOk.ok && !!regBody.token, 'register new player returns token');

    // SEC-02/03: unauth inventory → 401
    const invUnauth = await fetch(`${base}/api/inventory/victim-opt-a`);
    assert(invUnauth.status === 401, 'inventory without token → 401');

    // mismatch → 403
    const selfTok = sign('self-opt-a');
    const invMismatch = await fetch(`${base}/api/inventory/victim-opt-a`, {
      headers: { Authorization: `Bearer ${selfTok}` },
    });
    assert(invMismatch.status === 403, 'inventory identity mismatch → 403');

    const invOk = await fetch(`${base}/api/inventory/self-opt-a`, {
      headers: { Authorization: `Bearer ${selfTok}` },
    });
    assert(invOk.ok, 'inventory self → 200');

    // SEC-04 DM
    const dmUnauth = await fetch(`${base}/api/dm/conversations/victim-opt-a`);
    assert(dmUnauth.status === 401, 'dm list without token → 401');

    const dmMismatch = await fetch(`${base}/api/dm/conversations/victim-opt-a`, {
      headers: { Authorization: `Bearer ${selfTok}` },
    });
    assert(dmMismatch.status === 403, 'dm list mismatch → 403');

    // SEC-05 client-logs
    const logsUnauth = await fetch(`${base}/api/client-logs`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ logs: [{ ts: Date.now(), level: 'info', eventType: 't' }] }),
    });
    assert(logsUnauth.status === 401, 'client-logs without token → 401');

    const tooMany = Array.from({ length: 51 }, (_, i) => ({
      ts: Date.now(),
      level: 'info',
      eventType: `e${i}`,
    }));
    const logsOver = await fetch(`${base}/api/client-logs`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${selfTok}`,
      },
      body: JSON.stringify({ logs: tooMany }),
    });
    assert(logsOver.status === 400, 'client-logs >50 → 400');

    const logsOk = await fetch(`${base}/api/client-logs`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${selfTok}`,
      },
      body: JSON.stringify({
        logs: [{ ts: Date.now(), level: 'info', eventType: 'ok', playerId: 'forged' }],
      }),
    });
    assert(logsOk.ok, 'client-logs authed → 200');

    // SEC-01 production: ignore client playerId, always mint
    const prevEnv = process.env.NODE_ENV;
    process.env.NODE_ENV = 'production';
    try {
      const prodReg = await fetch(`${base}/api/players/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerId: 'victim-opt-a', nickname: '产线冒充' }),
      });
      const prodBody = (await prodReg.json()) as {
        token?: string;
        profile?: { playerId: string };
      };
      assert(prodReg.ok && !!prodBody.token, 'production register ok');
      assert(
        prodBody.profile?.playerId !== 'victim-opt-a',
        'production ignores client playerId (mints new)',
      );
    } finally {
      process.env.NODE_ENV = prevEnv;
    }
  });
}

async function main(): Promise<void> {
  console.log('verify-backend-opt-a');
  testSourceGuards();
  await testPepperProductionFail();
  await testHttpSec();
  console.log('\nAll BE-OPT-A checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
