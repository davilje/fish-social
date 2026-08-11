import { createServer } from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { Server } from 'socket.io';
import type { ClientToServerEvents, ServerToClientEvents } from '@fish-social/shared';
import './db.js';
import { installGlobalErrorHandlers, setFatalShutdownHandler } from './errorLog.js';
import { assertAuthConfigured, isAuthDisabled, verifyPlayerToken } from './auth.js';
import { assertErasePepperConfigured } from './playerAnonymize.js';
import { recoverPendingCatchLocksOnStartup } from './playerPondSession.js';
import { cancelAll as cancelAllTimers } from './timerRegistry.js';
import { expirePendingCatchFromDb, restorePendingCatchFromDb } from './inventory.js';
import { createApp, getCorsOriginPolicy, setShuttingDown } from './createApp.js';
import { registerSocketLifecycle } from './socketLifecycle.js';
import { startLoops, stopLoops } from './serverLoops.js';
import { closeDb } from './db.js';
import { logStructuredEvent } from './fishingObservability.js';
import { stopFishingMetricsQueue, recordFishingMetric, flushFishingMetricsQueue } from './fishingMetrics.js';
import { assertAdminSecurityConfigured } from './admin.js';
import { initOtelTracing, shutdownOtelTracing } from './otelTracing.js';
import { closePostgresPool } from './postgresMetricsStore.js';
import { flushPlayerPondSessionsOnShutdown } from './shutdownSessions.js';
import { SERVER_STARTED_AT, getServerLifecycleInfo } from './serverLifecycle.js';

installGlobalErrorHandlers();

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '../..');

const envPath = path.join(projectRoot, '.env');
if (fs.existsSync(envPath)) {
  for (const line of fs.readFileSync(envPath, 'utf8').split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eq = trimmed.indexOf('=');
    if (eq <= 0) continue;
    const key = trimmed.slice(0, eq).trim();
    let value = trimmed.slice(eq + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    if (!(key in process.env)) process.env[key] = value;
  }
}

const PORT = Number(process.env.PORT) || 3001;
const SHUTDOWN_TIMEOUT_MS = Number(process.env.SHUTDOWN_TIMEOUT_MS ?? 8000);
const LISTEN_RETRY_MS = 400;
const LISTEN_MAX_RETRIES = 12;

assertAuthConfigured();
assertErasePepperConfigured();
assertAdminSecurityConfigured();
const corsOrigins = getCorsOriginPolicy();

recoverPendingCatchLocksOnStartup(restorePendingCatchFromDb, expirePendingCatchFromDb);
void initOtelTracing();

const io = new Server<ClientToServerEvents, ServerToClientEvents>({
  cors: { origin: corsOrigins === '*' ? true : corsOrigins, credentials: true },
});
const app = createApp(projectRoot, io);
const httpServer = createServer(app);
io.attach(httpServer);

io.use((socket, next) => {
  if (isAuthDisabled()) return next();
  const token = socket.handshake.auth?.token as string | undefined;
  const payload = verifyPlayerToken(token);
  if (!payload) {
    logStructuredEvent('auth', 'auth_failed', {
      reason: 'invalid_socket_token',
      socketId: socket.id,
    });
    return next(new Error('unauthorized'));
  }
  socket.data.authPlayerId = payload.playerId;
  next();
});

function roomFanoutCount(pondId: string): number {
  return io.sockets.adapter.rooms.get(pondId)?.size ?? 0;
}
registerSocketLifecycle({ io, roomFanoutCount });
startLoops({ io, roomFanoutCount });

let shuttingDown = false;

async function shutdown(signal: string, exitCode = 0): Promise<void> {
  if (shuttingDown) return;
  shuttingDown = true;
  setShuttingDown(true);
  const life = getServerLifecycleInfo();
  recordFishingMetric('server_stop', {
    payload: { pid: life.pid, uptimeSec: life.uptimeSec, reason: signal, startedAt: SERVER_STARTED_AT },
  });
  logStructuredEvent('shutdown', 'shutdown_started', { signal, eventType: 'shutdown_started' });

  logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'stop_loops', reason: signal });
  stopLoops();

  // STAB-01: persist in-pond humans before closing sockets/db
  flushPlayerPondSessionsOnShutdown();

  logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'cancel_timers', reason: signal });
  cancelAllTimers();

  // STAB-02: stop metrics queue first, then await OTEL + PG pool
  logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'flush_metrics', reason: signal });
  stopFishingMetricsQueue();
  flushFishingMetricsQueue();

  logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'shutdown_otel', reason: signal });
  try {
    await shutdownOtelTracing();
  } catch {
    // best effort
  }

  logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'close_pg_pool', reason: signal });
  try {
    await closePostgresPool();
  } catch {
    // best effort
  }

  const forceExit = setTimeout(() => {
    logStructuredEvent('shutdown', 'shutdown_timeout', { signal, eventType: 'shutdown_timeout' });
    process.exit(1);
  }, SHUTDOWN_TIMEOUT_MS);
  forceExit.unref();

  logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'close_io', reason: signal });
  await new Promise<void>((resolve) => {
    io.close(() => {
      logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'close_http', reason: signal });
      httpServer.close(() => {
        logStructuredEvent('shutdown', 'shutdown_phase', { phase: 'close_db', reason: signal });
        closeDb();
        clearTimeout(forceExit);
        resolve();
      });
    });
  });

  logStructuredEvent('shutdown', 'shutdown_complete', { signal, eventType: 'shutdown_complete' });
  process.exit(exitCode);
}

process.on('SIGINT', () => {
  void shutdown('SIGINT');
});
process.on('SIGTERM', () => {
  void shutdown('SIGTERM');
});

// STAB-03: fatal → ordered shutdown → exit(1); EPIPE still ignored in errorLog
setFatalShutdownHandler((reason) => {
  void shutdown(reason, 1);
});

function onServerReady() {
  console.log(`Fish Social server running on http://localhost:${PORT}`);
  logStructuredEvent('startup', 'server_ready', { port: PORT, eventType: 'server_ready' });
  recordFishingMetric('server_start', {
    payload: {
      pid: process.pid,
      startedAt: SERVER_STARTED_AT,
      reason: process.env.SERVER_START_REASON ?? 'unknown',
    },
  });
}

function startListening(retriesLeft = LISTEN_MAX_RETRIES) {
  const onListening = () => {
    httpServer.off('error', onError);
    onServerReady();
  };

  const onError = (err: NodeJS.ErrnoException) => {
    httpServer.off('error', onError);
    const afterClose = () => {
      if (err.code === 'EADDRINUSE' && retriesLeft > 0) {
        setTimeout(() => startListening(retriesLeft - 1), LISTEN_RETRY_MS);
        return;
      }
      if (err.code === 'EADDRINUSE') {
        console.error(`端口 ${PORT} 已被占用，请运行 npm run ports:free`);
        process.exit(1);
        return;
      }
      console.error('[error] httpServer', err);
      process.exit(1);
    };
    httpServer.close(() => afterClose());
  };

  httpServer.once('error', onError);
  httpServer.listen(PORT, onListening);
}

startListening();
