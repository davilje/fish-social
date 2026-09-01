/**
 * FEAT-RETURN-02+：本塘 session 鱼获 / 回鱼 / 扣费 / XP 台账，离塘时汇总。
 */
import type { ReturnFeeMode } from '@fish-social/shared';
import { getGamePondDef } from '@fish-social/shared';

export type SessionCatchOutcome = 'returned' | 'kept';

export type SessionCatchEntry = {
  speciesId: string;
  quality: string;
  sizeM: number;
  outcome: SessionCatchOutcome;
  returnGold?: number;
  catchPlayerXp?: number;
  catchPondXp?: number;
  returnPlayerXp?: number;
  returnPondXp?: number;
  caughtAt: number;
};

export type PondSessionSummary = {
  pondId: string;
  pondName: string;
  returnFeeMode: ReturnFeeMode;
  catches: SessionCatchEntry[];
  feesPaid: number;
  totalReturnGold: number;
  totalCatchPlayerXp: number;
  totalCatchPondXp: number;
  totalReturnPlayerXp: number;
  totalReturnPondXp: number;
  netProfit: number;
  joinedAt: number;
  leftAt: number;
};

type Ledger = {
  pondId: string;
  returnFeeMode: ReturnFeeMode;
  catches: SessionCatchEntry[];
  feesPaid: number;
  joinedAt: number;
};

const ledgers = new Map<string, Ledger>();

export function ensurePondSessionLedger(
  playerId: string,
  pondId: string,
  returnFeeMode: ReturnFeeMode,
): void {
  if (ledgers.has(playerId)) return;
  startPondSessionLedger(playerId, pondId, returnFeeMode);
}

function startPondSessionLedger(
  playerId: string,
  pondId: string,
  returnFeeMode: ReturnFeeMode,
): void {
  ledgers.set(playerId, {
    pondId,
    returnFeeMode,
    catches: [],
    feesPaid: 0,
    joinedAt: Date.now(),
  });
}

export function clearPondSessionLedger(playerId: string): void {
  ledgers.delete(playerId);
}

export function recordSessionFeePaid(playerId: string, amount: number): void {
  if (amount <= 0) return;
  const ledger = ledgers.get(playerId);
  if (!ledger) return;
  ledger.feesPaid += amount;
}

export function getSessionCatchCount(playerId: string | null | undefined): number {
  if (!playerId) return 0;
  const ledger = ledgers.get(playerId);
  return ledger ? ledger.catches.length : 0;
}

export function recordSessionCatch(
  playerId: string,
  entry: SessionCatchEntry,
): void {
  const ledger = ledgers.get(playerId);
  if (!ledger) return;
  ledger.catches.push(entry);
}

export function buildPondSessionSummary(playerId: string): PondSessionSummary | null {
  const ledger = ledgers.get(playerId);
  if (!ledger) return null;

  let totalReturnGold = 0;
  let totalCatchPlayerXp = 0;
  let totalCatchPondXp = 0;
  let totalReturnPlayerXp = 0;
  let totalReturnPondXp = 0;

  for (const c of ledger.catches) {
    if (c.returnGold) totalReturnGold += c.returnGold;
    if (c.catchPlayerXp) totalCatchPlayerXp += c.catchPlayerXp;
    if (c.catchPondXp) totalCatchPondXp += c.catchPondXp;
    if (c.returnPlayerXp) totalReturnPlayerXp += c.returnPlayerXp;
    if (c.returnPondXp) totalReturnPondXp += c.returnPondXp;
  }

  const pondDef = getGamePondDef(ledger.pondId);
  const leftAt = Date.now();

  return {
    pondId: ledger.pondId,
    pondName: pondDef?.name ?? ledger.pondId,
    returnFeeMode: ledger.returnFeeMode,
    catches: [...ledger.catches],
    feesPaid: ledger.feesPaid,
    totalReturnGold,
    totalCatchPlayerXp,
    totalCatchPondXp,
    totalReturnPlayerXp,
    totalReturnPondXp,
    netProfit: totalReturnGold - ledger.feesPaid,
    joinedAt: ledger.joinedAt,
    leftAt,
  };
}
