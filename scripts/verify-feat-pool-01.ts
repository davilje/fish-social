/**
 * FEAT-POOL-01：种池抽种 + 品质权重表 ∩ 种品质带；sizeCap / 公式常数对齐。
 * Run: npm run verify:feat-pool-01
 */
import {
  QUALITY_BITE_BASE,
  QUALITY_SIZE_CAP,
  SIZE_BITE_K,
  BITE_BASE_SCALE,
  type FishQuality,
  calcFishWeightKg,
  calcQualitySizeBiteRate,
  calcSupplementIdealCounts,
  getFishQualityStats,
  getFishingFormulaConstant,
  getGameSpecies,
  getPondCategoryQualityWeights,
  getPondEcologyDef,
  getQualityMaxSize,
  listGamePonds,
  listPondFishPool,
  pickPondSpecies,
  pickSpawnFish,
  qualityRank,
  rollPondQuality,
} from '@fish-social/shared';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

const qualities: FishQuality[] = ['gray', 'green', 'blue', 'purple', 'red', 'orange', 'gold'];
for (const q of qualities) {
  const stats = getFishQualityStats(q);
  assert(stats, `missing fish_quality_stats ${q}`);
  assert(
    Math.abs(stats.sizeCapM - QUALITY_SIZE_CAP[q]) < 1e-9,
    `sizeCap ${q} table=${stats.sizeCapM} code=${QUALITY_SIZE_CAP[q]}`,
  );
  assert(
    Math.abs(stats.biteBaseAtMaxSize - QUALITY_BITE_BASE[q]) < 1e-9,
    `biteBase ${q} table=${stats.biteBaseAtMaxSize} code=${QUALITY_BITE_BASE[q]}`,
  );
  assert(getQualityMaxSize(q) === Math.min(QUALITY_SIZE_CAP[q], 50), `getQualityMaxSize ${q}`);
}

assert(Math.abs(getFishingFormulaConstant('BITE_BASE_SCALE', -1) - 0.05) < 1e-9, 'BITE_BASE_SCALE');
assert(Math.abs(getFishingFormulaConstant('SIZE_BITE_K', -1) - 0.65) < 1e-9, 'SIZE_BITE_K');
assert(Math.abs(getFishingFormulaConstant('ESCAPE_AT_40M', -1) - 0.985) < 1e-9, 'ESCAPE_AT_40M');
assert(Math.abs(getFishingFormulaConstant('LENGTH_WEIGHT_A', -1) - 12) < 1e-9, 'LENGTH_WEIGHT_A');
assert(Math.abs(getFishingFormulaConstant('LENGTH_WEIGHT_B', -1) - 3) < 1e-9, 'LENGTH_WEIGHT_B');
assert(Math.abs(calcFishWeightKg(1) - 12) < 1e-9, 'calcFishWeightKg(1)≈a');

for (const pond of listGamePonds()) {
  const eco = getPondEcologyDef(pond.pondId);
  assert(eco && eco.maxPopulation > 0, `ecology missing ${pond.pondId}`);
  const pool = listPondFishPool(pond.pondId);
  assert(pool.length > 0, `empty pool ${pond.pondId}`);
  for (const row of pool) {
    assert(row.speciesId, `pool row missing speciesId ${pond.pondId}`);
    assert(!(row as { quality?: string }).quality, `pool must not bind quality ${pond.pondId}/${row.speciesId}`);
  }
  const speciesId = pickPondSpecies(pond.pondId);
  assert(speciesId, `pick species failed ${pond.pondId}`);
  const spawned = pickSpawnFish(pond.pondId);
  assert(spawned && spawned.speciesId && spawned.quality, `pickSpawnFish failed ${pond.pondId}`);
  const def = getGameSpecies(spawned.speciesId);
  assert(def, `species def ${spawned.speciesId}`);
  const qMin = Number(def!.qualityMin) || 1;
  const qMax = Number(def!.qualityMax) || 7;
  const rank = qualityRank(spawned.quality);
  assert(
    rank >= qMin && rank <= qMax,
    `spawn quality out of band ${pond.pondId} ${spawned.speciesId} ${spawned.quality}`,
  );
  const q = rollPondQuality(pond.pondId, qMin, qMax);
  assert(qualities.includes(q), `rollPondQuality invalid ${pond.pondId} ${q}`);
  assert(qualityRank(q) >= qMin && qualityRank(q) <= qMax, `roll out of band ${pond.pondId}`);
}

// common 种品质带 1–4：不得高于 purple
for (let i = 0; i < 80; i++) {
  const q = rollPondQuality('pond-calm', 1, 4);
  assert(qualityRank(q) <= 4, `common band leak ${q}`);
}

const categories = ['novice', 'advanced', 'veteran', 'wilderness', 'reservoir', 'forbidden', 'giant'] as const;
for (const cat of categories) {
  const rows = getPondCategoryQualityWeights(cat);
  assert(rows.length > 0, `missing category weights ${cat}`);
}
const noviceIdeal = calcSupplementIdealCounts(100, 'novice');
const advancedIdeal = calcSupplementIdealCounts(100, 'advanced');
assert(noviceIdeal.gold === 0, 'novice gold ideal should be 0');
assert(advancedIdeal.gold > 0, 'advanced gold ideal');
assert(noviceIdeal.gray > advancedIdeal.gray, 'novice gray share higher');

const noviceQs = getPondCategoryQualityWeights('novice').map((r) => r.quality);
assert(noviceQs.every((q) => q === 'gray' || q === 'green'), 'novice qualities gray-green only');

const grayBite = calcQualitySizeBiteRate('gray', 0.15);
const n = 0.15 / QUALITY_SIZE_CAP.gray;
const expected = QUALITY_BITE_BASE.gray * BITE_BASE_SCALE * (1 - SIZE_BITE_K * (1 - n));
assert(Math.abs(grayBite - expected) < 1e-9, `gray bite ${grayBite} vs ${expected}`);

console.log('verify-feat-pool-01: OK');
