import { randomUUID } from 'crypto';
import { db } from './db.js';
import { logError as logToLogger } from './logger.js';

export interface ErrorLogEntry {
  id: string;
  message: string;
  stack?: string;
  context?: string;
  correlationId?: string;
  createdAt: number;
}

const MAX_LOGS_MEM = 50;
const memoryRing: ErrorLogEntry[] = [];

const insertStmt = db.prepare(`
  INSERT INTO error_logs (id, message, stack, context, correlation_id, created_at)
  VALUES (@id, @message, @stack, @context, @correlation_id, @createdAt)
`);

const RETENTION_DAYS = Number(process.env.ERROR_LOG_RETENTION_DAYS ?? 90);

/** stdout/stderr already closed (parent pipe gone) — not an app bug */
function isBenignStreamError(err: unknown): boolean {
  if (!err || typeof err !== 'object') return false;
  const code = (err as NodeJS.ErrnoException).code;
  return code === 'EPIPE' || code === 'ECONNRESET';
}

function looksLikeEpipeMessage(message: string): boolean {
  return /EPIPE|broken pipe/i.test(message);
}

function enforceRetention(): void {
  const cutoff = Date.now() - RETENTION_DAYS * 24 * 60 * 60 * 1000;
  db.prepare('DELETE FROM error_logs WHERE created_at < ?').run(cutoff);
}

export function logError(message: string, error?: unknown, context?: string, correlationId?: string): void {
  // Never feed EPIPE back into the logger — that is what caused the noon flood.
  if (isBenignStreamError(error) || looksLikeEpipeMessage(message)) return;

  const entry: ErrorLogEntry = {
    id: randomUUID(),
    message,
    stack: error instanceof Error ? error.stack : undefined,
    context,
    correlationId,
    createdAt: Date.now(),
  };
  memoryRing.unshift(entry);
  if (memoryRing.length > MAX_LOGS_MEM) memoryRing.pop();

  try {
    insertStmt.run({
      id: entry.id,
      message: entry.message,
      stack: entry.stack ?? null,
      context: entry.context ?? null,
      correlation_id: entry.correlationId ?? null,
      createdAt: entry.createdAt,
    });
  } catch (e) {
    try {
      logToLogger({ error: String(e) }, 'failed to persist error_log');
    } catch {
      /* ignore logger EPIPE */
    }
  }

  try {
    logToLogger({ message, context, correlationId }, `[${context ?? 'error'}] ${message}`);
  } catch {
    /* stdout closed */
  }
}

export function getErrorLogs(limit = 100): ErrorLogEntry[] {
  const cappedLimit = Math.min(200, Math.max(1, limit));
  return db
    .prepare('SELECT id, message, stack, context, correlation_id as correlationId, created_at as createdAt FROM error_logs ORDER BY created_at DESC LIMIT ?')
    .all(cappedLimit) as ErrorLogEntry[];
}

export function getErrorLogsSince(since: number, limit = 200): ErrorLogEntry[] {
  const cappedLimit = Math.min(200, Math.max(1, limit));
  return db
    .prepare('SELECT id, message, stack, context, correlation_id as correlationId, created_at as createdAt FROM error_logs WHERE created_at >= ? ORDER BY created_at DESC LIMIT ?')
    .all(since, cappedLimit) as ErrorLogEntry[];
}

export function getErrorLogsByContext(context: string, limit = 200): ErrorLogEntry[] {
  const cappedLimit = Math.min(200, Math.max(1, limit));
  return db
    .prepare('SELECT id, message, stack, context, correlation_id as correlationId, created_at as createdAt FROM error_logs WHERE context = ? ORDER BY created_at DESC LIMIT ?')
    .all(context, cappedLimit) as ErrorLogEntry[];
}

export function clearErrorLogs(): void {
  db.prepare('DELETE FROM error_logs').run();
  memoryRing.length = 0;
}

export function getRecentErrorLogs(limit = 50): ErrorLogEntry[] {
  return memoryRing.slice(0, Math.min(limit, MAX_LOGS_MEM));
}

let handlingUncaught = false;
let fatalShutdownHandler: ((reason: string) => void) | null = null;

/** STAB-03: wire from index.ts after shutdown is defined */
export function setFatalShutdownHandler(fn: (reason: string) => void): void {
  fatalShutdownHandler = fn;
}

export function installGlobalErrorHandlers(): void {
  enforceRetention();

  // Without this, writing to a closed pipe raises uncaughtException → log → EPIPE loop.
  for (const stream of [process.stdout, process.stderr]) {
    stream.on('error', (err) => {
      if (isBenignStreamError(err)) return;
    });
  }

  process.on('uncaughtException', (err) => {
    if (isBenignStreamError(err)) return;
    if (handlingUncaught) return;
    handlingUncaught = true;
    try {
      logError(err.message, err, 'uncaughtException');
    } catch {
      /* ignore */
    }
    if (fatalShutdownHandler) {
      fatalShutdownHandler('uncaughtException');
    } else {
      process.exit(1);
    }
  });
  process.on('unhandledRejection', (reason) => {
    if (isBenignStreamError(reason)) return;
    logError('Unhandled rejection', reason, 'unhandledRejection');
  });
}
