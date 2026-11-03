import { describe, expect, it } from 'vitest';
import {
  FISH_QUALITIES, POND_CATALOG, PONDS, FISH_DAY_MS,
  createFishEntryGrowth, calcEntryFishLength, getFishGrowthT80Days,
  getQualityMaxSize, getSpecies, getFishQualityBaseCap, calcFishGrowthBonusMinutes,
  calcEscapeGrowthSize, calcPondSupplementCount, calcSupplementCheckMs,
  calcSupplementIdealCounts, calcSupplementQualityWeights, pickSupplementFish,
  getPondEcologyDayKey, getPondStockConfig, getPondCategoryQualityWeights,
  getGamePondDef, pickPondSpeciesForQuality, getWorldTileMap, worldPondIdAt,
  calcSingleFishBiteProbability, type FishQuality, type PondFishEntity,
} from '@fish-social/shared';

const NOW = Date.UTC(2026,10,4);
const counts = (n=0) => Object.fromEntries(FISH_QUALITIES.map(q=>[q.id,n])) as Record<FishQuality,number>;

describe('pond-entry growth contract', () => {
  it.each(FISH_QUALITIES.map(q=>q.id))('%s reaches its own 80% at T80, then grows more slowly without a 14-day stop', quality => {
    const state=createFishEntryGrowth(quality,`curve-${quality}`,NOW,'debug');
    const size=(days:number)=>calcEntryFishLength(quality,state,NOW+days*FISH_DAY_MS);
    const t=getFishGrowthT80Days(quality);
    expect(size(0)).toBeCloseTo(state.entrySizeM,12);
    expect(size(t)).toBeCloseTo(.8*state.individualCapM,12);
    expect(size(t*.5)-size(0)).toBeGreaterThan(size(t)-size(t*.5));
    expect(size(15)).toBeGreaterThan(size(14));
    expect(size(1000)).toBeLessThanOrEqual(state.individualCapM);
    expect(size(-1)).toBeCloseTo(state.entrySizeM,12);
  });

  it('persists independent, reproducible baseline and cap traits instead of species or random speed multipliers', () => {
    const capSet=new Set<number>(),entrySet=new Set<number>();
    for(let i=0;i<1000;i++) {
      const id=`traits-${i}`;
      const a=createFishEntryGrowth('gray',id,NOW,'debug');
      const b=createFishEntryGrowth('gray',id,NOW,'debug');
      expect(a).toEqual(b);
      expect(a.juvenileEntrySizeM).toBeGreaterThanOrEqual(.034);
      expect(a.juvenileEntrySizeM).toBeLessThanOrEqual(.046);
      expect(a.individualCapM).toBeGreaterThanOrEqual(.17);
      expect(a.individualCapM).toBeLessThanOrEqual(.23);
      expect(getQualityMaxSize('gray',getSpecies('loach'),id)).toBe(a.individualCapM);
      expect(getQualityMaxSize('gray',getSpecies('chinese_sturgeon'),id)).toBe(a.individualCapM);
      capSet.add(a.individualCapM);entrySet.add(a.juvenileEntrySizeM);
    }
    expect(capSet.size).toBeGreaterThan(45);
    expect(entrySet.size).toBe(13);
    expect(getQualityMaxSize('gold',getSpecies('loach'))).toBe(40);
  });

  it('recovers historical entry dates from opening lengths without fabricating feeding history', () => {
    for(const quality of FISH_QUALITIES.map(q=>q.id)) {
      for(const unit of [0,.1,.5,.9,1]) {
        const fresh=createFishEntryGrowth(quality,'opening',NOW,'debug');
        const seeded=createFishEntryGrowth(quality,'opening',NOW,'pre_opening_seed',()=>unit);
        const upper=calcEntryFishLength(quality,fresh,NOW+14*FISH_DAY_MS);
        expect(seeded.enteredAt).toBeGreaterThanOrEqual(NOW-14*FISH_DAY_MS);
        expect(seeded.enteredAt).toBeLessThanOrEqual(NOW);
        expect(seeded.entrySizeM).toBe(fresh.entrySizeM);
        expect(seeded.growthBonusMinutes).toBe(0);
        expect(calcEntryFishLength(quality,seeded,NOW)).toBeCloseTo(fresh.entrySizeM+unit*(upper-fresh.entrySizeM),5);
      }
    }
  });

  it('seeds mixed-specification fish at the real entry time and continues from the actual entry size', () => {
    const t=getFishGrowthT80Days('blue');
    const base=createFishEntryGrowth('blue','mixed',NOW,'debug');
    for(const [draw,position] of [[.1,.1],[.6,.5],[.95,1.15]]) {
      const samples=[draw,.5];
      const mixed=createFishEntryGrowth('blue','mixed',NOW,'supplement',()=>samples.shift()!);
      expect(mixed.enteredAt).toBe(NOW);
      expect(mixed.entrySizeM).toBeCloseTo(calcEntryFishLength('blue',base,NOW+position*t*FISH_DAY_MS),3);
      expect(calcEntryFishLength('blue',mixed,NOW)).toBeCloseTo(mixed.entrySizeM,12);
      expect(calcEntryFishLength('blue',mixed,NOW+FISH_DAY_MS)).toBeGreaterThan(mixed.entrySizeM);
    }
  });

  it('nutrition speeds growth without changing residence, and time in inventory does not count as pond growth', () => {
    const state=createFishEntryGrowth('green','feeding',NOW,'debug');
    const advanced={...state,growthBonusMinutes:360};
    expect(advanced.enteredAt).toBe(state.enteredAt);
    expect(calcEntryFishLength('green',advanced,NOW)).toBeCloseTo(calcEntryFishLength('green',state,NOW+.25*FISH_DAY_MS),12);
    const returned={...advanced,outOfPondMs:3*FISH_DAY_MS};
    expect(calcEntryFishLength('green',returned,NOW+4*FISH_DAY_MS)).toBeCloseTo(calcEntryFishLength('green',advanced,NOW+FISH_DAY_MS),12);
    const target=calcEntryFishLength('green',advanced,NOW);
    expect(calcFishGrowthBonusMinutes('green',state,target,NOW)).toBeCloseTo(360,9);
    expect(calcEscapeGrowthSize('green',getSpecies('carp'),target,'feeding')).toBe(target);
  });

  it('uses a saved individual cap for bite normalization', () => {
    const fish={speciesId:'carp',quality:'gray',biteMultiplier:1,sizeM:.2,individualCapM:.2} as PondFishEntity;
    const other={...fish,sizeM:.23,individualCapM:.23};
    expect(calcSingleFishBiteProbability(fish,1)).toBeCloseTo(calcSingleFishBiteProbability(other,1),12);
  });
});

