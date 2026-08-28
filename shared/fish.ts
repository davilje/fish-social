/** 鱼品质（灰 → 金，难度递增） */
export type FishQuality =
  | 'gray'
  | 'green'
  | 'blue'
  | 'purple'
  | 'red'
  | 'orange'
  | 'gold';

export type FishDiet = 'herbivore' | 'omnivore' | 'carnivore';

export interface FishQualityInfo {
  id: FishQuality;
  name: string;
  color: string;
  weight: number;
}

export const FISH_QUALITIES: FishQualityInfo[] = [
  { id: 'gray', name: '普通', color: '#9E9E9E', weight: 38 },
  { id: 'green', name: '优良', color: '#4CAF50', weight: 28 },
  { id: 'blue', name: '稀有', color: '#2196F3', weight: 18 },
  { id: 'purple', name: '史诗', color: '#9C27B0', weight: 9 },
  { id: 'red', name: '传说', color: '#F44336', weight: 4 },
  { id: 'orange', name: '神话', color: '#FF9800', weight: 2 },
  { id: 'gold', name: '至尊', color: '#FFD700', weight: 1 },
];

/** FEAT-FISH-CN-01：种 ID 来自数值表，不再硬编码 20 种 union */
export type FishSpeciesId = string;

export const LEGACY_SPECIES_ID_MAP: Record<string, string> = {
  bass: 'black_bass',
  trout: 'rainbow_trout',
  perch: 'black_bass',
  sturgeon: 'chinese_sturgeon',
};

export const DELETED_FOREIGN_SPECIES_IDS = [
  'tuna',
  'marlin',
  'salmon',
  'cod',
  'herring',
  'snapper',
  'mackerel',
  'pike',
] as const;

export interface FishSpecies {
  id: FishSpeciesId;
  name: string;
  icon: string;
  /** 图鉴展示参考体长，不参与出生/成长结算 */
  typicalMinM: number;
  typicalMaxM: number;
  /** 播种品质带：1=gray … 7=gold */
  qualityMin: number;
  qualityMax: number;
  qualityAffinity: Partial<Record<FishQuality, number>>;
  biteWeight: number;
  /** 每 30 秒咬钩基础概率（0~1），A0-v2 替代 biteWeight 语义 */
  biteRatePerTick?: number;
  baseEscapeRate: number;
  diet: FishDiet;
}

import speciesJson from './generated/game-data/fish_species.json';

function rowToSpecies(row: {
  speciesId: string;
  name: string;
  diet?: string;
  typicalMinM?: number;
  typicalMaxM?: number;
  qualityMin?: number;
  qualityMax?: number;
}): FishSpecies {
  const diet = (
    row.diet === 'herbivore' || row.diet === 'carnivore' || row.diet === 'omnivore'
      ? row.diet
      : 'omnivore'
  ) as FishDiet;
  const qMin = Math.max(1, Math.min(7, Number(row.qualityMin) || 1));
  const qMax = Math.max(qMin, Math.min(7, Number(row.qualityMax) || 7));
  return {
    id: row.speciesId,
    name: row.name,
    icon: '🐟',
    typicalMinM: Number(row.typicalMinM) || 0.1,
    typicalMaxM: Number(row.typicalMaxM) || 0.5,
    qualityMin: qMin,
    qualityMax: qMax,
    qualityAffinity: {},
    biteWeight: 0.1,
    baseEscapeRate: 0.1,
    diet,
  };
}

/** FEAT-FISH-CN-01：权威种库来自 fish_species.json */
export const FISH_SPECIES: FishSpecies[] = (
  speciesJson as Array<{
    speciesId: string;
    name: string;
    diet?: string;
    typicalMinM?: number;
    typicalMaxM?: number;
    qualityMin?: number;
    qualityMax?: number;
  }>
).map(rowToSpecies);

const speciesById = new Map(FISH_SPECIES.map((s) => [s.id, s]));

