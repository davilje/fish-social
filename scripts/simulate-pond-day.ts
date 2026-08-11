/**
 * 鱼塘生态+钓鱼蒙特卡洛模拟（15 分钟步长，默认 7 天连续）
 * v0.4.1：单鱼抽样咬钩 + BASE÷20 + 幼鱼脱钩（D10–D12）
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import {
  FISH_BITE_CHECK_MS,
  POND_ECOSYSTEM_TICK_MS,
} from '@fish-social/shared';
import {
  POND_STOCK_CONFIGS,
  POND_SUPPLEMENT_BATCH_MAX,
  POND_SUPPLEMENT_BATCH_RATIO,
  POND_SUPPLEMENT_TARGET_RATIO,
  FISH_MIGRATION_FRACTION,
  calcSupplementCheckMs,
  pickMigrationSpot,
  pickSpotForNewFish,
  rollSupplementQuality,
  type PondStockConfig,
} from '@fish-social/shared';
import { FISH_QUALITIES, getSpecies, type FishQuality, type FishSpeciesId } from '@fish-social/shared';
import { PONDS } from '@fish-social/shared';
import {
  calcEffectiveEscapeRate,
  calcEscapeGrowthSize,
  calcFishBiteContribution,
  calcQualitySizeBiteRate,
  calcSingleFishBiteProbability,
  getQualityMaxSize,
  growFishSizeV2,
  pickSpotFishCandidate,
  rollIndividualMultiplier,
} from '@fish-social/shared';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = path.join(__dirname, '../docs/analytics/pond-day-simulation');

const STEP_MS = 15 * 60 * 1000;
const SIM_DAYS = 7;
const DAY_STEPS = (24 * 60 * 60 * 1000) / STEP_MS; // 96
const TOTAL_STEPS = DAY_STEPS * SIM_DAYS;
const AVG_SPOT_MULT = 2.5;
const BITE_CHECKS_PER_STEP = STEP_MS / FISH_BITE_CHECK_MS; // 15 @ 1min
const SEED = 42_026;
const SPOTS_PER_POND = 20;

type SimFish = {
  id: string;
  spotId: string;
  quality: FishQuality;
  sizeM: number;
  birthSizeM: number;
  bornAt: number;
  speciesId: FishSpeciesId;
  biteMultiplier: number;
  escapeMultiplier: number;
};

type QualityStats = {
  count: number;
  pct: number;
  avgSizeM: number;
  minSizeM: number;
  maxSizeM: number;
};

type StepSnapshot = {
  step: number;
  label: string;
  total: number;
  avgSizeM: number;
  byQuality: Record<FishQuality, number>;
  caughtThisStep: number;
  supplementedThisStep: number;
};

type DaySnapshot = {
  day: number;
  total: number;
  avgSizeM: number;
  caughtToday: number;
  supplementedToday: number;
  popRatio: number;
};

type PondResult = {
  pondId: string;
  pondName: string;
  maxPopulation: number;
  anglers: number;
  initial: {
    total: number;
    avgSizeM: number;
    byQuality: QualityStats[];
    sizeHistogram: { label: string; count: number }[];
  };
  final: {
    total: number;
    avgSizeM: number;
    byQuality: QualityStats[];
    sizeHistogram: { label: string; count: number }[];
  };
  caught: {
    total: number;
    avgSizeM: number;
    byQuality: QualityStats[];
  };
  daily: DaySnapshot[];
  timeline: StepSnapshot[];
};

// ─── Seeded RNG ─────────────────────────────────────────────────────────────
function mulberry32(seed: number) {
  let s = seed;
  return () => {
    s |= 0;
    s = (s + 0x6d2b79f5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

let rng = mulberry32(SEED);

function rollQuality(): FishQuality {
  const total = FISH_QUALITIES.reduce((s, q) => s + q.weight, 0);
  let r = rng() * total;
  for (const q of FISH_QUALITIES) {
    r -= q.weight;
    if (r <= 0) return q.id;
  }
  return 'gray';
}

function rollJuvenile(quality: FishQuality, speciesId: FishSpeciesId): number {
  const species = getSpecies(speciesId);
  const maxSize = getQualityMaxSize(quality, species);
  const raw = 0.08 + rng() * (0.2 - 0.08);
  return Math.round(Math.max(0.01, Math.min(raw, maxSize * 0.95)) * 100) / 100;
}

function pickSpecies(config: PondStockConfig): FishSpeciesId {
  const pool = rng() < config.rareSpawnRate ? config.rareSpecies : config.commonSpecies;
  return pool[Math.floor(rng() * pool.length)];
}

function rollMultiplier(): number {
  return parseFloat((0.9 + rng() * 0.2).toFixed(4));
}

// ─── Stats helpers ──────────────────────────────────────────────────────────
const SIZE_BUCKETS = [
  { label: '0.08–0.15m', lo: 0, hi: 0.15 },
  { label: '0.15–0.25m', lo: 0.15, hi: 0.25 },
  { label: '0.25–0.40m', lo: 0.25, hi: 0.4 },
  { label: '0.40–0.80m', lo: 0.4, hi: 0.8 },
  { label: '0.80–2.0m', lo: 0.8, hi: 2 },
  { label: '2.0–5.0m', lo: 2, hi: 5 },
  { label: '5.0m+', lo: 5, hi: 999 },
];

function summarizeFish(fish: SimFish[]): {
  total: number;
  avgSizeM: number;
  byQuality: QualityStats[];
  sizeHistogram: { label: string; count: number }[];
} {
  const byQ = Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, [] as number[]])) as Record<
    FishQuality,
    number[]
  >;
  for (const f of fish) byQ[f.quality].push(f.sizeM);

  const total = fish.length;
  const avgSizeM = total ? fish.reduce((s, f) => s + f.sizeM, 0) / total : 0;

  const byQuality: QualityStats[] = FISH_QUALITIES.map((q) => {
    const sizes = byQ[q.id];
    const count = sizes.length;
    return {
      count,
      pct: total ? (count / total) * 100 : 0,
      avgSizeM: count ? sizes.reduce((a, b) => a + b, 0) / count : 0,
      minSizeM: count ? Math.min(...sizes) : 0,
      maxSizeM: count ? Math.max(...sizes) : 0,
    };
  });

  const sizeHistogram = SIZE_BUCKETS.map((b) => ({
    label: b.label,
    count: fish.filter((f) => f.sizeM >= b.lo && f.sizeM < b.hi).length,
  }));

  return { total, avgSizeM: round(avgSizeM), byQuality, sizeHistogram };
}

function round(n: number, d = 3) {
  const f = 10 ** d;
  return Math.round(n * f) / f;
}

function stepLabel(step: number): string {
  const mins = step * 15;
  if (mins === 0) return '0h';
  const h = mins / 60;
  if (h >= 24 && h % 24 === 0) return `D${h / 24}`;
  if (h >= 24) {
    const d = Math.floor(h / 24);
    const rh = h - d * 24;
    return rh === 0 ? `D${d}` : `D${d + 1} ${Number.isInteger(rh) ? rh + 'h' : rh.toFixed(1) + 'h'}`;
  }
  if (mins < 60) return `${mins}m`;
  return Number.isInteger(h) ? `${h}h` : `${h.toFixed(1)}h`;
}

// ─── Ecology ────────────────────────────────────────────────────────────────
let fishIdSeq = 0;

function getSpotIds(pondId: string): string[] {
  const pond = PONDS.find((p) => p.id === pondId);
  return pond?.spots.map((s) => s.id) ?? [];
}

function uniformHabitat(spotIds: string[]): Record<string, number> {
  return Object.fromEntries(spotIds.map((id) => [id, 1]));
}

function createFish(
  pondId: string,
  config: PondStockConfig,
  bornAt: number,
  spotIds: string[],
  habitat: Record<string, number>,
): SimFish {
  const speciesId = pickSpecies(config);
  const quality = rollQuality();
  const sizeM = rollJuvenile(quality, speciesId);
  return {
    id: `${pondId}-${++fishIdSeq}`,
    spotId: pickSpotForNewFish(spotIds, habitat),
    quality,
    speciesId,
    sizeM,
    birthSizeM: sizeM,
    bornAt,
    biteMultiplier: rollMultiplier(),
    escapeMultiplier: rollMultiplier(),
  };
}

function seedPond(config: PondStockConfig, bornAt: number): SimFish[] {
  const spotIds = getSpotIds(config.pondId);
  const habitat = uniformHabitat(spotIds);
  const fish: SimFish[] = [];
  for (let i = 0; i < config.initialPopulation; i++) {
    fish.push(createFish(config.pondId, config, bornAt, spotIds, habitat));
  }
  return fish;
}

function growFish(fish: SimFish[], now: number) {
  for (const f of fish) {
    const species = getSpecies(f.speciesId);
    f.sizeM = growFishSizeV2(f.quality, species, f.sizeM, f.birthSizeM, f.bornAt, now);
    const cap = getQualityMaxSize(f.quality, species);
    if (f.sizeM > cap) f.sizeM = cap;
  }
}

function countByQuality(fish: SimFish[]): Record<FishQuality, number> {
  const counts = Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0])) as Record<
    FishQuality,
    number
  >;
  for (const f of fish) counts[f.quality] += 1;
  return counts;
}

function createSupplementFish(
  pondId: string,
  config: PondStockConfig,
  bornAt: number,
  actualByQuality: Record<FishQuality, number>,
  spotIds: string[],
  habitat: Record<string, number>,
): SimFish {
  const speciesId = pickSpecies(config);
  const quality = rollSupplementQuality(actualByQuality, config.maxPopulation);
  const sizeM = rollJuvenile(quality, speciesId);
  return {
    id: `${pondId}-${++fishIdSeq}`,
    spotId: pickSpotForNewFish(spotIds, habitat),
    quality,
    speciesId,
    sizeM,
    birthSizeM: sizeM,
    bornAt,
    biteMultiplier: rollMultiplier(),
    escapeMultiplier: rollMultiplier(),
  };
}

function trySupplement(
  fish: SimFish[],
  config: PondStockConfig,
  now: number,
  activeAnglers: number,
  lastSupplementAt: number,
  force = false,
): { added: number; lastSupplementAt: number } {
  const effectiveMs = calcSupplementCheckMs(activeAnglers);
  if (!force && now - lastSupplementAt < effectiveMs) {
    return { added: 0, lastSupplementAt };
  }

  const target = Math.floor(config.maxPopulation * POND_SUPPLEMENT_TARGET_RATIO);
  const gap = Math.max(0, target - fish.length);
  if (gap === 0) return { added: 0, lastSupplementAt };

  const n = Math.min(
    POND_SUPPLEMENT_BATCH_MAX,
    Math.max(1, Math.ceil(gap * POND_SUPPLEMENT_BATCH_RATIO)),
  );
  const actualByQuality = countByQuality(fish);
  for (let i = 0; i < n; i++) {
    const spotIds = getSpotIds(config.pondId);
    const habitat = uniformHabitat(spotIds);
    const added = createSupplementFish(config.pondId, config, now, actualByQuality, spotIds, habitat);
    fish.push(added);
    actualByQuality[added.quality] += 1;
  }
  return { added: n, lastSupplementAt: now };
}

function migrateFishSpots(fish: SimFish[], pondId: string) {
  const spotIds = getSpotIds(pondId);
  if (spotIds.length === 0) return;
  const habitat = uniformHabitat(spotIds);
  for (const f of fish) {
    if (rng() > FISH_MIGRATION_FRACTION) continue;
    f.spotId = pickMigrationSpot(spotIds, habitat);
  }
}

function simulateFishing(fish: SimFish[], pondId: string, anglers: number, caught: SimFish[]) {
  if (anglers <= 0 || fish.length === 0) return;

  const spotIds = getSpotIds(pondId);
  const anglerSpots = Array.from({ length: anglers }, () => spotIds[Math.floor(rng() * spotIds.length)]!);

  for (let a = 0; a < anglers; a++) {
    const spotId = anglerSpots[a]!;
    for (let c = 0; c < BITE_CHECKS_PER_STEP; c++) {
      const candidates = fish.filter((f) => f.spotId === spotId);
      if (candidates.length === 0) continue;

      const target = pickSpotFishCandidate(candidates as import('@fish-social/shared').PondFishEntity[]);
      const simTarget = candidates.find((f) => f.id === target.id);
      if (!simTarget) continue;

      const pBite = calcSingleFishBiteProbability(
        simTarget as import('@fish-social/shared').PondFishEntity,
        AVG_SPOT_MULT,
        0,
      );
      if (rng() >= pBite) continue;

      const esc = calcEffectiveEscapeRate(simTarget.sizeM, 'basic', simTarget.escapeMultiplier);
      if (rng() < esc) {
        const species = getSpecies(simTarget.speciesId);
        simTarget.sizeM = calcEscapeGrowthSize(simTarget.quality, species, simTarget.sizeM);
        continue;
      }

      const idx = fish.indexOf(simTarget);
      if (idx >= 0) {
        caught.push({ ...simTarget });
        fish.splice(idx, 1);
      }
    }
  }
}

function simulatePond(config: PondStockConfig, pondName: string, anglers: number): PondResult {
  const startAt = 0;
  const fish = seedPond(config, startAt);
  const initialFish = fish.map((f) => ({ ...f }));
  const caught: SimFish[] = [];
  const timeline: StepSnapshot[] = [];
  const daily: DaySnapshot[] = [];
  let caughtAtDayStart = 0;
  let totalSupplemented = 0;
  let suppAtDayStart = 0;

  const snap = (step: number, fishList: SimFish[], caughtStep: number, suppStep: number) => {
    const byQ = Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0])) as Record<FishQuality, number>;
    for (const f of fishList) byQ[f.quality]++;
    timeline.push({
      step,
      label: stepLabel(step),
      total: fishList.length,
      avgSizeM: fishList.length ? round(fishList.reduce((s, f) => s + f.sizeM, 0) / fishList.length) : 0,
      byQuality: byQ,
      caughtThisStep: caughtStep,
      supplementedThisStep: suppStep,
    });
  };

  snap(0, fish, 0, 0);

  let lastSupplementAt = 0;
  for (let step = 1; step <= TOTAL_STEPS; step++) {
    const now = step * STEP_MS;
    growFish(fish, now);
    const suppResult = trySupplement(fish, config, now, anglers, lastSupplementAt);
    lastSupplementAt = suppResult.lastSupplementAt;
    totalSupplemented += suppResult.added;
    migrateFishSpots(fish, config.pondId);
    const caughtBefore = caught.length;
    simulateFishing(fish, config.pondId, anglers, caught);
    const caughtStep = caught.length - caughtBefore;
    snap(step, fish, caughtStep, suppResult.added);

    if (step % DAY_STEPS === 0) {
      const day = step / DAY_STEPS;
      daily.push({
        day,
        total: fish.length,
        avgSizeM: fish.length ? round(fish.reduce((s, f) => s + f.sizeM, 0) / fish.length) : 0,
        caughtToday: caught.length - caughtAtDayStart,
        supplementedToday: totalSupplemented - suppAtDayStart,
        popRatio: round((fish.length / config.maxPopulation) * 100, 1),
      });
      caughtAtDayStart = caught.length;
      suppAtDayStart = totalSupplemented;
    }
  }

  const caughtSummary = summarizeFish(caught);
  return {
    pondId: config.pondId,
    pondName,
    maxPopulation: config.maxPopulation,
    anglers,
    initial: summarizeFish(initialFish),
    final: summarizeFish(fish),
    caught: {
      total: caughtSummary.total,
      avgSizeM: caughtSummary.avgSizeM,
      byQuality: caughtSummary.byQuality,
    },
    daily,
    timeline,
  };
}

function summarizeCaught(caught: SimFish[]) {
  return summarizeFish(caught);
}

// ─── Main ───────────────────────────────────────────────────────────────────
const ANGLER_SCENARIOS = [0, 1, 3, 5, 10, 20];

const meta = {
  generatedAt: new Date().toISOString(),
  seed: SEED,
  stepMinutes: 15,
  simDays: SIM_DAYS,
  daySteps: DAY_STEPS,
  totalSteps: TOTAL_STEPS,
  rules: {
    rulesVersion: 'v0.4.1',
    FISH_BITE_CHECK_MS,
    POND_ECOSYSTEM_TICK_MS,
    supplementEveryMin: 15,
    dynamicSupplement: true,
    qualityGapSupplement: true,
    spotLocalBite: true,
    singleFishBite: true,
    biteBaseScale: 0.05,
    juvenileEscapeCurve: true,
    spotsPerPond: SPOTS_PER_POND,
    fishMigrationFraction: FISH_MIGRATION_FRACTION,
    POND_SUPPLEMENT_TARGET_RATIO,
    POND_SUPPLEMENT_BATCH_RATIO,
    POND_SUPPLEMENT_BATCH_MAX,
    noBreeding: true,
    growthCurve: 'L(t) 7d→40m',
    escapeHook: 'size-only curve',
    escapeGrowthOnMiss: '×1.02',
    juvenileSizeM: '0.08–0.20',
    avgSpotMultiplier: AVG_SPOT_MULT,
    baitBonus: 0,
    tackle: 'basic',
  },
  anglerScenarios: ANGLER_SCENARIOS,
};

const results: PondResult[] = [];

for (const anglers of ANGLER_SCENARIOS) {
  rng = mulberry32(SEED + anglers * 1000);
  fishIdSeq = 0;
  for (const config of POND_STOCK_CONFIGS) {
    const pond = PONDS.find((p) => p.id === config.pondId);
    results.push(simulatePond(config, pond?.name ?? config.pondId, anglers));
  }
}

fs.mkdirSync(OUT_DIR, { recursive: true });
const outPath = path.join(OUT_DIR, 'data.json');
fs.writeFileSync(outPath, JSON.stringify({ meta, results }, null, 2));

console.log(`Wrote ${outPath} (${SIM_DAYS} days × ${DAY_STEPS} steps/day)`);
for (const anglers of ANGLER_SCENARIOS) {
  console.log(`\n=== ${anglers} 人/塘 (${SIM_DAYS}d) ===`);
  for (const r of results.filter((x) => x.anglers === anglers)) {
    const lastDay = r.daily[r.daily.length - 1];
    console.log(
      `${r.pondName}: ${r.initial.total}→${r.final.total} fish | caught ${r.caught.total} (${lastDay?.caughtToday ?? 0}/d last) avg ${r.caught.avgSizeM}m`,
    );
  }
}
