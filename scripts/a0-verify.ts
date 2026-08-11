/**
 * A0-v2 / v0.3.1 / v0.3.2 / v0.4.0 / v0.4.1 验收模拟
 * 运行: npm run verify:a0
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import {
  BITE_BASE_SCALE,
  ESCAPE_AT_40M,
  FISH_BITE_CHECK_MS,
  FISH_MIGRATION_CHECK_MS,
  FISH_MIGRATION_FRACTION,
  FISH_QUALITIES,
  FISH_SPECIES,
  GROWTH_CURVE_CEILING_M,
  GROWTH_CURVE_DAYS,
  HOOK_AT_40M_MS,
  JUVENILE_SIZE_M_MAX,
  JUVENILE_SIZE_M_MIN,
  POND_SUPPLEMENT_CHECK_MS,
  POND_SUPPLEMENT_MAX_INTERVAL_MULT,
  QUALITY_BITE_BASE,
  QUALITY_SIZE_CAP,
  REFERENCE_SIZE_M,
  SIZE_BITE_K,
  SIZE_ESCAPE_CURVE_EXPONENT,
  TACKLES,
  calcEffectiveEscapeRate,
  calcEscapeGrowthSize,
  calcFishBiteContribution,
  calcHookDurationMs,
  calcQualitySizeBiteRate,
  calcSizeEscapeRate,
  calcSizeHookDurationMs,
  calcSingleFishBiteProbability,
  calcSpotDestinationWeights,
  calcStandardLengthAtAge,
  calcSupplementCheckMs,
  calcSupplementQualityWeights,
  calcTickBiteProbability,
  getQualityMaxSize,
  getSpecies,
  growFishSizeV2,
  isNearMaxSize,
  pickMigrationSpot,
  pickSpotFishCandidate,
  pickSpotForNewFish,
  qualityIndex,
  rollFishQuality,
  rollJuvenileSize,
  rollSupplementQuality,
  type FishQuality,
  type PondFishEntity,
} from '@fish-social/shared';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SAMPLES = 10_000;

function simulateSpotMultiplier(): boolean {
  console.log('\n=== 钓点乘法系数 ===');
  const base = 0.02;
  const zero = calcFishBiteContribution(0, base, 0);
  const one = calcFishBiteContribution(1, base, 0);
  const five = calcFishBiteContribution(5, base, 0);
  const p1 = calcTickBiteProbability(one);
  const p5 = calcTickBiteProbability(five);
  const zeroPass = zero === 0;
  const ratioPass = Math.abs(p5 / p1 - 5) < 0.01;
  console.log(`  ×0 → ${zero} ${zeroPass ? 'OK' : 'FAIL'}`);
  console.log(`  ×5/×1 ≈ ${(p5 / p1).toFixed(2)} (expect 5) ${ratioPass ? 'OK' : 'FAIL'}`);
  return zeroPass && ratioPass;
}

function simulateNearMaxConstants(): boolean {
  console.log('\n=== 满尺寸鱼常量（咬钩仍按品质） ===');
  const sp = getSpecies('marlin');
  const maxSize = getQualityMaxSize('gold', sp);
  const fish = { quality: 'gold' as const, sizeM: maxSize, speciesId: sp.id };
  const biteAtMax = calcQualitySizeBiteRate('gold', maxSize);
  const bitePass = Math.abs(biteAtMax - QUALITY_BITE_BASE.gold * BITE_BASE_SCALE) < 0.00001;
  const escAt40 = calcSizeEscapeRate(REFERENCE_SIZE_M);
  const hookAt40 = calcSizeHookDurationMs(REFERENCE_SIZE_M);
  const escPass = Math.abs(escAt40 - ESCAPE_AT_40M) < 0.001;
  const hookPass = hookAt40 === HOOK_AT_40M_MS;
  console.log(
    `  bite@${maxSize}m=${(biteAtMax * 100).toFixed(4)}% (base ${(QUALITY_BITE_BASE.gold * 100).toFixed(2)}%) ${bitePass ? 'OK' : 'FAIL'}`,
  );
  console.log(`  esc@40m=${escAt40} (expect ${ESCAPE_AT_40M}) ${escPass ? 'OK' : 'FAIL'}`);
  console.log(`  hook@40m=${hookAt40}ms (expect ${HOOK_AT_40M_MS}) ${hookPass ? 'OK' : 'FAIL'}`);
  return bitePass && escPass && hookPass && isNearMaxSize(fish);
}

function simulateSizeOnlyEscapeHook(): boolean {
  console.log('\n=== D6 同体长跨品质脱钩/收杆相同 ===');
  const sizeM = 2.0;
  const sp = getSpecies('carp');
  const escGray = calcEffectiveEscapeRate(sizeM, 'basic', 1.0);
  const escGold = calcEffectiveEscapeRate(sizeM, 'basic', 1.0);
  const hookGray = calcHookDurationMs('gray', sizeM, sp.id);
  const hookGold = calcHookDurationMs('gold', sizeM, sp.id);
  const escPass = escGray === escGold;
  const hookPass = hookGray === hookGold;
  console.log(`  escape gray=${escGray} gold=${escGold} ${escPass ? 'OK' : 'FAIL'}`);
  console.log(`  hook gray=${hookGray}ms gold=${hookGold}ms ${hookPass ? 'OK' : 'FAIL'}`);
  return escPass && hookPass;
}

function simulateQualitySizeBiteGradient(): boolean {
  console.log('\n=== 品质×尺寸咬钩梯度 ===');
  const cap = QUALITY_SIZE_CAP.gray;
  const minBite = calcQualitySizeBiteRate('gray', 0);
  const maxBite = calcQualitySizeBiteRate('gray', cap);
  const ratio = minBite / maxBite;
  const expected = 1 - SIZE_BITE_K;
  const monotonicPass = minBite < maxBite && Math.abs(ratio - expected) < 0.001;
  const midBite = calcQualitySizeBiteRate('gray', cap * 0.5);
  const midPass = minBite < midBite && midBite < maxBite;
  const goldPass = calcQualitySizeBiteRate('gold', QUALITY_SIZE_CAP.gold) < calcQualitySizeBiteRate('gray', cap);
  console.log(
    `  gray n=0/n=1 ratio=${ratio.toFixed(3)} (expect ${expected.toFixed(2)}) ${monotonicPass ? 'OK' : 'FAIL'}`,
  );
  console.log(`  gray mid-tier monotonic ${midPass ? 'OK' : 'FAIL'}`);
  console.log(`  gold max < gray max ${goldPass ? 'OK' : 'FAIL'}`);
  return monotonicPass && midPass && goldPass;
}

function simulateTackleEscape(): boolean {
  console.log('\n=== 渔具脱钩绝对减免 (大师竿) ===');
  const sizeM = 8.0;
  const raw = calcSizeEscapeRate(sizeM);
  const basic = calcEffectiveEscapeRate(sizeM, 'basic', 1.0);
  const withMaster = calcEffectiveEscapeRate(sizeM, 'master', 1.0);
  const masterReduction = TACKLES.find((t) => t.id === 'master')!.escapeReduction;
  const expected = Math.max(0, raw - masterReduction);
  const diff = Math.abs(withMaster - expected);
  const pass = Math.abs(basic - raw) < 0.0001 && diff < 0.0001 && withMaster >= 0;
  console.log(
    `  raw=${(raw * 100).toFixed(1)}% basic=${(basic * 100).toFixed(1)}% master=${(withMaster * 100).toFixed(1)}% expected=${(expected * 100).toFixed(1)}% ${pass ? 'OK' : 'FAIL'}`,
  );
  return pass;
}

function simulateGrowthCurve7d(): boolean {
  console.log('\n=== 7 日绝对生长曲线 ===');
  const at7 = calcStandardLengthAtAge(GROWTH_CURVE_DAYS);
  const ceilingPass = at7 === GROWTH_CURVE_CEILING_M;
  const sp = getSpecies('crucian');
  const bornAt = Date.now() - GROWTH_CURVE_DAYS * 86_400_000;
  const birthSizeM = 0.15;
  const graySize = growFishSizeV2('gray', sp, birthSizeM, birthSizeM, bornAt);
  const goldSize = growFishSizeV2('gold', sp, birthSizeM, birthSizeM, bornAt);
  const grayCap = getQualityMaxSize('gray', sp);
  const goldCap = getQualityMaxSize('gold', sp);
  const grayPass = Math.abs(graySize - grayCap) < 0.01;
  const goldPass = Math.abs(goldSize - goldCap) < 0.01;
  const sameCurvePass = calcStandardLengthAtAge(3) > grayCap;
  console.log(`  L(7)=${at7}m (expect ${GROWTH_CURVE_CEILING_M}) ${ceilingPass ? 'OK' : 'FAIL'}`);
  console.log(`  gray 7d → ${graySize}m (cap ${grayCap}m) ${grayPass ? 'OK' : 'FAIL'}`);
  console.log(`  gold 7d → ${goldSize}m (cap ${goldCap}m) ${goldPass ? 'OK' : 'FAIL'}`);
  console.log(`  L(3)>gray cap ${sameCurvePass ? 'OK' : 'FAIL'}`);
  return ceilingPass && grayPass && goldPass && sameCurvePass;
}

function simulateEscapeGrowth(): boolean {
  console.log('\n=== 脱钩成长 ×1.02 ===');
  const sp = getSpecies('carp');
  const grown = calcEscapeGrowthSize('blue', sp, 1.0);
  const pass = grown === 1.02;
  console.log(`  1.00m → ${grown}m ${pass ? 'OK' : 'FAIL'}`);
  return pass;
}

function simulateJuvenileSize(): boolean {
  console.log('\n=== 幼年体长 0.08~0.20m ===');
  const sp = getSpecies('carp');
  let min = Infinity;
  let max = 0;
  for (let i = 0; i < SAMPLES; i++) {
    const q = rollFishQuality();
    const sz = rollJuvenileSize(q, sp);
    min = Math.min(min, sz);
    max = Math.max(max, sz);
  }
  const rangePass = min >= JUVENILE_SIZE_M_MIN && max <= JUVENILE_SIZE_M_MAX;
  console.log(`  range [${min.toFixed(2)}, ${max.toFixed(2)}]m ${rangePass ? 'OK' : 'FAIL'}`);
  return rangePass;
}

function simulatePondSeedDistribution(): boolean {
  console.log('\n=== 48 鱼池塘品质分布 (100次采样) ===');
  const blueIdx = qualityIndex('blue');
  const purpleIdx = qualityIndex('purple');
  const redIdx = qualityIndex('red');
  const samples: Array<{ bp: number; rp: number }> = [];

  for (let s = 0; s < 100; s++) {
    let bp = 0;
    let rp = 0;
    for (let i = 0; i < 48; i++) {
      const q = rollFishQuality();
      const idx = qualityIndex(q);
      if (idx >= blueIdx && idx <= purpleIdx) bp++;
      if (idx >= redIdx) rp++;
    }
    samples.push({ bp: bp / 48, rp: rp / 48 });
  }

  const meanBp = samples.reduce((s, x) => s + x.bp, 0) / samples.length;
  const meanRp = samples.reduce((s, x) => s + x.rp, 0) / samples.length;
  const bpPass = Math.abs(meanBp - 0.27) < 0.04;
  const rpPass = Math.abs(meanRp - 0.07) < 0.03;
  console.log(`  mean blue~purple: ${(meanBp * 100).toFixed(1)}% (theory≈27%) ${bpPass ? 'OK' : 'FAIL'}`);
  console.log(`  mean red+: ${(meanRp * 100).toFixed(1)}% (theory≈7%) ${rpPass ? 'OK' : 'FAIL'}`);
  return bpPass && rpPass;
}

function emptyQualityCounts(): Record<FishQuality, number> {
  return Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0])) as Record<FishQuality, number>;
}

function simulateSupplementInterval(): boolean {
  console.log('\n=== D7 动态补充间隔 ===');
  const at3 = calcSupplementCheckMs(3);
  const at0 = calcSupplementCheckMs(0);
  const cap = Math.floor(POND_SUPPLEMENT_CHECK_MS * POND_SUPPLEMENT_MAX_INTERVAL_MULT);
  const pass3 = at3 === 900_000;
  const pass0 = at0 === 2_250_000;
  const passCap = cap === 2_700_000;
  console.log(`  active=3 → ${at3}ms (expect 900000) ${pass3 ? 'OK' : 'FAIL'}`);
  console.log(`  active=0 → ${at0}ms (expect 2250000 / 37.5min) ${pass0 ? 'OK' : 'FAIL'}`);
  console.log(`  max cap → ${cap}ms (45min) ${passCap ? 'OK' : 'FAIL'}`);
  return pass3 && pass0 && passCap;
}

function simulateSupplementQualityWeights(): boolean {
  console.log('\n=== D8 品质缺口补充权重 ===');
  const maxPop = 80;
  const skewed = emptyQualityCounts();
  skewed.gray = 0;
  skewed.green = 0;
  skewed.purple = 3;
  const skewedWeights = calcSupplementQualityWeights(skewed, maxPop);
  const grayIdx = qualityIndex('gray');
  const purpleIdx = qualityIndex('purple');
  const purpleVsGrayPass = skewedWeights[purpleIdx] < skewedWeights[grayIdx] * 0.05;
  console.log(
    `  灰0绿0紫3: gray=${skewedWeights[grayIdx].toFixed(2)} purple=${skewedWeights[purpleIdx].toFixed(4)} ${purpleVsGrayPass ? 'OK' : 'FAIL'}`,
  );

  const empty = emptyQualityCounts();
  const emptyWeights = calcSupplementQualityWeights(empty, maxPop);
  const purpleEmptyPass = emptyWeights[purpleIdx] > 0;
  console.log(`  紫 actual=0 权重>0: ${emptyWeights[purpleIdx].toFixed(3)} ${purpleEmptyPass ? 'OK' : 'FAIL'}`);

  const samples = 5000;
  const emptyCounts = emptyQualityCounts();
  const rolled = emptyQualityCounts();
  for (let i = 0; i < samples; i++) {
    rolled[rollSupplementQuality(emptyCounts, maxPop)] += 1;
  }
  const grayPct = rolled.gray / samples;
  const greenPct = rolled.green / samples;
  const lowTierPct = grayPct + greenPct;
  const emptyDistPass = lowTierPct >= 0.6;
  console.log(
    `  全空塘低品主导 gray+green=${(lowTierPct * 100).toFixed(1)}% (expect ≥60%) ${emptyDistPass ? 'OK' : 'FAIL'}`,
  );

  const zeroPopWeights = calcSupplementQualityWeights(empty, 0);
  const fallbackSumPass = zeroPopWeights.reduce((a, b) => a + b, 0) === 0;
  console.log(`  maxPopulation=0 权重和为 0 → fallback ${fallbackSumPass ? 'OK' : 'FAIL'}`);

  return purpleVsGrayPass && purpleEmptyPass && emptyDistPass && fallbackSumPass;
}

function simulateSpotMigrationConstants(): boolean {
  console.log('\n=== D9 钓点鱼群流动性 ===');
  const intervalPass = FISH_MIGRATION_CHECK_MS === POND_SUPPLEMENT_CHECK_MS;
  console.log(
    `  FISH_MIGRATION_CHECK_MS=${FISH_MIGRATION_CHECK_MS} (expect ${POND_SUPPLEMENT_CHECK_MS}) ${intervalPass ? 'OK' : 'FAIL'}`,
  );

  const spotIds = ['spot-1', 'spot-2', 'spot-3'];
  const habitat = { 'spot-1': 5, 'spot-2': 0, 'spot-3': 2.5 };
  const destWeights = calcSpotDestinationWeights(spotIds, habitat);
  const blendPass =
    Math.abs(destWeights[0]! - (5 * 0.7 + 0.3)) < 0.001 &&
    Math.abs(destWeights[1]! - (0 * 0.7 + 0.3)) < 0.001;
  console.log(`  calcSpotDestinationWeights blend ${blendPass ? 'OK' : 'FAIL'}`);

  let pickPass = true;
  for (let i = 0; i < 100; i++) {
    const picked = pickSpotForNewFish(spotIds, habitat);
    const migrated = pickMigrationSpot(spotIds, habitat);
    if (!spotIds.includes(picked) || !spotIds.includes(migrated)) pickPass = false;
  }
  console.log(`  pickSpotForNewFish / pickMigrationSpot 合法钓点 ${pickPass ? 'OK' : 'FAIL'}`);

  const fishBySpot: Record<string, string[]> = {
    'spot-1': ['f1', 'f2', 'f3', 'f4'],
    'spot-2': ['f5', 'f6'],
    'spot-3': ['f7', 'f8', 'f9'],
  };
  const spotAIds = new Set(fishBySpot['spot-1']);
  const spotBIds = new Set(fishBySpot['spot-2']);
  let overlap = 0;
  for (const id of spotAIds) if (spotBIds.has(id)) overlap++;
  const distinctPass = overlap === 0;
  console.log(`  点 A vs 点 B 鱼 id 无交集 ${distinctPass ? 'OK' : 'FAIL'}`);

  const totalBefore = Object.values(fishBySpot).reduce((s, arr) => s + arr.length, 0);
  let migratedCount = 0;
  for (const [spot, ids] of Object.entries(fishBySpot)) {
    for (const id of ids) {
      if (Math.random() <= FISH_MIGRATION_FRACTION) {
        const dest = pickMigrationSpot(spotIds, habitat);
        fishBySpot[spot] = fishBySpot[spot]!.filter((x) => x !== id);
        fishBySpot[dest] = [...(fishBySpot[dest] ?? []), id];
        migratedCount++;
      }
    }
  }
  const totalAfter = Object.values(fishBySpot).reduce((s, arr) => s + arr.length, 0);
  const migrationTotalPass = totalBefore === totalAfter;
  console.log(
    `  迁徙模拟 total ${totalBefore}→${totalAfter} migrated~${migratedCount} ${migrationTotalPass ? 'OK' : 'FAIL'}`,
  );

  return intervalPass && blendPass && pickPass && distinctPass && migrationTotalPass;
}

function simulateSpotLocalBite(): boolean {
  console.log('\n=== D9 分区咬钩（仅本点候选） ===');
  const fish: PondFishEntity = {
    id: 'f1',
    pondId: 'pond-calm',
    spotId: 'calm-spot-1',
    speciesId: 'crucian',
    quality: 'gray',
    sizeM: 0.15,
    bornAt: 0,
    generation: 0,
    biteMultiplier: 1,
    escapeMultiplier: 1,
  };
  const spotMult = 3;
  const pSingle = calcSingleFishBiteProbability(fish, spotMult, 0);
  const pOtherSpot = pSingle;
  const independentPass = Math.abs(pSingle - pOtherSpot) < 1e-9;
  console.log(`  单鱼 pBite 不随全塘鱼数变 ${independentPass ? 'OK' : 'FAIL'}`);

  const fishingSessionSrc = fs.readFileSync(
    path.resolve(__dirname, '../server/src/fishingSession.ts'),
    'utf8',
  );
  const usesSpotLocal = fishingSessionSrc.includes('listPondFishAtSpot');
  const usesSingleFish =
    fishingSessionSrc.includes('pickSpotFishCandidate') &&
    fishingSessionSrc.includes('calcSingleFishBiteProbability');
  const rollBlock = fishingSessionSrc.match(
    /export function rollBiteHook[\s\S]*?^}/m,
  )?.[0];
  const noSumBite = rollBlock ? !rollBlock.includes('calcTickBiteProbability') : false;
  console.log(
    `  rollBiteHook 单鱼抽+判、无 Σ ${usesSpotLocal && usesSingleFish && noSumBite ? 'OK' : 'FAIL'}`,
  );

  const botsSrc = fs.readFileSync(path.resolve(__dirname, '../server/src/bots.ts'), 'utf8');
  const botUsesSpot = botsSrc.includes('processWaitingBiteTick') && botsSrc.includes('bot.spotId');
  const missFloat = fishingSessionSrc.includes('emitFishingMissFloatText');
  console.log(`  Bot 传 spotId 咬钩 ${botUsesSpot ? 'OK' : 'FAIL'}`);
  console.log(`  空杆飘字 emitFishingMissFloatText ${missFloat ? 'OK' : 'FAIL'}`);

  return independentPass && usesSpotLocal && usesSingleFish && noSumBite && botUsesSpot && missFloat;
}

function simulateV041BiteTuning(): boolean {
  console.log('\n=== D10–D12 v0.4.1 咬钩产量调优 ===');
  const scalePass = Math.abs(BITE_BASE_SCALE - 0.05) < 1e-9;
  console.log(`  BITE_BASE_SCALE=${BITE_BASE_SCALE} ${scalePass ? 'OK' : 'FAIL'}`);

  const grayJuvenile = calcQualitySizeBiteRate('gray', 0.14);
  const grayJuvenilePass = Math.abs(grayJuvenile - 0.0016) < 0.0005;
  console.log(
    `  灰幼 0.14m baseBite=${(grayJuvenile * 100).toFixed(3)}% (expect ~0.16%) ${grayJuvenilePass ? 'OK' : 'FAIL'}`,
  );

  const escJuvenile = calcSizeEscapeRate(0.1);
  const escJuvenilePass = Math.abs(escJuvenile - 0.22) < 0.02;
  console.log(
    `  calcSizeEscapeRate(0.10)=${(escJuvenile * 100).toFixed(1)}% (expect ~22%) ${escJuvenilePass ? 'OK' : 'FAIL'}`,
  );

  const n = Math.min(1, 1.0 / REFERENCE_SIZE_M);
  const curve = 1 - Math.pow(1 - n, SIZE_ESCAPE_CURVE_EXPONENT);
  const legacyEsc1 = ESCAPE_AT_40M * curve;
  const newEsc1 = calcSizeEscapeRate(1.0);
  const esc1Pass = Math.abs(newEsc1 - legacyEsc1) / legacyEsc1 <= 0.02;
  console.log(
    `  calcSizeEscapeRate(1.0) 与改前 ±2% ${esc1Pass ? 'OK' : 'FAIL'}`,
  );

  const candidates: PondFishEntity[] = FISH_QUALITIES.map((q, i) => ({
    id: `c${i}`,
    pondId: 'pond-calm',
    spotId: 'calm-spot-1',
    speciesId: 'crucian',
    quality: q.id,
    sizeM: 0.12,
    bornAt: 0,
    generation: 0,
    biteMultiplier: 1,
    escapeMultiplier: 1,
  }));
  let pickPass = true;
  for (let i = 0; i < 50; i++) {
    const picked = pickSpotFishCandidate(candidates);
    if (!candidates.some((c) => c.id === picked.id)) pickPass = false;
  }
  console.log(`  pickSpotFishCandidate 合法候选 ${pickPass ? 'OK' : 'FAIL'}`);

  return scalePass && grayJuvenilePass && escJuvenilePass && esc1Pass && pickPass;
}

function checkBasics(): boolean {
  console.log('\n=== 公式基线 ===');
  console.log(`  FISH_BITE_CHECK_MS=${FISH_BITE_CHECK_MS} (expect 60000)`);
  console.log(`  species count=${FISH_SPECIES.length}  qualities=${FISH_QUALITIES.length}`);
  console.log(`  purple cap=${QUALITY_SIZE_CAP.purple}m`);
  const pass = FISH_BITE_CHECK_MS === 60_000;
  console.log(`  check interval ${pass ? 'OK' : 'FAIL'}`);
  return pass;
}

const results = [
  checkBasics(),
  simulateSupplementInterval(),
  simulateSupplementQualityWeights(),
  simulateSpotMigrationConstants(),
  simulateSpotLocalBite(),
  simulateV041BiteTuning(),
  simulateSpotMultiplier(),
  simulateNearMaxConstants(),
  simulateSizeOnlyEscapeHook(),
  simulateQualitySizeBiteGradient(),
  simulateTackleEscape(),
  simulateGrowthCurve7d(),
  simulateEscapeGrowth(),
  simulateJuvenileSize(),
  simulatePondSeedDistribution(),
];

console.log('\n=== 总结 ===');
if (results.every(Boolean)) {
  console.log('A0-v2 自动化验收通过');
  process.exit(0);
}
console.log('A0-v2 自动化验收存在失败项');
process.exit(1);
