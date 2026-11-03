import { describe, expect, it } from 'vitest';
import { formatFishHistorySummary, formatFishHistoryTime, formatFishResidence, type FishHistorySnapshot } from '@fish-social/shared';

describe('capture history presentation', () => {
  const history: FishHistorySnapshot = {
    fishId: 'individual-1', enteredAt: Date.UTC(2026, 10, 1), entrySizeM: .042,
    caughtAt: Date.UTC(2026, 10, 3, 6), residenceMs: 54 * 3600_000,
    growthSinceEntryM: .143, individualCapM: .2, growthRuleVersion: 'pond-entry-v1',
    source: 'pre_opening_seed', hookFeedCount: 2, groundbaitFeedCount: 6,
    escapeCount: 1, escapes: [{ at: Date.UTC(2026, 10, 2), pondId: 'pond-calm', spotId: 'spot-1', sizeM: .16 }],
  };

  it('uses frozen duration and actual entry length instead of birth or current time', () => {
    const before = JSON.stringify(history);
    expect(formatFishHistorySummary(history)).toBe('在塘2天6小时 · 入塘4.2cm · 长大14.3cm');
    expect(JSON.stringify(history)).toBe(before);
    expect(formatFishHistorySummary({ ...history, residenceMs: 0, entrySizeM: .12, growthSinceEntryM: 0 }))
      .toBe('在塘不足1分钟 · 入塘12.0cm · 长大0.0cm');
  });

  it('does not invent entry history for legacy captures', () => {
    expect(formatFishHistorySummary()).toBe('这条鱼的入塘履历未记录');
    expect(formatFishHistoryTime(0)).toBe('时间未记录');
  });

  it('uses the Shanghai calendar and bounded nonnegative durations', () => {
    expect(formatFishHistoryTime(Date.UTC(2026, 10, 1, 20, 5))).toBe('2026/11/02 04:05');
    expect(formatFishResidence(0)).toBe('不足1分钟');
    expect(formatFishResidence(-1)).toBe('不足1分钟');
    expect(formatFishResidence(61 * 60_000)).toBe('1小时1分钟');
  });
});
