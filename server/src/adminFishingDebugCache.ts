/**
 * QUAL-02/D5: fishing-debug report cache with max entries (LRU-ish by insert order).
 */
import type { buildFishingDebugReport } from './fishingDebug.js';

export type FishingDebugReport = ReturnType<typeof buildFishingDebugReport>;

const FISHING_DEBUG_CACHE_MS = 3000;
const FISHING_DEBUG_CACHE_MAX = Number(process.env.FISHING_DEBUG_CACHE_MAX ?? 64);

const fishingDebugCache = new Map<string, { at: number; data: FishingDebugReport }>();

export function getCachedFishingDebug(cacheKey: string): FishingDebugReport | null {
  const cached = fishingDebugCache.get(cacheKey);
  if (cached && Date.now() - cached.at < FISHING_DEBUG_CACHE_MS) {
    return cached.data;
  }
  return null;
}

export function setCachedFishingDebug(cacheKey: string, data: FishingDebugReport): void {
  if (fishingDebugCache.has(cacheKey)) {
    fishingDebugCache.delete(cacheKey);
  }
  fishingDebugCache.set(cacheKey, { at: Date.now(), data });
  while (fishingDebugCache.size > FISHING_DEBUG_CACHE_MAX) {
    const oldest = fishingDebugCache.keys().next().value;
    if (oldest === undefined) break;
    fishingDebugCache.delete(oldest);
  }
}

export function clearFishingDebugCache(): void {
  fishingDebugCache.clear();
}

export function getFishingDebugCacheSize(): number {
  return fishingDebugCache.size;
}