describe('stocking thresholds and full catalog', () => {
  it('applies hourly 40→80 and daily→100 with a stable 04:00 Shanghai boundary', () => {
    expect([0,40,41,80,100].map(n=>calcPondSupplementCount(n))).toEqual([80,40,0,0,0]);
    expect([0,40,41,80,100].map(n=>calcPondSupplementCount(n,true))).toEqual([100,60,59,20,0]);
    expect(calcPondSupplementCount(110,true)).toBe(0);
    expect(calcSupplementCheckMs(0)).toBe(3600000);
    expect(calcSupplementCheckMs(20)).toBe(3600000);
    expect(getPondEcologyDayKey(Date.UTC(2026,10,3,19,59,59))).toBe('2026-11-03');
    expect(getPondEcologyDayKey(Date.UTC(2026,10,3,20))).toBe('2026-11-04');
  });

  it('strictly excludes stocked high qualities in every supplement selection', () => {
    const actual=counts();
    const ideal=calcSupplementIdealCounts(100,'giant');
    for(const quality of ['purple','red','orange','gold'] as const) actual[quality]=Math.ceil(ideal[quality]);
    const weights=calcSupplementQualityWeights(actual,100,'giant');
    expect(weights.slice(3)).toEqual([0,0,0,0]);
    for(const quality of ['purple','red','orange','gold'] as const) {
      actual[quality]--;
      expect(calcSupplementQualityWeights(actual,100,'giant')[FISH_QUALITIES.findIndex(q=>q.id===quality)]).toBeGreaterThan(0);
      actual[quality]++;
    }
    for(let i=0;i<1000;i++) expect(['gray','green','blue']).toContain(pickSupplementFish('pond-frost',actual,100)?.quality);
    expect(pickSupplementFish('not-a-pond',counts(),100)).toBeUndefined();
  });

  it('provides 40 legal 100-fish ponds, 20 spots each, valid high-quality species and reachable map entries', () => {
    expect(POND_CATALOG).toHaveLength(40);
    expect(new Set(POND_CATALOG.map(p=>p.id)).size).toBe(40);
    for(const pond of POND_CATALOG) {
      expect(getPondStockConfig(pond.id)?.initialPopulation).toBe(100);
      expect(getPondStockConfig(pond.id)?.maxPopulation).toBe(100);
      expect(PONDS.find(p=>p.id===pond.id)?.spots).toHaveLength(20);
      const def=getGamePondDef(pond.id)!;
      for(const quality of getPondCategoryQualityWeights(def.pondCategory).filter(q=>q.spawnWeight>0)) {
        expect(pickPondSpeciesForQuality(pond.id,quality.quality)).toBeTruthy();
      }
      expect(getFishQualityBaseCap('gold')).toBe(40);
    }
    const world=getWorldTileMap();
    expect(world.entries).toHaveLength(40);
    for(const entry of world.entries) expect(worldPondIdAt(world,entry.col,entry.row)).toBe(entry.pondId);
    expect(getPondStockConfig('pond-novice')?.initialPopulation).toBe(1);
  });
});
