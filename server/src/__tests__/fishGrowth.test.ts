import Database from 'better-sqlite3';
import { describe, expect, it } from 'vitest';
import {
  FISH_QUALITIES, FISH_SPECIES, FISH_GROWTH_PROFILES, calcIndividualFishLength,
  formatFishSize, getFishGrowthDaysTo90, getQualityMaxSize, getSpeciesSizePotential, getSpecies,
  growFishSizeV2, rollJuvenileSize, calcSingleFishBiteProbability,
} from '@fish-social/shared';
import { migrateIndividualFishGrowth, INDIVIDUAL_FISH_GROWTH_KEY } from '../migrations/individual_fish_growth.js';
import { migrateFishingV2 } from '../migrations/fishing_v2.js';

describe('fish size display', () => {
  it.each([
    [0, '0.0cm'], [.02, '2.0cm'], [.123, '12.3cm'], [.2, '20.0cm'], [.999, '99.9cm'],
    [1, '1.00m'], [1.005, '1.01m'], [1.234, '1.23m'], [9.994, '9.99m'],
    [9.995, '10.0m'], [10, '10.0m'], [12.35, '12.4m'], [40, '40.0m'],
  ])('%s metres displays as %s', (size, display) => expect(formatFishSize(size)).toBe(display));
});

describe('legacy individual fish growth', () => {
  const carp = getSpecies('carp');

  it('keeps species sizes distinct and higher-quality potential increasing', () => {
    expect(getSpeciesSizePotential('gold', getSpecies('loach'))).toBe(.45);
    expect(getSpeciesSizePotential('gold', getSpecies('chinese_sturgeon'))).toBe(6.3);
    for (const species of FISH_SPECIES) {
      let previous = 0;
      for (const quality of FISH_QUALITIES) {
        const cap = getSpeciesSizePotential(quality.id, species, 'stable-id');
        expect(cap).toBeGreaterThan(previous);
        previous = cap;
      }
    }
  });

  it('reaches 90% of remaining growth at its individual maturity time, then slows down', () => {
    const id = 'carp-90';
    const cap = getSpeciesSizePotential('gray', carp, id);
    const days = getFishGrowthDaysTo90('gray', id);
    const length = (age: number) => calcIndividualFishLength('gray', carp, .05, age, id);
    expect(length(0)).toBe(.05);
    expect(length(days)).toBeCloseTo(.05 + .9 * (cap - .05), 3);
    expect(length(.5) - length(0)).toBeGreaterThan(length(1) - length(.5));
    expect(length(1 / 48)).toBeLessThan(cap * .5);
    expect(length(30)).toBeLessThanOrEqual(cap);
  });

  it('produces many stable sizes for same-species, same-age fish, even after maturity', () => {
    const sizes = new Set<number>();
    const caps = new Set<number>();
    for (let i = 0; i < 1000; i++) {
      const id = `fish-${i}`;
      const cap = getSpeciesSizePotential('gray', carp, id);
      expect(cap).toBeGreaterThanOrEqual(getSpeciesSizePotential('gray', carp) * .75 - .001);
      expect(cap).toBeLessThanOrEqual(getSpeciesSizePotential('gray', carp));
      const size = calcIndividualFishLength('gray', carp, .05, 7, id);
      expect(calcIndividualFishLength('gray', carp, .05, 7, id)).toBe(size);
      expect(size * 1000).toBeCloseTo(Math.round(size * 1000), 7);
      sizes.add(size); caps.add(cap);
    }
    expect(sizes.size).toBeGreaterThan(100);
    expect(caps.size).toBeGreaterThan(100);
  });

  it('does not accumulate extra growth when replaying ecology ticks or shrink bonus-grown fish', () => {
    const bornAt = Date.UTC(2026, 10, 1);
    const now = bornAt + 8 * 3600000;
    const direct = growFishSizeV2('green', carp, .05, .05, bornAt, now, 'replay');
    let replay = .05;
    for (let t = bornAt; t <= now; t += 30000) replay = growFishSizeV2('green', carp, replay, .05, bornAt, t, 'replay');
    expect(replay).toBe(direct);
    expect(growFishSizeV2('green', carp, 2, .05, bornAt, now, 'replay')).toBe(2);
    expect(growFishSizeV2('green', carp, .05, .05, bornAt, bornAt - 1, 'replay')).toBe(.05);
  });

  it('generates stable juvenile entry baselines between 3.4 and 4.6 centimetres', () => {
    const sizes = new Set<number>();
    for (let i = 0; i < 300; i++) {
      const id = `juvenile-${i}`;
      const size = rollJuvenileSize('blue', carp, id);
      expect(size).toBeGreaterThanOrEqual(.034);
      expect(size).toBeLessThanOrEqual(.046);
      expect(rollJuvenileSize('gold', getSpecies('loach'), id)).toBe(size);
      sizes.add(size);
    }
    expect(sizes.size).toBeGreaterThanOrEqual(11);
  });

  it('normalizes runtime bite chance by quality potential independent of species', () => {
    const base = { quality: 'gray' as const, biteMultiplier: 1 };
    const chance = (speciesId: string) => calcSingleFishBiteProbability({
      ...base, speciesId, sizeM: getQualityMaxSize('gray', getSpecies(speciesId)),
    } as Parameters<typeof calcSingleFishBiteProbability>[0], 1);
    expect(chance('loach')).toBeCloseTo(chance('chinese_sturgeon'), 10);
    expect(FISH_GROWTH_PROFILES.gold.daysTo90).toBe(14);
  });
});

describe('growth migration', () => {
  it('recalculates existing pond fish once, preserves owned records, and blocks legacy auto-upgrades', () => {
    const db = new Database(':memory:');
    db.exec(`CREATE TABLE game_config (config_key TEXT PRIMARY KEY, config_value TEXT, updated_at INTEGER);
      CREATE TABLE pond_fish (id TEXT PRIMARY KEY, species_id TEXT, quality TEXT, size_m REAL, birth_size_m REAL, born_at INTEGER, bite_weight REAL);
      CREATE TABLE inventory (id TEXT PRIMARY KEY, size_m REAL);
      INSERT INTO inventory VALUES ('kept', 40)`);
    const now = Date.UTC(2026, 10, 1);
    const insert = db.prepare("INSERT INTO pond_fish VALUES (?, 'carp', 'gray', .2, .05, ?, NULL)");
    for (let i = 0; i < 30; i++) insert.run(`fish-${i}`, now - 7 * 86400000);
    expect(migrateIndividualFishGrowth(db, now)).toBe(30);
    const sizes = db.prepare('SELECT size_m FROM pond_fish').all() as { size_m: number }[];
    expect(new Set(sizes.map(row => row.size_m)).size).toBeGreaterThan(20);
    expect(migrateIndividualFishGrowth(db, now + 86400000)).toBe(0);
    expect((db.prepare('SELECT size_m FROM inventory').get() as {size_m:number}).size_m).toBe(40);
    expect(migrateFishingV2(db).qualityUpgrades).toHaveLength(0);
    expect((db.prepare('SELECT COUNT(*) n FROM pond_fish WHERE quality = ?').get('gray') as {n:number}).n).toBe(30);
    expect((db.prepare('SELECT config_value FROM game_config WHERE config_key = ?').get(INDIVIDUAL_FISH_GROWTH_KEY) as {config_value:string}).config_value).toBe('true');
    db.close();
  });
});
