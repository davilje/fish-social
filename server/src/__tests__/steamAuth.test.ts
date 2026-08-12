import { describe, expect, it, afterEach } from 'vitest';
import { db } from '../db.js';
import {
  SteamAuthError,
  loginWithSteamTicket,
  type SteamTicketVerifier,
} from '../steamAuth.js';

process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'vitest-steam-jwt-secret';

const oldEnabled = process.env.STEAM_AUTH_ENABLED;
const oldAppId = process.env.STEAM_APP_ID;
const oldIdentity = process.env.STEAM_AUTH_IDENTITY;

afterEach(() => {
  if (oldEnabled === undefined) delete process.env.STEAM_AUTH_ENABLED;
  else process.env.STEAM_AUTH_ENABLED = oldEnabled;
  if (oldAppId === undefined) delete process.env.STEAM_APP_ID;
  else process.env.STEAM_APP_ID = oldAppId;
  if (oldIdentity === undefined) delete process.env.STEAM_AUTH_IDENTITY;
  else process.env.STEAM_AUTH_IDENTITY = oldIdentity;
  db.prepare('DELETE FROM steam_accounts WHERE steam_id64 LIKE ?').run('7656119%');
  db.prepare('DELETE FROM players WHERE player_id LIKE ?').run('steam_%');
});

const verifier: SteamTicketVerifier = {
  async verify(_ticket, appId) {
    return { steamId64: '76561198000000001', appId };
  },
};

describe('Steam ticket auth', () => {
  it('rejects disabled, empty ticket, and wrong app id', async () => {
    process.env.STEAM_AUTH_ENABLED = 'false';
    process.env.STEAM_APP_ID = '480';
    await expect(loginWithSteamTicket('ticket', '480', verifier)).rejects.toMatchObject({
      code: 'STEAM_AUTH_DISABLED',
    });

    process.env.STEAM_AUTH_ENABLED = 'true';
    await expect(loginWithSteamTicket('', '480', verifier)).rejects.toMatchObject({
      code: 'STEAM_MISSING_TICKET',
    });
    await expect(loginWithSteamTicket('ticket', '481', verifier)).rejects.toMatchObject({
      code: 'STEAM_INVALID_APP_ID',
    });

    process.env.STEAM_AUTH_IDENTITY = 'fish-social-server-v1';
    await expect(loginWithSteamTicket('ticket', '480', verifier, 'test-identity', 'wrong'))
      .rejects.toMatchObject({ code: 'STEAM_INVALID_IDENTITY' });
  });

  it('creates a player once and reuses it for the same SteamID64', async () => {
    process.env.STEAM_AUTH_ENABLED = 'true';
    process.env.STEAM_APP_ID = '480';

    const first = await loginWithSteamTicket('fake-ticket-a', '480', verifier, 'test-a');
    const second = await loginWithSteamTicket('fake-ticket-b', '480', verifier, 'test-b');

    expect(first.created).toBe(true);
    expect(second.created).toBe(false);
    expect(second.playerId).toBe(first.playerId);
    expect(first.accessToken.split('.')).toHaveLength(3);
  });

  it('does not expose ticket or key in the result', async () => {
    process.env.STEAM_AUTH_ENABLED = 'true';
    process.env.STEAM_APP_ID = '480';
    const result = await loginWithSteamTicket('secret-ticket', '480', verifier, 'test-c');
    expect(JSON.stringify(result)).not.toContain('secret-ticket');
    expect(JSON.stringify(result)).not.toContain('STEAM_WEB_API_KEY');
  });

  it('maps verifier rejection and revoked bindings to stable security errors', async () => {
    process.env.STEAM_AUTH_ENABLED = 'true';
    process.env.STEAM_APP_ID = '480';
    const rejectingVerifier: SteamTicketVerifier = {
      async verify() {
        throw new SteamAuthError('STEAM_TICKET_INVALID', 'invalid', 401);
      },
    };
    await expect(
      loginWithSteamTicket('bad-ticket', '480', rejectingVerifier, 'test-d'),
    ).rejects.toMatchObject({ code: 'STEAM_TICKET_INVALID', status: 401 });

    const result = await loginWithSteamTicket('good-ticket', '480', verifier, 'test-e');
    db.prepare('UPDATE steam_accounts SET revoked_at = ? WHERE player_id = ?').run(
      Date.now(),
      result.playerId,
    );
    await expect(loginWithSteamTicket('good-ticket-2', '480', verifier, 'test-f')).rejects.toMatchObject({
      code: 'STEAM_BINDING_CONFLICT',
      status: 403,
    });
  });
});

