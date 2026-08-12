import express from 'express';
import { randomUUID } from 'crypto';
import type { AddressInfo } from 'net';
import type { Server as HttpServer } from 'http';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { registerSocialRoutes } from '../socialRoutes.js';
import { db } from '../db.js';
import { ensurePlayer } from '../players.js';
import { signPlayerToken } from '../auth.js';

process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'social-lobby-test-secret';

const ownerPlayerId = `social-lobby-owner-${randomUUID()}`;
const memberPlayerId = `social-lobby-member-${randomUUID()}`;
const ownerSteamId64 = '76561199123456789';
const memberSteamId64 = '76561199123456790';
const testSteamAppId = '2713340';
const originalSteamAppId = process.env.STEAM_APP_ID;

describe('social lobby lifecycle and invite authorization', () => {
  let server: HttpServer | undefined;

  beforeEach(() => {
    // The create route validates the binding against STEAM_APP_ID. Keep this
    // fixture deterministic even when another test or the shell exports one.
    process.env.STEAM_APP_ID = testSteamAppId;
  });

  afterEach(async () => {
    const activeServer = server;
    if (activeServer) await new Promise<void>((resolve) => activeServer.close(() => resolve()));
    if (originalSteamAppId === undefined) delete process.env.STEAM_APP_ID;
    else process.env.STEAM_APP_ID = originalSteamAppId;
    db.prepare('DELETE FROM steam_accounts WHERE player_id IN (?, ?)').run(
      ownerPlayerId,
      memberPlayerId,
    );
    db.prepare('DELETE FROM players WHERE player_id IN (?, ?)').run(
      ownerPlayerId,
      memberPlayerId,
    );
  });

  it('requires a signed target invite and keeps pond independent from lobby close', async () => {
    ensurePlayer(ownerPlayerId, 'Lobby owner');
    ensurePlayer(memberPlayerId, 'Lobby member');
    const now = Date.now();
    db.prepare(`
      INSERT INTO steam_accounts
        (id, steam_id64, player_id, app_id, created_at, last_login_at, revoked_at)
      VALUES (?, ?, ?, ?, ?, ?, NULL), (?, ?, ?, ?, ?, ?, NULL)
    `).run(
      randomUUID(), ownerSteamId64, ownerPlayerId, testSteamAppId, now, now,
      randomUUID(), memberSteamId64, memberPlayerId, testSteamAppId, now, now,
    );

    const app = express();
    app.use(express.json());
    registerSocialRoutes(app, {} as never);
    server = app.listen(0);
    const port = (server.address() as AddressInfo).port;
    const url = `http://127.0.0.1:${port}`;
    const auth = (playerId: string) => ({ Authorization: `Bearer ${signPlayerToken(playerId)}` });
    const post = async (path: string, body: unknown, playerId: string) => fetch(url + path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...auth(playerId) },
      body: JSON.stringify(body),
    });

    const lobbyId = '76561199123456791';
    const create = await post('/api/social/lobby/create', {
      lobbyId,
      pondId: 'pond-calm',
      gameVersion: '1.0-steam-desktop',
      protocolVersion: '1.0.0-draft',
    }, ownerPlayerId);
    expect(create.status).toBe(201);

    const invite = await post('/api/social/lobby/invite', {
      lobbyId,
      friendSteamId64: memberSteamId64,
    }, ownerPlayerId);
    expect(invite.status).toBe(200);
    const inviteBody = await invite.json() as { inviteToken: string };

    const rejected = await post('/api/social/lobby/join', {
      lobbyId,
      gameVersion: '1.0-steam-desktop',
      protocolVersion: '1.0.0-draft',
    }, memberPlayerId);
    expect(rejected.status).toBe(403);

    const joined = await post('/api/social/lobby/join', {
      lobbyId,
      gameVersion: '1.0-steam-desktop',
      protocolVersion: '1.0.0-draft',
      inviteToken: inviteBody.inviteToken,
    }, memberPlayerId);
    expect(joined.status).toBe(200);

    const closed = await post('/api/social/lobby/close', { lobbyId }, ownerPlayerId);
    expect(closed.status).toBe(200);

    const expired = await post('/api/social/lobby/join', {
      lobbyId,
      gameVersion: '1.0-steam-desktop',
      protocolVersion: '1.0.0-draft',
      inviteToken: inviteBody.inviteToken,
    }, memberPlayerId);
    expect(expired.status).toBe(404);
    expect((await expired.json()).code).toBe('LOBBY_CACHE_MISSING');
  });

  it('returns diagnostic permission codes for missing, revoked, and invalid bindings', async () => {
    ensurePlayer(ownerPlayerId, 'Lobby owner');
    const app = express();
    app.use(express.json());
    registerSocialRoutes(app, {} as never);
    server = app.listen(0);
    const port = (server.address() as AddressInfo).port;
    const url = `http://127.0.0.1:${port}`;
    const post = (playerId: string, body: unknown) => fetch(url + '/api/social/lobby/create', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${signPlayerToken(playerId)}`,
      },
      body: JSON.stringify(body),
    });
    const validBody = {
      lobbyId: '76561199123456792',
      pondId: 'pond-calm',
      gameVersion: '1.0-steam-desktop',
      protocolVersion: '1.0.0-draft',
    };

    const missing = await post(ownerPlayerId, validBody);
    expect(missing.status).toBe(403);
    expect((await missing.json()).code).toBe('LOBBY_STEAM_BINDING_REQUIRED');

    const now = Date.now();
    db.prepare(`
      INSERT INTO steam_accounts
        (id, steam_id64, player_id, app_id, created_at, last_login_at, revoked_at)
      VALUES (?, ?, ?, ?, ?, ?, ?)
    `).run(
      randomUUID(),
      ownerSteamId64,
      ownerPlayerId,
      testSteamAppId,
      now,
      now,
      now,
    );
    const revoked = await post(ownerPlayerId, validBody);
    expect(revoked.status).toBe(403);
    expect((await revoked.json()).code).toBe('LOBBY_STEAM_BINDING_REQUIRED');

    db.prepare('DELETE FROM steam_accounts WHERE player_id = ?').run(ownerPlayerId);
    db.prepare(`
      INSERT INTO steam_accounts
        (id, steam_id64, player_id, app_id, created_at, last_login_at, revoked_at)
      VALUES (?, ?, ?, ?, ?, ?, NULL)
    `).run(
      randomUUID(),
      ownerSteamId64,
      ownerPlayerId,
      testSteamAppId,
      now,
      now,
    );
    const invalidLobby = await post(ownerPlayerId, { ...validBody, lobbyId: 'bad' });
    expect(invalidLobby.status).toBe(400);
    expect((await invalidLobby.json()).code).toBe('LOBBY_ID_INVALID');
  });
});
