import {
  PONDS,
  WORLD_POND_REGIONS,
  defaultAvatarPath,
  type ChatMessage,
  type PondUser,
} from '@fish-social/shared';

/** 服务端未连接时用于 Web 预览的演示数据 */
export const DEMO_WORLD = {
  regions: WORLD_POND_REGIONS,
  ponds: PONDS.map((p) => ({ id: p.id, name: p.name, regionId: p.regionId })),
  occupancy: Object.fromEntries(
    PONDS.map((p, i) => [p.id, [3, 7, 12, 5, 2, 4, 1, 6, 0, 3][i % 10]!]),
  ) as Record<string, number>,
};

export const DEMO_USERS: PondUser[] = [
  {
    id: 'demo-me',
    nickname: '钓友1234',
    color: '#64B5F6',
    avatarUrl: defaultAvatarPath('cat_avatar_orange.png'),
    spotId: 'calm-spot-3',
    status: 'fishing',
    fishingStartedAt: Date.now() - 125000,
    todayFishingMs: 125000,
  },
  {
    id: 'demo-2',
    nickname: '云中鹤',
    color: '#E57373',
    avatarUrl: defaultAvatarPath('cat_avatar_siamese.png'),
    spotId: 'calm-spot-1',
    status: 'fishing',
    fishingStartedAt: Date.now() - 3600000,
    todayFishingMs: 3600000,
  },
  {
    id: 'demo-3',
    nickname: '夜钓王',
    color: '#81C784',
    avatarUrl: defaultAvatarPath('cat_avatar_gray.png'),
    spotId: 'calm-spot-7',
    status: 'idle',
    fishingStartedAt: null,
    todayFishingMs: 0,
  },
];

export const DEMO_MESSAGES: ChatMessage[] = [
  {
    id: 'm1',
    pondId: 'pond-calm',
    userId: 'demo-2',
    nickname: '云中鹤',
    text: '今天鱼口不错啊',
    createdAt: Date.now() - 60000,
  },
  {
    id: 'm2',
    pondId: 'pond-calm',
    userId: 'demo-3',
    nickname: '夜钓王',
    text: '我刚到，找个位置坐坐',
    createdAt: Date.now() - 30000,
  },
];
