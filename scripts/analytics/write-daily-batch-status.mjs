/**
 * 写出 docs/analytics/daily-batch-status.json（OPS-UX-1 §5.1）
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '../..');

/**
 * @param {{
 *   dateKey: string,
 *   exitCode: number,
 *   source?: 'scheduled-task' | 'manual' | 'npm',
 *   logFile?: string | null,
 *   durationMs?: number,
 *   message?: string,
 *   startedAtMs?: number,
 * }} opts
 */
export function writeDailyBatchStatus(opts) {
  const exitCode = Number(opts.exitCode) || 0;
  const durationMs =
    opts.durationMs != null
      ? opts.durationMs
      : opts.startedAtMs != null
        ? Date.now() - opts.startedAtMs
        : undefined;
  const payload = {
    updatedAt: new Date().toISOString(),
    dateKey: opts.dateKey,
    exitCode,
    ok: exitCode === 0,
    source: opts.source ?? process.env.DAILY_BATCH_SOURCE ?? 'npm',
    ...(opts.logFile || process.env.DAILY_BATCH_LOG
      ? { logFile: opts.logFile ?? process.env.DAILY_BATCH_LOG }
      : {}),
    ...(durationMs != null ? { durationMs } : {}),
    ...(opts.message ? { message: opts.message } : {}),
  };
  const outDir = path.join(projectRoot, 'docs/analytics');
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, 'daily-batch-status.json');
  fs.writeFileSync(outPath, JSON.stringify(payload, null, 2), 'utf8');
  return outPath;
}