export function resolveSpeciesId(speciesId: string): FishSpeciesId {
  return LEGACY_SPECIES_ID_MAP[speciesId] ?? speciesId;
}

export const MIN_FISH_SIZE_M = 0.02;
export const MAX_FISH_SIZE_M = 50.0;
export const ANNOUNCE_MIN_QUALITY: FishQuality = 'purple';

export function getQualityInfo(quality: FishQuality): FishQualityInfo {
  return FISH_QUALITIES.find((q) => q.id === quality)!;
}

export function getSpecies(speciesId: FishSpeciesId): FishSpecies {
  const mapped = resolveSpeciesId(speciesId);
  return speciesById.get(mapped) ?? speciesById.get('carp') ?? FISH_SPECIES[0]!;
}

export function formatFishSize(sizeM: number): string {
  return `${sizeM.toFixed(2)}m`;
}

export function formatFishWeight(weightKg: number): string {
  if (weightKg >= 100) return `${weightKg.toFixed(0)}kg`;
  if (weightKg >= 10) return `${weightKg.toFixed(1)}kg`;
  return `${weightKg.toFixed(2)}kg`;
}

/** 品质序 1=gray … 7=gold */
export function qualityRank(quality: FishQuality): number {
  const i = FISH_QUALITIES.findIndex((q) => q.id === quality);
  return i >= 0 ? i + 1 : 1;
}

export function qualityFromRank(rank: number): FishQuality {
  const clamped = Math.max(1, Math.min(7, Math.round(rank)));
  return FISH_QUALITIES[clamped - 1]!.id;
}

/** @deprecated A0 起品质出生固定 */
export function deriveQualityFromSize(speciesId: FishSpeciesId, sizeM: number): FishQuality {
  const species = getSpecies(speciesId);
  const ratio = sizeM / species.typicalMaxM;
  if (ratio >= 2.8) return 'gold';
  if (ratio >= 2.0) return 'orange';
  if (ratio >= 1.4) return 'red';
  if (ratio >= 1.0) return 'purple';
  if (ratio >= 0.65) return 'blue';
  if (ratio >= 0.35) return 'green';
  return 'gray';
}

function weightedPick<T>(items: { item: T; weight: number }[]): T {
  const total = items.reduce((s, i) => s + i.weight, 0);
  let r = Math.random() * total;
  for (const { item, weight } of items) {
    r -= weight;
    if (r <= 0) return item;
  }
  return items[items.length - 1].item;
}

export function rollFishQuality(): FishQuality {
  return weightedPick(FISH_QUALITIES.map((q) => ({ item: q.id, weight: q.weight })));
}

/** C4：父母品质影响子代掷骰 */
export function rollFishQualityWithParents(
  parentA: FishQuality,
  parentB: FishQuality,
): FishQuality {
  const indexOf = (q: FishQuality) => FISH_QUALITIES.findIndex((x) => x.id === q);
  const parentBonus = Math.log(1 + indexOf(parentA) + indexOf(parentB) + 2);
  return weightedPick(
    FISH_QUALITIES.map((q) => ({
      item: q.id,
      weight: q.weight * (1 + parentBonus * 0.08),
    })),
  );
}

export function rollFishSpecies(quality: FishQuality): FishSpeciesId {
  return weightedPick(
    FISH_SPECIES.map((s) => ({
      item: s.id,
      weight: (s.qualityAffinity[quality] ?? 0.6) + 0.4,
    })),
  );
}

export interface RolledFish {
  speciesId: FishSpeciesId;
  quality: FishQuality;
  sizeM: number;
}

export function isAnnounceQuality(quality: FishQuality): boolean {
  const order = FISH_QUALITIES.map((q) => q.id);
  return order.indexOf(quality) >= order.indexOf(ANNOUNCE_MIN_QUALITY);
}

export function buildCatchDescription(rolled: RolledFish): string {
  const species = getSpecies(rolled.speciesId);
  const q = getQualityInfo(rolled.quality);
  return `${q.name} · ${species.name} · ${formatFishSize(rolled.sizeM)}`;
}
