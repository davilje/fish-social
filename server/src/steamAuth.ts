import { randomUUID } from 'crypto';
import { db } from './db.js';
import { ensurePlayer, getPlayer } from './players.js';
import { logStructuredEvent } from './fishingObservability.js';
import { signPlayerToken } from './auth.js';

export type SteamAuthErrorCode =
  | 'STEAM_AUTH_DISABLED'
  | 'STEAM_MISSING_TICKET'
  | 'STEAM_INVALID_APP_ID'
  | 'STEAM_NOT_CONFIGURED'
  | 'STEAM_TICKET_INVALID'
  | 'STEAM_BINDING_CONFLICT'
  | 'STEAM_RATE_LIMITED';

export class SteamAuthError extends Error {
  constructor(
    public readonly code: SteamAuthErrorCode,
    message: string,
    public readonly status = 400,
  ) {
    super(message);
  }
}

export interface SteamTicketIdentity {
  steamId64: string;
  appId: string;
}

export interface SteamTicketVerifier {
  verify(ticket: string, appId: string): Promise<SteamTicketIdentity>;
}

const getAccountBySteamStmt = db.prepare(
  'SELECT steam_id64, player_id, app_id, revoked_at FROM steam_accounts WHERE steam_id64 = ?',
);
const getAccountByPlayerStmt = db.prepare(
  'SELECT steam_id64, player_id, app_id, revoked_at FROM steam_accounts WHERE player_id = ?',
);
const insertAccountStmt = db.prepare(`
  INSERT INTO steam_accounts
    (id, steam_id64, player_id, app_id, created_at, last_login_at, revoked_at)
  VALUES
    (@id, @steamId64, @playerId, @appId, @now, @now, NULL)
`);
const touchAccountStmt = db.prepare(
  'UPDATE steam_accounts SET last_login_at = ?, app_id = ? WHERE steam_id64 = ?',
);

const attempts = new Map<string, { count: number; resetAt: number }>();
const RATE_WINDOW_MS = 60_000;
const RATE_LIMIT = 10;

function configuredAppId(): string {
  const appId = process.env.STEAM_APP_ID?.trim();
  if (!appId) throw new SteamAuthError('STEAM_NOT_CONFIGURED', 'Steam 登录尚未配置', 503);
  return appId;
}

function isEnabled(): boolean {
  return process.env.STEAM_AUTH_ENABLED === 'true' || process.env.STEAM_AUTH_ENABLED === '1';
}

function assertRateLimit(key: string): void {
  const now = Date.now();
  const current = attempts.get(key);
  if (!current || current.resetAt <= now) {
    attempts.set(key, { count: 1, resetAt: now + RATE_WINDOW_MS });
    return;
  }
  current.count += 1;
  if (current.count > RATE_LIMIT) {
    throw new SteamAuthError('STEAM_RATE_LIMITED', '登录请求过于频繁，请稍后再试', 429);
  }
}

function audit(eventType: string, fields: Record<string, unknown>): void {
  logStructuredEvent('steam_auth', eventType, {
    eventType,
    ...fields,
  });
}

function validateSteamId64(value: unknown): string {
  if (typeof value !== 'string' || !/^\d{17}$/.test(value)) {
    throw new SteamAuthError('STEAM_TICKET_INVALID', 'Steam Ticket 无效', 401);
  }
  return value;
}

