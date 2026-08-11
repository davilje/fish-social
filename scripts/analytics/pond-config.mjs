/** 二十塘上限（与 shared/pondEcology.ts POND_STOCK_CONFIGS 模板轮转一致） */
export const POND_IDS = [
  'pond-calm',
  'pond-mist',
  'pond-sunset',
  'pond-bamboo',
  'pond-reed',
  'pond-crystal',
  'pond-lotus',
  'pond-mirror',
  'pond-willow',
  'pond-stone',
  'pond-spring',
  'pond-dusk',
  'pond-pine',
  'pond-coral',
  'pond-moon',
  'pond-fern',
  'pond-ridge',
  'pond-harbor',
  'pond-orchid',
  'pond-frost',
];

const TEMPLATE_MAX = [80, 70, 60, 75];

export const POND_MAX = Object.fromEntries(
  POND_IDS.map((id, i) => [id, TEMPLATE_MAX[i % TEMPLATE_MAX.length]]),
);

export function maxPopulation(pondId) {
  return POND_MAX[pondId] ?? null;
}
