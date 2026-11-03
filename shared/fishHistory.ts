import { formatFishSize } from './fish';

export interface FishEscapeRecord {
  at: number;
  pondId: string;
  spotId: string;
  sizeM: number;
}

/** A capture-time snapshot. Never recompute residence from the viewer's clock. */
export interface FishHistorySnapshot {
  fishId: string;
  enteredAt: number;
  entrySizeM: number;
  caughtAt: number;
  residenceMs: number;
  growthSinceEntryM: number;
  individualCapM: number;
  growthRuleVersion: string;
  source: string;
  hookFeedCount: number;
  groundbaitFeedCount: number;
  escapeCount: number;
  escapes: FishEscapeRecord[];
}

export function formatFishResidence(residenceMs: number): string {
  const minutes = Math.floor(Math.max(0, Number.isFinite(residenceMs) ? residenceMs : 0) / 60_000);
  if (minutes < 1) return '不足1分钟';
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  if (days > 0) return `${days}天${hours}小时`;
  if (hours > 0) return `${hours}小时${minutes % 60}分钟`;
  return `${minutes}分钟`;
}

/** Game-world calendar follows the same Shanghai clock as the ecology settlement. */
export function formatFishHistoryTime(at: number): string {
  if (!Number.isFinite(at) || at <= 0) return '时间未记录';
  const date = new Date(at + 8 * 60 * 60 * 1000);
  if (!Number.isFinite(date.getTime())) return '时间未记录';
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getUTCFullYear()}/${pad(date.getUTCMonth() + 1)}/${pad(date.getUTCDate())} ${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`;
}

export function formatFishHistorySummary(history?: FishHistorySnapshot | null): string {
  if (!history) return '这条鱼的入塘履历未记录';
  return `在塘${formatFishResidence(history.residenceMs)} · 入塘${formatFishSize(history.entrySizeM)} · 长大${formatFishSize(history.growthSinceEntryM)}`;
}
