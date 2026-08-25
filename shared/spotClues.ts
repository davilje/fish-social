import type { SpotActivitySignal, SpotClueTextDef } from './gameDataTypes';

export type { SpotActivitySignal, SpotClueTextDef };

export interface SpotClueFilterOpts {
  playerLevel: number;
  pondLevel: number;
  pondCategory: string;
  spotTags: string[];
  /** v2: match spot ecology tier; v1 omit for uniform random among tag pool */
  activityTier?: SpotActivitySignal;
}

const FORBIDDEN_HABITAT = /会有|爱往|爱待|爱贴|爱在这里|宜钓|难有大货|鲫鱼|鲤鱼|草鱼|鳊|鲶|杂食鱼|板鲫|草青|鱼道|通勤|觅食|开口|钓凹|钓凸|可能性|谨慎|歇脚|路过|主场/;
const FORBIDDEN_ACTIVITY = /鱼星|鱼花|鱼群|开口|白费|没货|并不空|值得守|活性上来|当心|别当|多半|往往|可能性|进窝|鱼情|炸窝|逃窜|受惊|落饵|拱泥|拱食|鲫鱼|鲤鱼|草鱼|鳊|杂鱼|闹窝|缺氧|硬钓|说明有|像活鱼|老钓友/;

export function parseSpotTags(tags: string | undefined | null): string[] {
  if (!tags?.trim()) return [];
  return tags
    .split(',')
    .map((t) => t.trim())
    .filter(Boolean);
}

export function filterSpotCluePool(
  rows: SpotClueTextDef[],
  opts: SpotClueFilterOpts,
): SpotClueTextDef[] {
  const tagSet = new Set(opts.spotTags);
  return rows.filter((row) => {
    if (row.enabled === false) return false;
    if (!row.clueText?.trim()) return false;
    if (opts.playerLevel < Math.max(0, row.minPlayerLevel ?? 0)) return false;
    if (opts.pondLevel < Math.max(0, row.minPondLevel ?? 0)) return false;
    if (row.pondCategory && row.pondCategory !== opts.pondCategory) return false;
    const tag = row.spotTag?.trim();
    if (!tag || !tagSet.has(tag)) return false;
    if (opts.activityTier && row.clueType === 'activity') {
      if (row.activitySignal !== opts.activityTier) return false;
    }
    return true;
  });
}

export function weightedPickSpotClue(
  pool: SpotClueTextDef[],
  roll: number,
): SpotClueTextDef | undefined {
  if (pool.length === 0) return undefined;
  let total = 0;
  for (const row of pool) total += Math.max(1, row.weight ?? 1);
  let r = ((roll % total) + total) % total;
  for (const row of pool) {
    r -= Math.max(1, row.weight ?? 1);
    if (r < 0) return row;
  }
  return pool[pool.length - 1];
}

/** v1: 50/50 habitat vs activity among tag-matched pool */
export function pickSpotClueFromPool(
  pool: SpotClueTextDef[],
  roll: number,
  preferType?: 'habitat' | 'activity',
): SpotClueTextDef | undefined {
  if (pool.length === 0) return undefined;
  let candidates = pool;
  if (preferType) {
    const typed = pool.filter((r) => r.clueType === preferType);
    if (typed.length > 0) candidates = typed;
  } else {
    const useHabitat = roll % 2 === 0;
    const typed = pool.filter((r) => r.clueType === (useHabitat ? 'habitat' : 'activity'));
    if (typed.length > 0) candidates = typed;
  }
  return weightedPickSpotClue(candidates, roll);
}

export function validateSpotClueWording(row: SpotClueTextDef): string[] {
  const issues: string[] = [];
  const text = row.clueText ?? '';
  if (row.clueType === 'habitat' && FORBIDDEN_HABITAT.test(text)) {
    issues.push(`${row.clueId}: habitat mentions fish/preference`);
  }
  if (row.clueType === 'activity' && FORBIDDEN_ACTIVITY.test(text)) {
    issues.push(`${row.clueId}: activity mentions fish/conclusion`);
  }
  return issues;
}
