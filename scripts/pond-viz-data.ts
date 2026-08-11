import { FISH_QUALITIES, getSpecies, rollFishQuality } from '@fish-social/shared';
import { growFishSizeV2, QUALITY_SIZE_CAP, rollInitialSize } from '@fish-social/shared';
import { getPondStockConfig } from '@fish-social/shared';

const config = getPondStockConfig('pond-calm')!;
const N = 8000;
const qualityCounts = Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0]));
const sizes: number[] = [];
const byQualitySize = Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, [] as number[]]));

function pickSpecies() {
  const pool = Math.random() < config.rareSpawnRate ? config.rareSpecies : config.commonSpecies;
  return pool[Math.floor(Math.random() * pool.length)];
}

for (let i = 0; i < N; i++) {
  const sid = pickSpecies();
  const sp = getSpecies(sid);
  const q = rollFishQuality();
  const sz = rollInitialSize(q, sp);
  qualityCounts[q]++;
  sizes.push(sz);
  byQualitySize[q].push(sz);
}

const qDist = FISH_QUALITIES.map((q) => ({
  id: q.id,
  name: q.name,
  color: q.color,
  count48: Math.round((qualityCounts[q.id] / N) * 48),
  pct: +((qualityCounts[q.id] / N) * 100).toFixed(1),
  avgSize: +(byQualitySize[q.id].reduce((a, b) => a + b, 0) / byQualitySize[q.id].length).toFixed(3),
}));

const buckets = [0, 0.05, 0.1, 0.2, 0.35, 0.5, 0.8, 1.2, 2, 4];
const hist = buckets.slice(0, -1).map((lo, i) => {
  const hi = buckets[i + 1];
  const c = sizes.filter((s) => s >= lo && s < hi).length;
  return { label: `${lo}-${hi}m`, count: Math.round((c / N) * 48) };
});

const hours = [0, 6, 12, 24, 48, 72, 120, 168, 240, 360, 480];
const species = getSpecies('carp');
const growth = FISH_QUALITIES.map((q) => {
  const qIdx = FISH_QUALITIES.findIndex((x) => x.id === q.id);
  const cap = QUALITY_SIZE_CAP[q.id];
  const floor = Math.max(0.03, species.typicalMinM * 0.6);
  const ceiling = Math.min(cap, species.typicalMaxM * 0.45);
  const r = Math.pow(0.5, 2 + qIdx * 0.3);
  let cur = floor + (ceiling - floor) * r;
  const pts = [{ h: 0, size: +cur.toFixed(3) }];
  for (let i = 1; i < hours.length; i++) {
    const dh = hours[i] - hours[i - 1];
    cur = growFishSizeV2(q.id, species, cur, dh);
    pts.push({ h: hours[i], size: +cur.toFixed(3) });
  }
  return {
    quality: q.id,
    name: q.name,
    color: q.color,
    cap,
    birth: pts[0].size,
    points: pts,
  };
});

console.log(JSON.stringify({ pond: 'pond-calm', initial: 48, qDist, hist, growth }, null, 2));
