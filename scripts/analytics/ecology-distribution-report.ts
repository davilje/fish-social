/**
 * Ecology distribution analysis: pond quality MC, live snapshot, species rarity, size/weight matrix.
 * Run: npm run build:shared && npx tsx scripts/analytics/ecology-distribution-report.ts
 */
import fs from 'node:fs';
import path from 'node:path';
import {
  calcFishWeightJin,
  calcFishWeightKg,
  FISH_QUALITIES,
  getQualityMaxSize,
  JUVENILE_SIZE_M_MAX,
  JUVENILE_SIZE_M_MIN,
  type FishQuality,
} from '@fish-social/shared';
import {
  getGamePondDef,
  listGamePonds,
  listGameSpecies,
  listPondFishPool,
  pickSpawnFish,
} from '@fish-social/shared';
import pondCategoryQualityWeights from '../../shared/generated/game-data/pond_category_quality_weights.json';

const MC_SAMPLES = 10_000;
const QUALITIES = FISH_QUALITIES.map((q) => q.id);
const SNAPSHOT_PATH = path.join(
  process.cwd(),
  'docs/analytics/daily/2026-08-26/ecology-snapshot.json',
);

type QualityCounts = Record<FishQuality, number>;

function emptyCounts(): QualityCounts {
  return Object.fromEntries(QUALITIES.map((q) => [q, 0])) as QualityCounts;
}

function pct(n: number, total: number): string {
  if (total <= 0) return '0.0%';
  return `${((n / total) * 100).toFixed(1)}%`;
}

function categoryTheoryPct(category: string): Record<FishQuality, string> {
  const rows = (pondCategoryQualityWeights as Array<{ pondCategory: string; quality: FishQuality; spawnWeight: number }>).filter(
    (r) => r.pondCategory === category,
  );
  const sum = rows.reduce((s, r) => s + r.spawnWeight, 0) || 1;
  const out = emptyCounts();
  for (const r of rows) out[r.quality] = (r.spawnWeight / sum) * 100;
  return Object.fromEntries(QUALITIES.map((q) => [q, `${out[q].toFixed(1)}%`])) as Record<FishQuality, string>;
}

function mcPondQuality(pondId: string): Record<FishQuality, string> {
  const counts = emptyCounts();
  let ok = 0;
  for (let i = 0; i < MC_SAMPLES; i++) {
    const fish = pickSpawnFish(pondId);
    if (!fish) continue;
    counts[fish.quality]++;
    ok++;
  }
  return Object.fromEntries(QUALITIES.map((q) => [q, pct(counts[q], ok)])) as Record<FishQuality, string>;
}

function snapshotQuality(byQuality: Partial<Record<FishQuality, number>>, population: number): Record<FishQuality, string> {
  const counts = emptyCounts();
  for (const q of QUALITIES) counts[q] = byQuality[q] ?? 0;
  return Object.fromEntries(QUALITIES.map((q) => [q, pct(counts[q], population)])) as Record<FishQuality, string>;
}

function fmtWeight(sizeM: number): { kg: number; jin: number } {
  return { kg: calcFishWeightKg(sizeM), jin: calcFishWeightJin(sizeM) };
}

function solveSizeForJin(targetJin: number): number {
  let lo = 0.03;
  let hi = 50;
  for (let i = 0; i < 40; i++) {
    const mid = (lo + hi) / 2;
    if (calcFishWeightJin(mid) > targetJin) hi = mid;
    else lo = mid;
  }
  return Math.round(((lo + hi) / 2) * 1000) / 1000;
}

// --- 1. Pond quality ---
const ponds = listGamePonds().filter((p) => p.showOnWorldMap !== false || p.pondId === 'pond-novice');
const pondMc: Array<Record<string, unknown>> = [];
const pondLive: Array<Record<string, unknown>> = [];

for (const pond of ponds) {
  const mc = mcPondQuality(pond.pondId);
  const theory = categoryTheoryPct(pond.pondCategory);
  pondMc.push({
    pondId: pond.pondId,
    name: pond.name,
    category: pond.pondCategory,
    initialPop: pond.initialPopulation,
    maxPop: pond.maxPopulation,
    theory,
    mc,
  });
}

if (fs.existsSync(SNAPSHOT_PATH)) {
  const snap = JSON.parse(fs.readFileSync(SNAPSHOT_PATH, 'utf8')) as {
    ponds: Array<{
      pondId: string;
      population: number;
      maxPopulation: number;
      byQuality: Partial<Record<FishQuality, number>>;
      avgSizeM: number;
    }>;
  };
  for (const row of snap.ponds) {
    const def = getGamePondDef(row.pondId);
    pondLive.push({
      pondId: row.pondId,
      category: def?.pondCategory ?? '—',
      population: row.population,
      maxPopulation: row.maxPopulation,
      popPct: pct(row.population, row.maxPopulation),
      live: snapshotQuality(row.byQuality, row.population),
      avgSizeM: row.avgSizeM,
    });
  }
}

// --- 2. Species rarity ---
const species = listGameSpecies();
const rarityOrder = ['common', 'uncommon', 'rare', 'legendary'] as const;
const rarityCounts: Record<string, number> = {};
for (const s of species) rarityCounts[s.rarityTier] = (rarityCounts[s.rarityTier] ?? 0) + 1;

