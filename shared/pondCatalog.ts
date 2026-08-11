/** 二十塘目录：展示名 / 短前缀 / 色板（FEAT-SCENE-TILE-3） */

export interface PondPalette {
  grass: string;
  dirt: string;
  water: string;
  shore: string;
  fishSpot: string;
  entry: string;
}

export interface PondCatalogEntry {
  id: string;
  name: string;
  regionId: string;
  prefix: string;
  palette: PondPalette;
}

export const POND_CATALOG: PondCatalogEntry[] = [
  {
    id: 'pond-calm',
    name: '静心湖',
    regionId: 'region-calm',
    prefix: 'calm',
    palette: {
      grass: '#7CB342',
      dirt: '#A1887F',
      water: '#42A5F5',
      shore: '#8D6E63',
      fishSpot: '#6D4C41',
      entry: '#1565C0',
    },
  },
  {
    id: 'pond-mist',
    name: '云雾塘',
    regionId: 'region-mist',
    prefix: 'mist',
    palette: {
      grass: '#9CCC65',
      dirt: '#B0BEC5',
      water: '#78909C',
      shore: '#90A4AE',
      fishSpot: '#546E7A',
      entry: '#455A64',
    },
  },
  {
    id: 'pond-sunset',
    name: '夕阳湾',
    regionId: 'region-sunset',
    prefix: 'sunset',
    palette: {
      grass: '#AED581',
      dirt: '#BCAAA4',
      water: '#FF8A65',
      shore: '#A1887F',
      fishSpot: '#8D6E63',
      entry: '#E64A19',
    },
  },
  {
    id: 'pond-bamboo',
    name: '竹林池',
    regionId: 'region-bamboo',
    prefix: 'bamboo',
    palette: {
      grass: '#66BB6A',
      dirt: '#A5D6A7',
      water: '#26A69A',
      shore: '#81C784',
      fishSpot: '#5D4037',
      entry: '#00695C',
    },
  },
  {
    id: 'pond-reed',
    name: '芦苇荡',
    regionId: 'region-reed',
    prefix: 'reed',
    palette: {
      grass: '#C5E1A5',
      dirt: '#D7CCC8',
      water: '#4DB6AC',
      shore: '#A1887F',
      fishSpot: '#6D4C41',
      entry: '#00897B',
    },
  },
  {
    id: 'pond-crystal',
    name: '晶石潭',
    regionId: 'region-crystal',
    prefix: 'crystal',
    palette: {
      grass: '#80CBC4',
      dirt: '#B0BEC5',
      water: '#29B6F6',
      shore: '#78909C',
      fishSpot: '#455A64',
      entry: '#0277BD',
    },
  },
  {
    id: 'pond-lotus',
    name: '荷香池',
    regionId: 'region-lotus',
    prefix: 'lotus',
    palette: {
      grass: '#81C784',
      dirt: '#CE93D8',
      water: '#64B5F6',
      shore: '#A1887F',
      fishSpot: '#6A1B9A',
      entry: '#7B1FA2',
    },
  },
  {
    id: 'pond-mirror',
    name: '镜面湖',
    regionId: 'region-mirror',
    prefix: 'mirror',
    palette: {
      grass: '#A5D6A7',
      dirt: '#CFD8DC',
      water: '#4FC3F7',
      shore: '#90A4AE',
      fishSpot: '#37474F',
      entry: '#0288D1',
    },
  },
  {
    id: 'pond-willow',
    name: '柳荫湾',
    regionId: 'region-willow',
    prefix: 'willow',
    palette: {
      grass: '#9CCC65',
      dirt: '#BCAAA4',
      water: '#26C6DA',
      shore: '#8D6E63',
      fishSpot: '#5D4037',
      entry: '#00838F',
    },
  },
  {
    id: 'pond-stone',
    name: '叠石矶',
    regionId: 'region-stone',
    prefix: 'stone',
    palette: {
      grass: '#A1887F',
      dirt: '#8D6E63',
      water: '#5C6BC0',
      shore: '#6D4C41',
      fishSpot: '#3E2723',
      entry: '#3949AB',
    },
  },
  {
    id: 'pond-spring',
    name: '清泉眼',
    regionId: 'region-spring',
    prefix: 'spring',
    palette: {
      grass: '#8BC34A',
      dirt: '#BCAAA4',
      water: '#00BCD4',
      shore: '#795548',
      fishSpot: '#5D4037',
      entry: '#0097A7',
    },
  },
  {
    id: 'pond-dusk',
    name: '暮色泊',
    regionId: 'region-dusk',
    prefix: 'dusk',
    palette: {
      grass: '#FFCC80',
      dirt: '#BCAAA4',
      water: '#FF7043',
      shore: '#8D6E63',
      fishSpot: '#6D4C41',
      entry: '#E64A19',
    },
  },
  {
    id: 'pond-pine',
    name: '松风潭',
    regionId: 'region-pine',
    prefix: 'pine',
    palette: {
      grass: '#558B2F',
      dirt: '#8D6E63',
      water: '#00897B',
      shore: '#6D4C41',
      fishSpot: '#3E2723',
      entry: '#00695C',
    },
  },
  {
    id: 'pond-coral',
    name: '珊瑚浅',
    regionId: 'region-coral',
    prefix: 'coral',
    palette: {
      grass: '#FFAB91',
      dirt: '#FFCCBC',
      water: '#26A69A',
      shore: '#A1887F',
      fishSpot: '#BF360C',
      entry: '#D84315',
    },
  },
  {
    id: 'pond-moon',
    name: '月影池',
    regionId: 'region-moon',
    prefix: 'moon',
    palette: {
      grass: '#90A4AE',
      dirt: '#78909C',
      water: '#5C6BC0',
      shore: '#546E7A',
      fishSpot: '#37474F',
      entry: '#303F9F',
    },
  },
  {
    id: 'pond-fern',
    name: '蕨影泽',
    regionId: 'region-fern',
    prefix: 'fern',
    palette: {
      grass: '#7CB342',
      dirt: '#A5D6A7',
      water: '#43A047',
      shore: '#689F38',
      fishSpot: '#33691E',
      entry: '#2E7D32',
    },
  },
  {
    id: 'pond-ridge',
    name: '岭下塘',
    regionId: 'region-ridge',
    prefix: 'ridge',
    palette: {
      grass: '#AED581',
      dirt: '#BCAAA4',
      water: '#039BE5',
      shore: '#8D6E63',
      fishSpot: '#5D4037',
      entry: '#0277BD',
    },
  },
  {
    id: 'pond-harbor',
    name: '渔港湾',
    regionId: 'region-harbor',
    prefix: 'harbor',
    palette: {
      grass: '#81D4FA',
      dirt: '#B0BEC5',
      water: '#0288D1',
      shore: '#607D8B',
      fishSpot: '#37474F',
      entry: '#01579B',
    },
  },
  {
    id: 'pond-orchid',
    name: '兰汀',
    regionId: 'region-orchid',
    prefix: 'orchid',
    palette: {
      grass: '#CE93D8',
      dirt: '#D1C4E9',
      water: '#7E57C2',
      shore: '#9575CD',
      fishSpot: '#4527A0',
      entry: '#5E35B1',
    },
  },
  {
    id: 'pond-frost',
    name: '霜华淀',
    regionId: 'region-frost',
    prefix: 'frost',
    palette: {
      grass: '#B3E5FC',
      dirt: '#CFD8DC',
      water: '#81D4FA',
      shore: '#90A4AE',
      fishSpot: '#546E7A',
      entry: '#0277BD',
    },
  },
];

export function getPondCatalogEntry(pondId: string): PondCatalogEntry | undefined {
  return POND_CATALOG.find((p) => p.id === pondId);
}
