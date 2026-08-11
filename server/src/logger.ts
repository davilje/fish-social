import pino from 'pino';
import path from 'path';
import fs from 'fs';
import { getLokiStream } from './logTransportLoki.js';

/** QUAL-08: production defaults to mask; set LOG_MASK_USER_DATA=false to disable */
export function shouldMaskUserData(): boolean {
  const explicit = process.env.LOG_MASK_USER_DATA;
  if (explicit === 'true' || explicit === '1') return true;
  if (explicit === 'false' || explicit === '0') return false;
  return process.env.NODE_ENV === 'production';
}

const LOG_LEVEL_ENV = process.env.LOG_LEVEL || (process.env.NODE_ENV === 'production' ? 'info' : 'debug');
const LOG_DIR = process.env.LOG_DIR || 'logs';
const LOG_PRETTY = process.env.LOG_PRETTY === '1';
const LOG_TO_FILE = process.env.LOG_TO_FILE === '1' || process.env.NODE_ENV === 'production';
const IS_DOCKER = !process.env.LOG_DIR || process.env.LOG_DIR.trim() === '';

function resolveLogLevel(): string {
  if (LOG_LEVEL_ENV === 'production') return 'info';
  if (LOG_LEVEL_ENV === 'development') return 'debug';
  return LOG_LEVEL_ENV;
}

function createPinoLogger(): pino.Logger {
  const level = resolveLogLevel();
  const baseFields = { service: 'fish-social-server' };

  if (LOG_PRETTY) {
    return pino({
      level,
      base: baseFields,
      transport: {
        target: 'pino-pretty',
        options: { colorize: true, translateTime: 'HH:MM:ss.l' },
      },
    });
  }

  if (LOG_TO_FILE && !IS_DOCKER) {
    const logDir = path.resolve(LOG_DIR);
    if (!fs.existsSync(logDir)) {
      fs.mkdirSync(logDir, { recursive: true });
    }
    const logFile = path.join(logDir, 'server.log');
    return pino({
      level,
      base: baseFields,
      transport: {
        target: 'pino-roll',
        options: {
          file: logFile,
          frequency: 'daily',
          mkdir: true,
        },
      },
    });
  }

  return pino({
    level,
    base: baseFields,
    timestamp: pino.stdTimeFunctions.isoTime,
    sync: true,
  }, process.stdout);
}

const logger = createPinoLogger();

let lokiLogger: pino.Logger | null = null;
const lokiStream = getLokiStream();
if (lokiStream) {
  try {
    lokiLogger = pino({ level: 'debug', base: { service: 'fish-social-server' } }, lokiStream);
  } catch (e) {
    console.warn('[loki] Failed to create Loki logger:', e instanceof Error ? e.message : String(e));
  }
}

function safePinoCall(fn: () => void): void {
  try {
    fn();
  } catch (err) {
    const code = err && typeof err === 'object' ? (err as NodeJS.ErrnoException).code : undefined;
    if (code === 'EPIPE' || code === 'ECONNRESET') return;
    throw err;
  }
}

/** @deprecated Use logStructuredEvent from fishingObservability.ts */
export function logInfo(fields: Record<string, unknown>, msg?: string): void {
  safePinoCall(() => {
    logger.info(fields, msg ?? '');
    if (lokiLogger) lokiLogger.info(fields, msg ?? '');
  });
}

/** @deprecated Use logStructuredEvent from fishingObservability.ts */
export function logWarn(fields: Record<string, unknown>, msg?: string): void {
  safePinoCall(() => {
    logger.warn(fields, msg ?? '');
    if (lokiLogger) lokiLogger.warn(fields, msg ?? '');
  });
}

/** @deprecated Use logStructuredEvent from fishingObservability.ts */
export function logError(fields: Record<string, unknown>, msg?: string): void {
  safePinoCall(() => {
    logger.error(fields, msg ?? '');
    if (lokiLogger) lokiLogger.error(fields, msg ?? '');
  });
}

export function logDebug(fields: Record<string, unknown>, msg?: string): void {
  safePinoCall(() => {
    logger.debug(fields, msg ?? '');
    if (lokiLogger) lokiLogger.debug(fields, msg ?? '');
  });
}

function maskString(val: string): string {
  return val.length <= 2 ? val.substring(0, 1) + '***' : val.substring(0, 2) + '***';
}

export function maskSensitiveFields(fields: Record<string, unknown>): Record<string, unknown> {
  if (!shouldMaskUserData()) return fields;
  const masked = { ...fields };
  const sensitiveKeys = [
    'text',
    'nickname',
    'from_nickname',
    'to_nickname',
    'playerId',
    'authPlayerId',
    'bodyPlayerId',
  ];
  for (const key of sensitiveKeys) {
    if (typeof masked[key] === 'string' && (masked[key] as string).length > 0) {
      masked[key] = maskString(masked[key] as string);
    }
  }
  return masked;
}

type LogLevel = 'error' | 'warn' | 'info' | 'debug';

function getEventLevel(eventType: string): LogLevel {
  if (eventType === 'uncaught_exception' || eventType === 'metrics_flush_failure') return 'error';
  if (eventType === 'sqlite_query_slow' || eventType === 'phase_transition_invalid' || eventType === 'admin_route_slow') return 'warn';
  if (eventType.startsWith('bite_tick_') || eventType === 'snapshot_build_duration_ms' || eventType.startsWith('perf_')) return 'debug';
  return 'info';
}

function emitEventLog(actualLevel: LogLevel, logFields: Record<string, unknown>, msg: string): void {
  safePinoCall(() => {
    switch (actualLevel) {
      case 'error':
        logger.error(logFields, msg);
        if (lokiLogger) try { lokiLogger.error(logFields, msg); } catch {}
        break;
      case 'warn':
        logger.warn(logFields, msg);
        if (lokiLogger) try { lokiLogger.warn(logFields, msg); } catch {}
        break;
      case 'debug':
        logger.debug(logFields, msg);
        if (lokiLogger) try { lokiLogger.debug(logFields, msg); } catch {}
        break;
      default:
        logger.info(logFields, msg);
        if (lokiLogger) try { lokiLogger.info(logFields, msg); } catch {}
    }
  });
}

let pendingLogEvents: Promise<void> = Promise.resolve();

export function flushPendingLogEvents(): Promise<void> {
  return pendingLogEvents;
}

export function logEvent(prefix: string, eventType: string, fields: Record<string, unknown>): void {
  const maskedFields = maskSensitiveFields(fields);
  const eventLevel = getEventLevel(eventType);
  const rawPlayerId = typeof fields.playerId === 'string' ? fields.playerId : undefined;
  const msg = '[' + prefix + '] ' + eventType;
  const baseLogFields = {
    ...maskedFields,
    eventType,
    ts: maskedFields.ts ?? Date.now(),
  };

  pendingLogEvents = pendingLogEvents.then(async () => {
    try {
      const { isDebugSampled } = await import('./debugSampler.js');
      const isSampled = rawPlayerId ? isDebugSampled(rawPlayerId) : false;
      const actualLevel = isSampled && eventLevel === 'debug' ? 'info' : eventLevel;
      emitEventLog(actualLevel, {
        ...baseLogFields,
        ...(isSampled && eventLevel === 'debug' ? { debug_sampled: true } : {}),
      }, msg);
    } catch {
      emitEventLog(eventLevel, baseLogFields, msg);
    }
  });
}

export { logger };
