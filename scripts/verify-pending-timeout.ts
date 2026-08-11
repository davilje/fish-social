/**
 * v0.4.4-patch 验收：pending_catch_expired 真实超时慢测
 * 默认快速模式仅提示；加 --slow 才真实等待。
 */
import '../server/src/db.js';
import { lockPendingCatch, PENDING_CATCH_TIMEOUT_MS, getPendingCatch } from '../server/src/inventory.js';
import { getPlayerFishingTimeline } from '../server/src/fishingMetrics.js';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function main(): Promise<void> {
  console.log('verify-pending-timeout');
  if (!process.argv.includes('--slow')) {
    console.log('SKIP: fast mode (run with --slow for real timeout waiting)');
    return;
  }

  const playerId = `verify-pending-${Date.now()}`;
  const userId = `verify-user-${Date.now()}`;
  const pondId = 'pond-calm';
  const pondFishId = `verify-pond-fish-${Date.now()}`;
  const catchId = `verify-catch-${Date.now()}`;

  const locked = lockPendingCatch(
    userId,
    {
      catchId,
      pondFishId,
      speciesId: 'carp',
      quality: 'gray',
      sizeM: 0.25,
      hookDurationMs: 1000,
    },
    { playerId, pondId },
  );
  assert(Boolean(locked), 'pending catch created');
  assert(getPendingCatch(userId)?.catchId === catchId, 'pending exists before timeout');

  const waitMs = PENDING_CATCH_TIMEOUT_MS + 1500;
  console.log(`  waiting ${waitMs}ms for real timeout...`);
  await sleep(waitMs);

  assert(getPendingCatch(userId) === undefined, 'pending cleared after timeout');
  const relock = lockPendingCatch(
    userId,
    {
      catchId: `${catchId}-relock`,
      pondFishId,
      speciesId: 'carp',
      quality: 'gray',
      sizeM: 0.25,
      hookDurationMs: 1000,
    },
    { playerId, pondId },
  );
  assert(Boolean(relock), 'fish lock released and can lock again');

  const timeline = getPlayerFishingTimeline(playerId, 2, 500);
  assert(timeline.summary.pendingCatchExpiredCount >= 1, 'pending_catch_expired counted');
  console.log('All pending timeout checks passed.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
