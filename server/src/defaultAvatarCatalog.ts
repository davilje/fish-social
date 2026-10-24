import {
  DEFAULT_AVATARS,
  isDefaultAvatarPath,
  normalizeCatAssetPath,
} from '@fish-social/shared';

const CURRENT_CHARACTER_IDS = [
  'calico',
  'orange',
  'white',
  'silver',
  'brown',
  'cream',
  'blue',
  'black',
  'cow',
  'dafeiyu',
  'sangbiao',
] as const;

const RETIRED_CAT_IDS = ['gray', 'siamese', 'tuxedo'] as const;
const ORANGE_PATH = '/image/profile/cat_avatar_orange.png';

export function assertDefaultAvatarCatalog(): void {
  const loaded = new Set(DEFAULT_AVATARS.map((avatar) => avatar.id));
  for (const id of CURRENT_CHARACTER_IDS) {
    const url = `/image/profile/cat_avatar_${id}.png`;
    if (!loaded.has(id) || !isDefaultAvatarPath(url)) {
      throw new Error(
        `默认头像白名单过期，缺少 ${url}。shared/dist 落后于 defaultAvatars.ts，拒绝听端口。`,
      );
    }
  }
  if (loaded.size !== CURRENT_CHARACTER_IDS.length) {
    throw new Error(
      `默认头像白名单过期：dist 有 ${loaded.size} 个角色，源码合同是 ${CURRENT_CHARACTER_IDS.length} 个角色。`,
    );
  }
  for (const id of RETIRED_CAT_IDS) {
    const normalized = normalizeCatAssetPath(`/image/profile/cat_avatar_${id}.png`);
    if (normalized !== ORANGE_PATH || !isDefaultAvatarPath(normalized)) {
      throw new Error(`退役头像 ${id} 未归一到橘猫，shared/dist 与 defaultAvatars.ts 不一致。`);
    }
  }
  if (isDefaultAvatarPath('/image/profile/not-a-cat.png')) {
    throw new Error('未知头像不应通过默认头像白名单。');
  }
}
