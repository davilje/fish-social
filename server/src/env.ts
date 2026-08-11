/**
 * QUAL-04: critical env catalog (human-readable: docs/ops/server-env.md).
 * Import side-effect free — lists keys for docs / assert helpers.
 */

export const CRITICAL_ENV = {
  JWT_SECRET: 'Required unless AUTH_DISABLED=1 in development',
  ADMIN_SECRET: 'Required in production',
  PLAYER_ERASE_PEPPER: 'Required in production (erase / anonymize)',
  ALLOWED_ORIGINS: 'Required in production; must not be *',
  MAX_HUMAN_SOCKETS: 'Soft-reject bound humans (default 200)',
  MAX_SOCKET_CONNECTIONS: 'Hard cap all Socket.IO clients (default 200)',
  SOCKET_EVENT_RATE_PER_SEC: 'Per-socket event budget (default 20)',
  ADMIN_ALLOW_QUERY_KEY: 'Allow ?key= on Admin REST (default false in production)',
  OPS_STATIC_ENABLED: 'Serve /planning /ops /analytics (default true; set false on public hosts)',
  LOG_MASK_USER_DATA: 'Mask playerId/nickname in logs (default true in production)',
  METRICS_PG_URL: 'Optional Postgres metrics dual-write',
  METRICS_DUAL_WRITE: 'Write SQLite + PG when true',
  METRICS_READ_FROM: 'Must be sqlite (postgres reads unsupported)',
  SHUTDOWN_TIMEOUT_MS: 'Force exit after graceful shutdown starts (default 8000)',
} as const;

export type CriticalEnvKey = keyof typeof CRITICAL_ENV;
