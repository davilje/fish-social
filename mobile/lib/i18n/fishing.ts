import {
  getQualityInfo,
  getSpecies,
  type FishQuality,
  type FishSpeciesId,
  type FishingFloatTextKind,
} from '@fish-social/shared';

export type AppLocale = 'zh-CN' | 'en-US';

let currentLocale: AppLocale = 'zh-CN';

const MESSAGES: Record<
  AppLocale,
  Record<FishingFloatTextKind, string>
> = {
  'zh-CN': {
    hook: '🐟 {species} 咬钩！',
    escape: '💨 {species} 脱钩了！',
    miss: '🌊 水面平静…',
    cast: '🎣 抛竿！',
  },
  'en-US': {
    hook: '🐟 {species} hooked!',
    escape: '💨 {species} got away!',
    miss: '🌊 All quiet…',
    cast: '🎣 Cast!',
  },
};

export function setAppLocale(locale: AppLocale): void {
  currentLocale = locale;
}

export function getAppLocale(): AppLocale {
  return currentLocale;
}

function qualityPrefix(quality?: FishQuality): string {
  if (!quality) return '';
  if (quality === 'gold' || quality === 'orange') return '✨';
  return '';
}

export function formatFishingFloatText(
  kind: FishingFloatTextKind,
  speciesId?: FishSpeciesId,
  quality?: FishQuality,
  locale: AppLocale = currentLocale,
): { text: string; color: string } {
  if (kind === 'miss') {
    return { text: MESSAGES[locale].miss, color: '#9E9E9E' };
  }
  if (kind === 'cast') {
    return { text: MESSAGES[locale].cast, color: '#4A90A4' };
  }
  const species = getSpecies(speciesId ?? 'crucian');
  const template = MESSAGES[locale][kind];
  const prefix = qualityPrefix(quality);
  const text = template.replace('{species}', `${prefix}${species.name}`);
  const color = quality ? getQualityInfo(quality).color : '#9E9E9E';
  return { text, color };
}
