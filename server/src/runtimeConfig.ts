import {
  BITE_LAMBDA,
  FISH_BITE_CHECK_MS,
  FISH_SPECIES,
  type FishQuality,
} from '@fish-social/shared';
import { loadGameConfigFromDb } from './gameConfig.js';
import { isPlayerInGrayCohort } from './grayRelease.js';

/** 运行时钓鱼数值（A2 只读；C1 热更） */
const numericOverrides = new Map<string, number>();

let lastRefreshAt = 0;

export function getRuntimeNumber(key: string, fallback: number): number {
  return numericOverrides.get(key) ?? fallback;
}

export function setRuntimeNumber(key: string, value: number): void {
  numericOverrides.set(key, value);
}

export function clearRuntimeOverrides(): void {
  numericOverrides.clear();
}

export function getBiteLambda(): number {
  return getRuntimeNumber('BITE_LAMBDA', BITE_LAMBDA);
}

export function getBiteCheckMs(): number {
  const val = getRuntimeNumber('FISH_BITE_CHECK_MS', FISH_BITE_CHECK_MS);
  if (!Number.isFinite(val) || val < FISH_BITE_CHECK_MS) {
    return FISH_BITE_CHECK_MS;
  }
  return val;
}

export function getHookDurationScale(): number {
  const val = getRuntimeNumber('HOOK_DURATION_SCALE', 1);
  return Number.isFinite(val) && val > 0 ? val : 1;
}

export function getGrayReleasePercent(): number {
  const val = getRuntimeNumber('GRAY_RELEASE_PERCENT', 100);
  if (!Number.isFinite(val)) return 100;
  return Math.min(100, Math.max(0, val));
}

export function isPlayerInGrayRelease(playerId: string): boolean {
  return isPlayerInGrayCohort(playerId, getGrayReleasePercent());
}

/** 按运行时配置动态调度间隔（每次 tick 后重新读取 DB 覆盖值） */
export function scheduleRuntimeInterval(
  fn: () => void,
  getIntervalMs: () => number,
): () => void {
  let cancelled = false;
  let timer: NodeJS.Timeout | null = null;
  const tick = () => {
    if (cancelled) return;
    fn();
    timer = setTimeout(tick, Math.max(1000, getIntervalMs()));
  };
  tick();
  return () => {
    cancelled = true;
    if (timer) clearTimeout(timer);
  };
}

/** @deprecated v0.3.0 已合并进 QUALITY_ESCAPE_BASE */
export function getQualityEscapeBonus(quality: FishQuality): number {
  const key = `QUALITY_ESCAPE_BONUS_${quality.toUpperCase()}`;
  return getRuntimeNumber(key, 0);
}

/** @deprecated v0.3.0 咬钩改由 calcQualitySizeBiteRate */
export function getSpeciesBiteWeight(speciesId: string, fallback: number): number {
  const key = `SPECIES_BITE_WEIGHT_${speciesId}`;
  return getRuntimeNumber(key, fallback);
}

export function getSpeciesEscapeRate(speciesId: string, fallback: number): number {
  const key = `SPECIES_ESCAPE_RATE_${speciesId}`;
  return getRuntimeNumber(key, fallback);
}

export function refreshRuntimeFromDb(): void {
  const dbMap = loadGameConfigFromDb();
  clearRuntimeOverrides();
  for (const [key, raw] of dbMap) {
    const n = Number(raw);
    if (Number.isFinite(n)) numericOverrides.set(key, n);
  }
  lastRefreshAt = Date.now();
}

export function applyRuntimeConfigFromDb(): void {
  refreshRuntimeFromDb();
}

export function getRuntimeConfigMeta(): { lastRefreshAt: number; overrideCount: number } {
  return { lastRefreshAt, overrideCount: numericOverrides.size };
}

/** 供 debug 展示物种运行时参数 */
export function getAllSpeciesRuntimeKeys(): string[] {
  return FISH_SPECIES.map((s) => `SPECIES_ESCAPE_RATE_${s.id}`);
}
