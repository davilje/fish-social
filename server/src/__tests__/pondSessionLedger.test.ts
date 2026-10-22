import { afterEach, describe, expect, it, vi } from 'vitest';
import { calcFishWeightKg } from '@fish-social/shared';
import {
  buildPondSessionSummary, clearPondSessionLedger, ensurePondSessionLedger,
  recordSessionBite, recordSessionCatch, recordSessionEscape,
  recordSessionFeePaid, recordSessionFishingDuration,
} from '../pondSessionLedger.js';

const player = 'workspace-settlement-test';
afterEach(() => { clearPondSessionLedger(player); vi.restoreAllMocks(); });

describe('pond journal settlement', () => {
  it('counts genuine hook and escape events even with no successful catches', () => {
    ensurePondSessionLedger(player, 'test-pond', 'sell_only');
    recordSessionBite(player); recordSessionBite(player); recordSessionEscape(player);
    const summary = buildPondSessionSummary(player)!;
    expect(summary.biteCount).toBe(2); expect(summary.escapeCount).toBe(1);
    expect(summary.catches).toEqual([]);
  });

  it('adds only credited fishing segments, excluding idle wall time, and preserves totals across ensure/reconnect', () => {
    const now = vi.spyOn(Date, 'now').mockReturnValue(1000);
    ensurePondSessionLedger(player, 'test-pond', 'sell_only');
    recordSessionFishingDuration(player, 30000);
    now.mockReturnValue(3601000);
    ensurePondSessionLedger(player, 'test-pond', 'sell_only');
    recordSessionFishingDuration(player, 10000);
    recordSessionFishingDuration(player, 0); recordSessionFishingDuration(player, -1); recordSessionFishingDuration(player, NaN);
    const first = buildPondSessionSummary(player)!;
    expect(first.fishingDurationMs).toBe(40000);
    expect(first.leftAt - first.joinedAt).toBe(3600000);
    expect(buildPondSessionSummary(player)!.fishingDurationMs).toBe(40000);
  });

  it('uses authoritative weight formula and preserves inventory link only when supplied', () => {
    ensurePondSessionLedger(player, 'test-pond', 'auto_return');
    recordSessionFeePaid(player, 200);
    recordSessionCatch(player, { speciesId:'crucian', quality:'blue', sizeM:.8, outcome:'kept', inventoryItemId:'fish-1', caughtAt:1234 });
    recordSessionCatch(player, { speciesId:'koi', quality:'purple', sizeM:1.1, outcome:'returned', returnGold:600, catchPlayerXp:20, caughtAt:2345 });
    const summary = buildPondSessionSummary(player)!;
    expect(summary.catches[0].weightKg).toBe(calcFishWeightKg(.8));
    expect(summary.catches[0].inventoryItemId).toBe('fish-1');
    expect(summary.catches[1].inventoryItemId).toBeUndefined();
    expect(summary.netProfit).toBe(400); expect(summary.totalCatchPlayerXp).toBe(20);
  });

  it('clears all totals between pond visits and ignores events without an active ledger', () => {
    recordSessionBite(player); recordSessionEscape(player); recordSessionFishingDuration(player, 9000);
    expect(buildPondSessionSummary(player)).toBeNull();
    ensurePondSessionLedger(player, 'test-pond', 'sell_only'); recordSessionBite(player);
    clearPondSessionLedger(player); ensurePondSessionLedger(player, 'other-pond', 'sell_only');
    expect(buildPondSessionSummary(player)).toMatchObject({ pondId:'other-pond', biteCount:0, escapeCount:0, fishingDurationMs:0 });
  });
});