const poolRows = ponds.flatMap((p) => listPondFishPool(p.pondId).filter((r) => r.enabled !== false));
const speciesWeight = new Map<string, number>();
const rarityWeight = new Map<string, number>();
const speciesById = new Map(species.map((s) => [s.speciesId, s]));

for (const row of poolRows) {
  const w = row.spawnWeight ?? 0;
  speciesWeight.set(row.speciesId, (speciesWeight.get(row.speciesId) ?? 0) + w);
  const tier = speciesById.get(row.speciesId)?.rarityTier ?? 'unknown';
  rarityWeight.set(tier, (rarityWeight.get(tier) ?? 0) + w);
}
const totalPoolWeight = [...speciesWeight.values()].reduce((a, b) => a + b, 0);

const speciesList = species.map((s) => ({
  speciesId: s.speciesId,
  name: s.name,
  rarityTier: s.rarityTier,
  qualityMax: s.qualityMax,
  qualityMaxName: FISH_QUALITIES[s.qualityMax - 1]?.name ?? '—',
  spawnWeightTotal: speciesWeight.get(s.speciesId) ?? 0,
  spawnWeightPct: totalPoolWeight > 0 ? ((speciesWeight.get(s.speciesId) ?? 0) / totalPoolWeight) * 100 : 0,
}));

const raritySummary = rarityOrder.map((tier) => ({
  tier,
  speciesCount: rarityCounts[tier] ?? 0,
  speciesPct: pct(rarityCounts[tier] ?? 0, species.length),
  spawnWeight: rarityWeight.get(tier) ?? 0,
  spawnWeightPct: totalPoolWeight > 0 ? pct(rarityWeight.get(tier) ?? 0, totalPoolWeight) : '0%',
}));

// --- 3. Size/weight matrix ---
const qualityBaseline = QUALITIES.map((q) => {
  const maxM = getQualityMaxSize(q);
  const initLo = JUVENILE_SIZE_M_MIN;
  const initHi = Math.min(JUVENILE_SIZE_M_MAX, maxM * 0.95);
  const maxW = fmtWeight(maxM);
  const initLoW = fmtWeight(initLo);
  const initHiW = fmtWeight(initHi);
  return {
    quality: q,
    displayName: FISH_QUALITIES.find((x) => x.id === q)?.name ?? q,
    maxSizeM: maxM,
    maxKg: maxW.kg,
    maxJin: maxW.jin,
    initSizeRangeM: `${initLo.toFixed(2)}–${initHi.toFixed(2)}`,
    initKgRange: `${initLoW.kg.toFixed(3)}–${initHiW.kg.toFixed(3)}`,
    initJinRange: `${initLoW.jin.toFixed(2)}–${initHiW.jin.toFixed(2)}`,
  };
});

const speciesQualityMatrix: Array<Record<string, unknown>> = [];
for (const s of species) {
  for (let rank = s.qualityMin; rank <= s.qualityMax; rank++) {
    const q = FISH_QUALITIES[rank - 1]?.id as FishQuality;
    if (!q) continue;
    const maxM = getQualityMaxSize(q);
    const initLo = JUVENILE_SIZE_M_MIN;
    const initHi = Math.min(JUVENILE_SIZE_M_MAX, maxM * 0.95);
    const maxW = fmtWeight(maxM);
    const initLoW = fmtWeight(initLo);
    const initHiW = fmtWeight(initHi);
    speciesQualityMatrix.push({
      speciesId: s.speciesId,
      name: s.name,
      rarityTier: s.rarityTier,
      quality: q,
      qualityRank: rank,
      maxSizeM: maxM,
      maxJin: maxW.jin,
      initSizeRangeM: `${initLo.toFixed(2)}–${initHi.toFixed(2)}`,
      initJinRange: `${initLoW.jin.toFixed(2)}–${initHiW.jin.toFixed(2)}`,
      returnEligibleAtMax: maxW.jin > 10,
      returnHeavyAtMax: maxW.jin > 100,
    });
  }
}

// Return fish thresholds
const returnThresholds = {
  minJin10: { sizeM: solveSizeForJin(10), note: '>10 斤可回' },
  minJin100: { sizeM: solveSizeForJin(100), note: '>100 斤 ×3' },
};

const OUT_PATH = path.join(process.cwd(), 'docs/analytics/ecology-distribution-report.json');

const report = {
      meta: {
        mcSamples: MC_SAMPLES,
        pondCount: ponds.length,
        speciesCount: species.length,
        snapshotDate: '2026-08-26',
        snapshotPath: SNAPSHOT_PATH,
        totalPoolWeight,
        matrixRows: speciesQualityMatrix.length,
      },
      pondQualityMc: pondMc,
      pondQualityLive: pondLive,
      raritySummary,
      speciesList,
      qualityBaseline,
      speciesQualityMatrix,
  returnThresholds,
};

fs.writeFileSync(OUT_PATH, JSON.stringify(report, null, 2), 'utf8');
console.log(`Written: ${OUT_PATH}`);
