/**
 * BUGFIX：日扣费满 4 次 / 今日 8h 后不可 start_fishing；仍可逻辑上 seated。
 */
import '../server/src/db.js';
import { MAX_DAILY_FISHING_MS, ADMISSION_FEE_SLICE_MS } from '@fish-social/shared';
import { ensurePlayer, addCoins } from '../server/src/players.js';
import {
  applyAdmissionFeeProgress,
  canStartFishingWithFee,
  getAdmissionFeeState,
} from '../server/src/playerProgress.js';
import { addTodayFishingMs, getTodayFishingMs } from '../server/src/pondUserManager.js';

const id = `test-quota-daycap-${Date.now()}`;
ensurePlayer(id, 'QuotaCap');
addCoins(id, 1_000_000);

// 选一个收费塘（calm 通常有 fee）
const pondId = 'pond-calm';
const gate0 = canStartFishingWithFee(id, pondId);
if (!gate0.ok) throw new Error(`expected start ok before charges: ${gate0.error}`);

for (let i = 0; i < 4; i++) {
  const r = applyAdmissionFeeProgress(id, pondId, ADMISSION_FEE_SLICE_MS);
  if (r.kind !== 'ok') throw new Error(`charge ${i + 1} failed`);
}

const state = getAdmissionFeeState(id);
if (state.charges < 4) throw new Error(`expected 4 charges, got ${state.charges}`);

const gateFull = canStartFishingWithFee(id, pondId);
if (gateFull.ok) throw new Error('expected start blocked after 4 fee charges');
if (!gateFull.error.includes('已用完')) {
  throw new Error(`unexpected error: ${gateFull.error}`);
}

// 日额度满同样应挡住（免费塘路径）
const id2 = `test-quota-ms-${Date.now()}`;
ensurePlayer(id2, 'QuotaMs');
addTodayFishingMs(id2, MAX_DAILY_FISHING_MS);
if (getTodayFishingMs(id2) < MAX_DAILY_FISHING_MS) {
  throw new Error('today ms not at cap');
}

console.log('quota day-cap gate ok:', {
  feeCharges: state.charges,
  feeBlock: gateFull.error,
  todayMs: getTodayFishingMs(id2),
});
