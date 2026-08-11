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



export type FishSpeciesId =

  | 'crucian' | 'carp' | 'bass' | 'tuna' | 'marlin' | 'catfish' | 'trout' | 'pike'

  | 'salmon' | 'tilapia' | 'mandarin' | 'sturgeon' | 'eel' | 'herring' | 'cod'

  | 'snapper' | 'mackerel' | 'perch' | 'topmouth' | 'koi';



export interface FishSpecies {

  id: FishSpeciesId;

  name: string;

  icon: string;

  typicalMinM: number;

  typicalMaxM: number;

  qualityAffinity: Partial<Record<FishQuality, number>>;

  biteWeight: number;

  /** 每 30 秒咬钩基础概率（0~1），A0-v2 替代 biteWeight 语义 */
  biteRatePerTick?: number;

  baseEscapeRate: number;

  diet: FishDiet;

}



export const FISH_SPECIES: FishSpecies[] = [

  { id: 'crucian', name: '鲫鱼', icon: '🐟', typicalMinM: 0.03, typicalMaxM: 0.35, qualityAffinity: { gray: 3, green: 2, blue: 0.5 }, biteWeight: 0.12, baseEscapeRate: 0.08, diet: 'herbivore' },

  { id: 'tilapia', name: '罗非鱼', icon: '🐟', typicalMinM: 0.1, typicalMaxM: 0.5, qualityAffinity: { gray: 3, green: 2 }, biteWeight: 0.12, baseEscapeRate: 0.08, diet: 'herbivore' },

  { id: 'perch', name: '河鲈', icon: '🏞️', typicalMinM: 0.08, typicalMaxM: 0.45, qualityAffinity: { gray: 2.5, green: 2 }, biteWeight: 0.12, baseEscapeRate: 0.08, diet: 'omnivore' },

  { id: 'carp', name: '鲤鱼', icon: '🐠', typicalMinM: 0.08, typicalMaxM: 0.9, qualityAffinity: { gray: 2, green: 2.5, blue: 1.2, purple: 0.3 }, biteWeight: 0.1, baseEscapeRate: 0.1, diet: 'herbivore' },

  { id: 'herring', name: '鲱鱼', icon: '🐟', typicalMinM: 0.08, typicalMaxM: 0.4, qualityAffinity: { gray: 3, green: 1.5 }, biteWeight: 0.1, baseEscapeRate: 0.1, diet: 'omnivore' },

  { id: 'mackerel', name: '鲭鱼', icon: '💨', typicalMinM: 0.12, typicalMaxM: 0.55, qualityAffinity: { gray: 2, green: 2, blue: 1 }, biteWeight: 0.1, baseEscapeRate: 0.1, diet: 'omnivore' },

  { id: 'cod', name: '鳕鱼', icon: '❄️', typicalMinM: 0.2, typicalMaxM: 1.1, qualityAffinity: { green: 2, blue: 2 }, biteWeight: 0.1, baseEscapeRate: 0.1, diet: 'omnivore' },

  { id: 'snapper', name: '鲷鱼', icon: '🌺', typicalMinM: 0.15, typicalMaxM: 0.85, qualityAffinity: { green: 2, blue: 2, purple: 0.8 }, biteWeight: 0.1, baseEscapeRate: 0.1, diet: 'omnivore' },

  { id: 'catfish', name: '鲶鱼', icon: '🐋', typicalMinM: 0.2, typicalMaxM: 1.2, qualityAffinity: { gray: 2, green: 2, blue: 1.5 }, biteWeight: 0.1, baseEscapeRate: 0.1, diet: 'omnivore' },

  { id: 'koi', name: '锦鲤', icon: '🎐', typicalMinM: 0.15, typicalMaxM: 0.8, qualityAffinity: { green: 2, blue: 2, purple: 1.5, red: 0.5 }, biteWeight: 0.1, baseEscapeRate: 0.1, diet: 'omnivore' },

  { id: 'bass', name: '大口黑鲈', icon: '🐡', typicalMinM: 0.15, typicalMaxM: 0.75, qualityAffinity: { green: 2, blue: 2, purple: 1, red: 0.4 }, biteWeight: 0.08, baseEscapeRate: 0.14, diet: 'carnivore' },

  { id: 'trout', name: '鳟鱼', icon: '🎏', typicalMinM: 0.12, typicalMaxM: 0.65, qualityAffinity: { green: 2.5, blue: 2, purple: 0.8 }, biteWeight: 0.08, baseEscapeRate: 0.14, diet: 'carnivore' },

  { id: 'mandarin', name: '桂鱼', icon: '🐯', typicalMinM: 0.15, typicalMaxM: 0.7, qualityAffinity: { green: 2, blue: 2, purple: 1 }, biteWeight: 0.08, baseEscapeRate: 0.14, diet: 'carnivore' },

  { id: 'eel', name: '鳗鱼', icon: '🐍', typicalMinM: 0.2, typicalMaxM: 1.0, qualityAffinity: { green: 2, blue: 2, purple: 1 }, biteWeight: 0.08, baseEscapeRate: 0.14, diet: 'carnivore' },

  { id: 'topmouth', name: '翘嘴', icon: '🦅', typicalMinM: 0.2, typicalMaxM: 1.0, qualityAffinity: { blue: 2, purple: 1.5, red: 0.8 }, biteWeight: 0.08, baseEscapeRate: 0.14, diet: 'carnivore' },

  { id: 'tuna', name: '黄鳍金枪鱼', icon: '🦈', typicalMinM: 0.5, typicalMaxM: 2.3, qualityAffinity: { blue: 2, purple: 2, red: 1.5, orange: 0.8 }, biteWeight: 0.06, baseEscapeRate: 0.18, diet: 'carnivore' },

  { id: 'salmon', name: '三文鱼', icon: '🍣', typicalMinM: 0.4, typicalMaxM: 1.8, qualityAffinity: { blue: 2, purple: 2, red: 1.2 }, biteWeight: 0.06, baseEscapeRate: 0.18, diet: 'carnivore' },

  { id: 'pike', name: '狗鱼', icon: '🐊', typicalMinM: 0.25, typicalMaxM: 1.0, qualityAffinity: { blue: 2, purple: 1.5, red: 1 }, biteWeight: 0.06, baseEscapeRate: 0.18, diet: 'carnivore' },

  { id: 'marlin', name: '蓝旗鱼', icon: '⚔️', typicalMinM: 1.0, typicalMaxM: 5.0, qualityAffinity: { purple: 2, red: 2, orange: 1.5, gold: 2 }, biteWeight: 0.04, baseEscapeRate: 0.25, diet: 'carnivore' },

  { id: 'sturgeon', name: '鲟鱼', icon: '🦕', typicalMinM: 0.8, typicalMaxM: 3.5, qualityAffinity: { purple: 2, red: 2, orange: 1.5, gold: 1 }, biteWeight: 0.04, baseEscapeRate: 0.25, diet: 'carnivore' },

];



export const MIN_FISH_SIZE_M = 0.03;

export const MAX_FISH_SIZE_M = 50.0;

export const ANNOUNCE_MIN_QUALITY: FishQuality = 'purple';



export function getQualityInfo(quality: FishQuality): FishQualityInfo {

  return FISH_QUALITIES.find((q) => q.id === quality)!;

}



export function getSpecies(speciesId: FishSpeciesId): FishSpecies {

  return FISH_SPECIES.find((s) => s.id === speciesId)!;

}



export function formatFishSize(sizeM: number): string {

  return `${sizeM.toFixed(2)}m`;

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