export class SteamWebApiTicketVerifier implements SteamTicketVerifier {
  async verify(ticket: string, appId: string): Promise<SteamTicketIdentity> {
    const key = process.env.STEAM_WEB_API_KEY?.trim();
    if (!key) throw new SteamAuthError('STEAM_NOT_CONFIGURED', 'Steam 登录尚未配置', 503);
    const identity = process.env.STEAM_AUTH_IDENTITY?.trim();
    if (!identity) throw new SteamAuthError('STEAM_NOT_CONFIGURED', 'Steam 登录身份未配置', 503);

    const url = new URL(
      'https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/',
    );
    url.searchParams.set('key', key);
    url.searchParams.set('appid', appId);
    url.searchParams.set('ticket', ticket);
    url.searchParams.set('identity', identity);

    let response: Response;
    try {
      response = await fetch(url);
    } catch {
      throw new SteamAuthError('STEAM_TICKET_INVALID', 'Steam 验证服务不可用', 502);
    }
    if (!response.ok) {
      throw new SteamAuthError('STEAM_TICKET_INVALID', 'Steam Ticket 验证失败', 401);
    }

    let payload: unknown;
    try {
      payload = await response.json();
    } catch {
      throw new SteamAuthError('STEAM_TICKET_INVALID', 'Steam Ticket 验证失败', 401);
    }
    const params = (payload as { response?: { params?: { steamid?: unknown; result?: unknown } } })
      .response?.params;
    if (params?.result !== 'OK') {
      throw new SteamAuthError('STEAM_TICKET_INVALID', 'Steam Ticket 无效', 401);
    }
    return { steamId64: validateSteamId64(params.steamid), appId };
  }
}

export interface SteamLoginResult {
  playerId: string;
  accessToken: string;
  created: boolean;
}

export async function loginWithSteamTicket(
  ticket: unknown,
  requestedAppId: unknown,
  verifier: SteamTicketVerifier = new SteamWebApiTicketVerifier(),
  rateKey = 'unknown',
): Promise<SteamLoginResult> {
  if (!isEnabled()) throw new SteamAuthError('STEAM_AUTH_DISABLED', 'Steam 登录未启用', 404);
  assertRateLimit(rateKey);
  if (typeof ticket !== 'string' || ticket.trim().length === 0) {
    audit('steam_login_rejected', { reason: 'missing_ticket' });
    throw new SteamAuthError('STEAM_MISSING_TICKET', '缺少 Steam Ticket');
  }
  if (ticket.length > 4096) {
    audit('steam_login_rejected', { reason: 'ticket_too_large' });
    throw new SteamAuthError('STEAM_TICKET_INVALID', 'Steam Ticket 无效', 401);
  }

  const appId = configuredAppId();
  if (typeof requestedAppId !== 'string' || requestedAppId !== appId) {
    audit('steam_login_rejected', { reason: 'invalid_app_id' });
    throw new SteamAuthError('STEAM_INVALID_APP_ID', 'Steam App ID 不匹配');
  }

  let identity: SteamTicketIdentity;
  try {
    identity = await verifier.verify(ticket, appId);
  } catch (error) {
    audit('steam_login_rejected', {
      reason: error instanceof SteamAuthError ? error.code : 'ticket_verification_failed',
    });
    throw error;
  }
  if (identity.appId !== appId) {
    throw new SteamAuthError('STEAM_INVALID_APP_ID', 'Steam App ID 不匹配');
  }

  const now = Date.now();
  const existing = getAccountBySteamStmt.get(identity.steamId64) as
    | { steam_id64: string; player_id: string; app_id: string; revoked_at: number | null }
    | undefined;
  if (existing?.revoked_at) {
    throw new SteamAuthError('STEAM_BINDING_CONFLICT', 'Steam 账号已被禁用', 403);
  }

  let playerId: string;
  let created = false;
  if (existing) {
    playerId = existing.player_id;
    touchAccountStmt.run(now, appId, identity.steamId64);
    if (!getPlayer(playerId)) {
      audit('steam_login_rejected', { reason: 'binding_player_missing' });
      throw new SteamAuthError('STEAM_BINDING_CONFLICT', 'Steam 账号绑定异常', 409);
    }
  } else {
    const samePlayer = getAccountByPlayerStmt.get(`steam_${identity.steamId64}`) as unknown;
    if (samePlayer) {
      throw new SteamAuthError('STEAM_BINDING_CONFLICT', 'Steam 账号绑定冲突', 409);
    }
    playerId = `steam_${randomUUID()}`;
    ensurePlayer(playerId, 'Steam玩家');
    insertAccountStmt.run({
      id: randomUUID(),
      steamId64: identity.steamId64,
      playerId,
      appId,
      now,
    });
    created = true;
  }

  audit('steam_login_success', {
    steamId64: identity.steamId64,
    playerId,
    created,
  });
  return { playerId, accessToken: signPlayerToken(playerId), created };
}

