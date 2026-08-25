/**
 * FEAT-FISH-CN-01：50 种本土鱼、真实塘名；分区以 pond_fish_pool 为准。
 * Run: npm run verify:fish-cn-01
 */
import {
  DELETED_FOREIGN_SPECIES_IDS,
  FISH_SPECIES,
  getCatchGroup,
  getGamePondDef,
  getPondStockConfig,
  getSpecies,
  listGamePonds,
  listGameSpecies,
  listPondFishPool,
} from '@fish-social/shared';

const FOREIGN = new Set<string>(DELETED_FOREIGN_SPECIES_IDS);
FOREIGN.add('bass');
FOREIGN.add('trout');
FOREIGN.add('perch');
FOREIGN.add('sturgeon');

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

const speciesIds = FISH_SPECIES.map((s) => s.id);
assert(speciesIds.length >= 48 && speciesIds.length <= 52, `species count=${speciesIds.length}`);
for (const id of FOREIGN) {
  assert(!speciesIds.includes(id), `foreign id still in catalog: ${id}`);
}
assert(getSpecies('crucian').name === '鲫鱼', 'crucian name');
assert(getSpecies('taimen').name === '哲罗鲑', 'taimen name');
assert(getSpecies('bass').id === 'black_bass', 'legacy bass maps to black_bass');
assert(getCatchGroup('bass') === 'lure_predator', 'legacy bass catchGroup');

const TIER_MAX: Record<string, number> = {
  common: 4,
  uncommon: 5,
  rare: 6,
  legendary: 7,
};
for (const s of listGameSpecies()) {
  assert(s.qualityMin === 1, `${s.speciesId} qualityMin=${s.qualityMin}`);
  const expectMax = TIER_MAX[s.rarityTier ?? ''] ?? 7;
  assert(s.qualityMax === expectMax, `${s.speciesId} qualityMax=${s.qualityMax} tier=${s.rarityTier}`);
  assert(getSpecies(s.speciesId).qualityMin === 1, `FishSpecies ${s.speciesId} qualityMin`);
  assert(getSpecies(s.speciesId).qualityMax === expectMax, `FishSpecies ${s.speciesId} qualityMax`);
}

const expectedNames: Record<string, string> = {
  'pond-calm': '千岛湖',
  'pond-mist': '太湖',
  'pond-sunset': '洪泽湖',
  'pond-bamboo': '鄱阳湖',
  'pond-reed': '洞庭湖',
  'pond-crystal': '滇池',
  'pond-lotus': '洱海',
  'pond-mirror': '镜泊湖',
  'pond-willow': '查干湖',
  'pond-stone': '万绿湖',
  'pond-spring': '北江',
  'pond-dusk': '丹江口水库',
  'pond-pine': '清江',
  'pond-coral': '舟山近海',
  'pond-moon': '厦门湾',
  'pond-fern': '南澳近海',
  'pond-ridge': '长江故道野塘',
  'pond-harbor': '青岛近海',
  'pond-orchid': '涠洲近海',
  'pond-frost': '兴凯湖',
  'pond-novice': '城郊练杆塘',
};

const ponds = listGamePonds();
assert(ponds.length === 21, `pond count=${ponds.length}`);
for (const [id, name] of Object.entries(expectedNames)) {
  const pond = getGamePondDef(id);
  assert(pond, `missing pond ${id}`);
  assert(pond.name === name, `${id} name=${pond.name} expected ${name}`);
  assert(pond.bioRegion, `${id} missing bioRegion`);
  assert(pond.waterType, `${id} missing waterType`);
}

const nationwide = listGameSpecies().filter((s) => s.nationwide);
assert(nationwide.length === 8, `nationwide count=${nationwide.length}`);

for (const pond of ponds) {
  const pool = listPondFishPool(pond.pondId);
  const minRows = pond.pondId === 'pond-novice' ? 3 : 8;
  assert(pool.length >= minRows, `${pond.pondId} pool species=${pool.length}`);
  const ids = new Set(pool.map((r) => r.speciesId));
  assert(ids.size === pool.length, `${pond.pondId} duplicate species in pool`);
  for (const row of pool) {
    assert(row.speciesName, `${pond.pondId}/${row.speciesId} missing speciesName`);
    assert(!FOREIGN.has(row.speciesId), `foreign ${row.speciesId} in ${pond.pondId}`);
  }
}

function assertNotInWrongRegion(speciesId: string, forbiddenRegions: string[]) {
  for (const pond of ponds) {
    const has = listPondFishPool(pond.pondId).some((r) => r.speciesId === speciesId);
    if (has) {
      assert(
        pond.bioRegion && !forbiddenRegions.includes(pond.bioRegion),
        `${speciesId} should not spawn in ${pond.pondId} (${pond.bioRegion})`,
      );
    }
  }
}

assertNotInWrongRegion('taimen', [
  'east_plain_lake',
  'yangtze_mid',
  'southwest_plateau',
  'south_reservoir',
  'southeast_coast',
  'north_bohai',
  'south_sea',
]);
assertNotInWrongRegion('grouper', [
  'east_plain_lake',
  'yangtze_mid',
  'southwest_plateau',
  'northeast_cold',
  'south_reservoir',
  'north_bohai',
]);

const nationwidePondCounts: Record<string, number> = {};
for (const sid of nationwide.map((s) => s.speciesId)) {
  nationwidePondCounts[sid] = ponds.filter((p) =>
    listPondFishPool(p.pondId).some((r) => r.speciesId === sid),
  ).length;
  assert(
    nationwidePondCounts[sid]! >= 15,
    `${sid} only in ${nationwidePondCounts[sid]} ponds`,
  );
}

const novicePool = listPondFishPool('pond-novice');
assert(
  novicePool.every((r) => ['crucian', 'carp', 'loach'].includes(r.speciesId)),
  'novice pool species',
);
assert(novicePool.length === 3, `novice pool count=${novicePool.length}`);

const calmStock = getPondStockConfig('pond-calm');
assert(calmStock && calmStock.commonSpecies.length > 0, 'calm stock from tables');
assert(!calmStock!.commonSpecies.includes('tuna'), 'no tuna in calm stock');

console.log('FEAT-FISH-CN-01 ok');
console.log('  species=', speciesIds.length);
console.log('  ponds=', ponds.length);
console.log('  calm pool=', listPondFishPool('pond-calm').length);
